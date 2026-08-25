using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica el catálogo de capacidades por actor (ARS-49).
/// </summary>
/// <remarks>
/// El invariante de privacidad de esta pieza es el más importante de la épica:
/// <b>el catálogo se deriva de los GRANT efectivos y nunca del payload del
/// prompt</b>. El prefijo trae el esquema entero, columnas personales incluidas; un
/// catálogo derivado de ahí le ofrecería a cualquiera preguntas sobre columnas que
/// su rol no puede leer.
/// </remarks>
[Collection(ColeccionPostgres.Nombre)]
public sealed class CapacidadesTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_capacidades")
{
    /// <summary>Alcance global y acceso a datos personales.</summary>
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");

    /// <summary>Ámbito de carrera: sin acceso a datos personales.</summary>
    private static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");

    /// <summary>Ámbito de materia: mismo acceso a datos que el coordinador.</summary>
    private static readonly Guid Jefe = Guid.Parse("a0000000-0000-4000-8000-000000000002");

    private static readonly string[] ColumnasPersonales =
        ["documento", "cuil", "fecha_nacimiento", "telefono", "upn"];

    // ------------------------------------------------- derivado de los GRANT

    [Fact]
    public async Task El_catalogo_de_un_rol_basico_no_cuenta_ninguna_columna_personal()
    {
        // La primera versión de este test miraba el TEXTO redactado buscando
        // «documento», y pasaba aunque el catálogo se armara con la conexión de datos
        // personales: la redacción nunca nombra columnas, así que no había nada que
        // encontrar. Ahora se mira el conteo de la tabla que tiene las columnas
        // personales, que es lo único que se movería si el catálogo dejara de
        // derivarse del rol del actor.
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;
        var catalogo = Catalogo();

        var sinPii = await catalogo.ObtenerAsync(Coordinador, ct);
        var conPii = await catalogo.ObtenerAsync(Secretaria, ct);

        var personasBasica = Assert.Single(
            sinPii.Cubre, a => a.Nombre == "identity.personas");
        var personasConPii = Assert.Single(
            conPii.Cubre, a => a.Nombre == "identity.personas");

        // Cuatro de las cinco columnas personales están en `identity.personas`
        // —documento, cuil, fecha_nacimiento y teléfono—; la quinta, `upn`, está en
        // `identity.users`.
        Assert.Equal(4, personasConPii.Columnas - personasBasica.Columnas);

        var usuariosBasica = Assert.Single(sinPii.Cubre, a => a.Nombre == "identity.users");
        var usuariosConPii = Assert.Single(conPii.Cubre, a => a.Nombre == "identity.users");

        Assert.Equal(1, usuariosConPii.Columnas - usuariosBasica.Columnas);
    }

    [Fact]
    public async Task Dos_actores_con_acceso_distinto_reciben_conteos_distintos()
    {
        // ES EL GATE DE LA ÉPICA. Si los dos vieran lo mismo, el catálogo no estaría
        // derivándose de los privilegios sino de algo compartido.
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;
        var catalogo = Catalogo();

        var conPii = await catalogo.ObtenerAsync(Secretaria, ct);
        var sinPii = await catalogo.ObtenerAsync(Coordinador, ct);

        Assert.True(conPii.Columnas > sinPii.Columnas,
            $"Con datos personales: {conPii.Columnas}; sin: {sinPii.Columnas}.");

        // Cinco columnas exactamente: documento, cuil, fecha_nacimiento, telefono y upn.
        Assert.Equal(5, conPii.Columnas - sinPii.Columnas);
    }

    [Fact]
    public async Task Los_conteos_coinciden_con_lo_que_el_rol_puede_leer()
    {
        await SembrarAsync();

        var puede = await Catalogo().ObtenerAsync(
            Coordinador, TestContext.Current.CancellationToken);

        var (basica, _) = CadenasDeLectura();
        await using var conexion = new NpgsqlConnection(basica.Valor);
        await conexion.OpenAsync(TestContext.Current.CancellationToken);

        var legibles = await LectorDeCatalogo.LeerColumnasAsync(
            conexion, TestContext.Current.CancellationToken);

        Assert.Equal(legibles.Count, puede.Columnas);
        Assert.Equal(
            legibles.Select(c => $"{c.Esquema}.{c.Tabla}").Distinct().Count(),
            puede.Tablas);
    }

    [Fact]
    public async Task El_catalogo_no_ofrece_el_schema_propio_del_asistente()
    {
        // Los dos registros del asistente están revocados a sus propios roles. Si
        // aparecieran acá, el catálogo estaría ofreciendo consultar el texto de las
        // preguntas de todos los demás.
        await SembrarAsync();

        var puede = await Catalogo().ObtenerAsync(
            Secretaria, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(puede.Cubre, area =>
            area.Nombre.StartsWith("asistente.", StringComparison.Ordinal));
        Assert.DoesNotContain(puede.Cubre, area =>
            area.Nombre.StartsWith("audit.", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------- los ejemplos

    [Fact]
    public async Task Los_ejemplos_salen_del_catalogo_verificado()
    {
        await SembrarAsync();
        var verificados = new SelectorDeEjemplos().Catalogo
            .Select(e => e.Pregunta)
            .ToHashSet(StringComparer.Ordinal);

        var puede = await Catalogo().ObtenerAsync(
            Secretaria, TestContext.Current.CancellationToken);

        Assert.NotEmpty(puede.Ejemplos);
        Assert.All(puede.Ejemplos, ejemplo => Assert.Contains(ejemplo, verificados));
    }

    [Fact]
    public async Task Se_ofrecen_entre_cuatro_y_seis_ejemplos()
    {
        await SembrarAsync();

        var puede = await Catalogo().ObtenerAsync(
            Secretaria, TestContext.Current.CancellationToken);

        Assert.InRange(puede.Ejemplos.Count, 4, 6);
    }

    [Fact]
    public async Task Una_consulta_con_una_columna_personal_no_es_ejecutable_por_el_rol_basico()
    {
        // EL FILTRO, PROBADO CON UNA CONSULTA SINTÉTICA. El catálogo de ejemplos real
        // no tiene ninguna que toque datos personales, así que sobre datos reales el
        // filtro es hoy un no-op — y sin este test nadie sabría si funciona el día
        // que alguien agregue el primero.
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;
        var (basica, pii) = CadenasDeLectura();
        const string ConDatosPersonales = "SELECT documento FROM identity.personas";

        await using var conBasica = new NpgsqlConnection(basica.Valor);
        await conBasica.OpenAsync(ct);

        await using var conPii = new NpgsqlConnection(pii.Valor);
        await conPii.OpenAsync(ct);

        Assert.False(await CatalogoDeCapacidades.EjecutableAsync(
            conBasica, Secretaria, ConDatosPersonales, ct));

        Assert.True(await CatalogoDeCapacidades.EjecutableAsync(
            conPii, Secretaria, ConDatosPersonales, ct));
    }

    [Fact]
    public void Hoy_ningun_ejemplo_del_catalogo_toca_una_columna_personal()
    {
        // Deja escrito el estado real: el filtro protege de algo que todavía no
        // pasó. Si este test empieza a fallar, quiere decir que se agregó un ejemplo
        // con datos personales, y entonces el filtro pasa a tener efecto — que es lo
        // que el test de arriba ya verificó que funciona.
        var conPersonales = new SelectorDeEjemplos().Catalogo
            .Where(e => ColumnasPersonales.Any(c =>
                e.Sql.Contains(c, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.Empty(conPersonales);
    }

    // ------------------------------------------------- alcance, límites, caché

    [Fact]
    public async Task El_ambito_no_altera_los_conteos_pero_si_el_alcance_informado()
    {
        // El ámbito cambia QUÉ FILAS ve, no QUÉ PUEDE PREGUNTAR. Meterlo en los
        // conteos los haría mentir en las dos direcciones.
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;
        var catalogo = Catalogo();

        var deCarrera = await catalogo.ObtenerAsync(Coordinador, ct);
        var deMateria = await catalogo.ObtenerAsync(Jefe, ct);
        var global = await catalogo.ObtenerAsync(Secretaria, ct);

        Assert.Equal(deCarrera.Columnas, deMateria.Columnas);
        Assert.Equal(deCarrera.Alcance, deMateria.Alcance);
        Assert.NotEqual(global.Alcance, deCarrera.Alcance);
    }

    [Fact]
    public async Task El_catalogo_dice_que_no_escribe_y_que_no_sale_del_sistema()
    {
        await SembrarAsync();

        var puede = await Catalogo().ObtenerAsync(
            Secretaria, TestContext.Current.CancellationToken);

        Assert.NotEmpty(puede.NoPuede);
        Assert.Contains(puede.NoPuede, l =>
            l.Contains("No modifica", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(puede.NoPuede, l =>
            l.Contains("Guaraní", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task El_catalogo_se_cachea_por_rol()
    {
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;
        var catalogo = Catalogo();

        await catalogo.ObtenerAsync(Coordinador, ct);
        await catalogo.ObtenerAsync(Jefe, ct);

        // Los dos usan el rol básico: una sola lectura del catálogo de PostgreSQL.
        Assert.Equal(1, catalogo.Lecturas);

        await catalogo.ObtenerAsync(Secretaria, ct);

        // El de datos personales es otra variante.
        Assert.Equal(2, catalogo.Lecturas);
    }

    // ------------------------------------------------------ la meta-pregunta

    [Fact]
    public async Task La_meta_pregunta_responde_con_el_catalogo_real_y_cero_llamadas()
    {
        await SembrarAsync();
        var banco = Banco();

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿qué podés hacer?", TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);
        Assert.Equal(0, banco.Proveedor.Llamadas);
        Assert.Equal(0, turno.LlamadasAlModelo);

        // Menciona ejemplos reales del catálogo verificado, y viajan como
        // sugerencias para que la interfaz los pueda hacer clicables.
        Assert.NotEmpty(turno.Sugerencias!);
        Assert.Contains(turno.Sugerencias![0], turno.Respuesta, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Con_el_corte_al_proveedor_abierto_la_meta_pregunta_sigue_respondiendo()
    {
        await SembrarAsync();
        var banco = Banco(new OpcionesAsistente
        {
            FallosParaAbrirElBreaker = 1,
            CupoDeLlamadasPorActor = 0,
        });

        banco.Breaker.Fallo();

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿qué podés hacer?", TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);
        Assert.NotEmpty(turno.Sugerencias!);
        Assert.Equal(0, banco.Proveedor.Llamadas);
    }

    [Fact]
    public async Task La_redaccion_no_nombra_tablas_ni_schemas()
    {
        // Son etiquetas internas (RNF-18). Lo que se muestra son los comentarios del
        // catálogo, que están escritos para leerse.
        await SembrarAsync();

        var texto = RedaccionDeCapacidades.Texto(
            await Catalogo().ObtenerAsync(Secretaria, TestContext.Current.CancellationToken));

        Assert.DoesNotContain("identity.", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("designaciones.", texto, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ apoyo

    private CatalogoDeCapacidades Catalogo()
    {
        var (basica, pii) = CadenasDeLectura();

        return new CatalogoDeCapacidades(
            basica,
            pii,
            new ConsultorDeAlcance(basica),
            new SelectorDeEjemplos(),
            new CacheDeCapacidades(),
            NullLogger<CatalogoDeCapacidades>.Instance);
    }

    private BancoDelAsistente Banco(OpcionesAsistente? configuracion = null)
    {
        var (basica, pii) = CadenasDeLectura();

        return BancoDelAsistente.Armar(
            basica,
            pii,
            ClasificadorDeSensibilidad(),
            configuracion ?? new OpcionesAsistente { CupoDeLlamadasPorActor = 0 });
    }

    private async Task SembrarAsync()
    {
        var sql = await File.ReadAllTextAsync(
            Path.Combine(RaizRepositorio.Ruta(), "infra", "scripts", "seed-data", "sintetico.sql"),
            TestContext.Current.CancellationToken);

        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = 60 };
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
