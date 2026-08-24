using ArsDocendi.Shared;
using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Asistente;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica las tres cadenas de conexión tipadas.
/// </summary>
/// <remarks>
/// No necesitan base: lo que se prueba es cómo se arman y qué garantiza el sistema
/// de tipos. Se componen los servicios reales del Host —<c>AddArsDocendiShared</c>
/// y <c>AddAsistenteModule</c>— para que sea la registración de producción la que
/// se ejercita, no una reconstrucción.
/// </remarks>
public sealed class CadenasDeConexionTests
{
    private const string CadenaDelDueno =
        "Host=arsdocendi-postgres;Port=5432;Database=arsdocendi_pr_123;Username=app_pr_123;Password=secreto-del-dueno";

    [Fact]
    public void Los_tres_tipos_resuelven_a_usuarios_de_base_distintos()
    {
        using var servicios = Componer().BuildServiceProvider();

        var duena = servicios.GetRequiredService<CadenaDuena>();
        var soloLectura = servicios.GetRequiredService<CadenaSoloLectura>();
        var conPii = servicios.GetRequiredService<CadenaSoloLecturaPii>();

        Assert.Equal("app_pr_123", duena.Usuario);
        Assert.Equal("asistente_ro_pr_123", soloLectura.Usuario);
        Assert.Equal("asistente_ro_pii_pr_123", conPii.Usuario);
        Assert.Equal(3, new[] { duena.Usuario, soloLectura.Usuario, conPii.Usuario }.Distinct().Count());
    }

    [Fact]
    public void Las_cadenas_derivadas_apuntan_a_la_misma_base_que_la_del_dueno()
    {
        using var servicios = Componer().BuildServiceProvider();

        var duena = new NpgsqlConnectionStringBuilder(
            servicios.GetRequiredService<CadenaDuena>().Valor);

        foreach (var derivada in new[]
                 {
                     servicios.GetRequiredService<CadenaSoloLectura>().Valor,
                     servicios.GetRequiredService<CadenaSoloLecturaPii>().Valor,
                 })
        {
            var partes = new NpgsqlConnectionStringBuilder(derivada);

            // Es el punto de derivar en vez de configurar tres cadenas sueltas: con
            // tres independientes, un typo en el nombre de la base haría que el
            // asistente leyera otro ambiente sin que nada fallara.
            Assert.Equal(duena.Host, partes.Host);
            Assert.Equal(duena.Port, partes.Port);
            Assert.Equal(duena.Database, partes.Database);
            Assert.NotEqual(duena.Username, partes.Username);
        }
    }

    [Fact]
    public void Los_tres_tipos_son_independientes_entre_si()
    {
        Type[] tipos = [typeof(CadenaDuena), typeof(CadenaSoloLectura), typeof(CadenaSoloLecturaPii)];

        // Esto es lo que hace que pasar la cadena equivocada no compile. Un test no
        // puede afirmar «esto no compila» sin un compilador adentro; sí puede afirmar
        // las propiedades del diseño de tipos de las que esa garantía depende: nada
        // de herencia común, nada de conversiones.
        foreach (var tipo in tipos)
        {
            Assert.Equal(typeof(object), tipo.BaseType);
            Assert.DoesNotContain(tipo.GetMethods(), m => m.Name is "op_Implicit" or "op_Explicit");
        }

        foreach (var origen in tipos)
        {
            foreach (var destino in tipos.Where(t => t != origen))
            {
                Assert.False(
                    destino.IsAssignableFrom(origen),
                    $"{origen.Name} no debería poder pasar donde se espera {destino.Name}.");
            }
        }
    }

    [Fact]
    public void La_cadena_no_expone_la_contrasena_al_convertirse_en_texto()
    {
        var duena = new CadenaDuena(CadenaDelDueno);

        var texto = duena.ToString();

        // El accidente que esto previene es interpolar la cadena en un log o en un
        // mensaje de excepción. El valor crudo sigue disponible en Valor, que hay
        // que pedir a propósito.
        Assert.DoesNotContain("secreto-del-dueno", texto, StringComparison.Ordinal);
        Assert.Contains("arsdocendi_pr_123", texto, StringComparison.Ordinal);
        Assert.Contains("secreto-del-dueno", duena.Valor, StringComparison.Ordinal);
    }

    [Fact]
    public void Sin_cadena_configurada_el_error_nombra_la_clave_que_falta()
    {
        var vacia = new ConfigurationBuilder().Build();

        var error = Assert.Throws<InvalidOperationException>(() => CadenaDuena.Desde(vacia));

        Assert.Contains(CadenaDuena.Clave, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Sin_rol_configurado_la_cadena_de_solo_lectura_falla_nombrando_la_clave()
    {
        using var servicios = Componer(conRolesDelAsistente: false).BuildServiceProvider();

        var error = Assert.Throws<InvalidOperationException>(
            servicios.GetRequiredService<CadenaSoloLectura>);

        Assert.Contains(nameof(OpcionesAsistente.RolSoloLectura), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void El_Host_arranca_aunque_falten_los_roles_del_asistente()
    {
        // Contrapartida del test anterior: la configuración del asistente no puede
        // ser condición de arranque. Un ambiente que todavía no la tiene debe seguir
        // sirviendo el resto del sistema; el error llega recién a quien pide la cadena.
        using var servicios = Componer(conRolesDelAsistente: false).BuildServiceProvider();

        var duena = servicios.GetRequiredService<CadenaDuena>();

        Assert.Equal("app_pr_123", duena.Usuario);
    }

    [Fact]
    public void Una_cadena_vacia_se_rechaza_al_construirla()
    {
        Assert.Throws<ArgumentException>(() => new CadenaDuena("  "));
        Assert.Throws<ArgumentException>(() => new CadenaSoloLectura(""));
        Assert.Throws<ArgumentException>(() => new CadenaSoloLecturaPii(""));
    }

    private static ServiceCollection Componer(bool conRolesDelAsistente = true)
    {
        var valores = new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{CadenaDuena.Clave}"] = CadenaDelDueno,
        };

        if (conRolesDelAsistente)
        {
            valores[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.RolSoloLectura)}"] =
                "asistente_ro_pr_123";
            valores[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.RolSoloLecturaPii)}"] =
                "asistente_ro_pii_pr_123";
            valores[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.PasswordSoloLectura)}"] =
                "clave-ro";
            valores[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.PasswordSoloLecturaPii)}"] =
                "clave-ro-pii";
        }

        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddArsDocendiShared(configuracion);
        servicios.AddAsistenteModule(configuracion);
        return servicios;
    }
}
