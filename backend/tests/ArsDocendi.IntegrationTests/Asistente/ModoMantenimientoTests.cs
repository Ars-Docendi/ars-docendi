using System.IO;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Persistencia;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El kill switch de mantenimiento (asistente-modo-mantenimiento, design.md
/// D7/D8 de asistente-administracion-de-uso).
/// </summary>
public sealed class ModoMantenimientoTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_modo_mantenimiento")
{
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private const string ContarDocentes = "SELECT count(*) AS cantidad FROM designaciones.designaciones";

    // ------------------------------------------------------------- 6.1, el puerto

    [Fact]
    public async Task Inactivo_por_default_y_reporta_activo_con_razon_tras_activar()
    {
        var cadena = new CadenaDuena(Cadena);
        var disponibilidad = new DisponibilidadDelModuloReal(cadena);
        var ct = TestContext.Current.CancellationToken;

        var inicial = await disponibilidad.ConsultarAsync(ct);
        Assert.False(inicial.Activo);
        Assert.Null(inicial.Razon);

        await disponibilidad.ActivarAsync(Secretaria, "mantenimiento programado", ct);
        var activo = await disponibilidad.ConsultarAsync(ct);

        Assert.True(activo.Activo);
        Assert.Equal("mantenimiento programado", activo.Razon);

        await disponibilidad.DesactivarAsync(Secretaria, ct);
        var desactivado = await disponibilidad.ConsultarAsync(ct);

        Assert.False(desactivado.Activo);
        Assert.Null(desactivado.Razon);
    }

    // ---------------------------------------------------- 6.7, sin caché de proceso

    [Fact]
    public async Task El_toggle_es_visible_desde_una_segunda_conexion_independiente()
    {
        var cadena = new CadenaDuena(Cadena);
        var primeraInstancia = new DisponibilidadDelModuloReal(cadena);
        var segundaInstancia = new DisponibilidadDelModuloReal(cadena);
        var ct = TestContext.Current.CancellationToken;

        Assert.False((await segundaInstancia.ConsultarAsync(ct)).Activo);

        await primeraInstancia.ActivarAsync(Secretaria, "verificando", ct);

        // Una instancia DISTINTA —simulando un segundo Host— ve el cambio de
        // inmediato, sin ningún mecanismo de invalidación de caché.
        Assert.True((await segundaInstancia.ConsultarAsync(ct)).Activo);
    }

    // -------------------------------------------------- 6.6, bypass del admin

    [Fact]
    public async Task Con_mantenimiento_activo_un_actor_sin_permiso_queda_degradado()
    {
        await SembrarAsync();
        var (basica, pii) = CadenasDeLectura();
        var disponibilidadDelModulo = new DisponibilidadDelModuloFalsa();
        await disponibilidadDelModulo.ActivarAsync(Secretaria, "mantenimiento programado", default);

        var banco = BancoDelAsistente.Armar(
            basica, pii, ClasificadorDeSensibilidad(), Apertura,
            new OpcionesAsistente(),
            disponibilidadDelModuloFalsa: disponibilidadDelModulo);

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes están designados?",
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.ServicioDegradado, turno.Estado);
        Assert.Equal(PoliticaDeAbstencion.TextoMantenimiento("mantenimiento programado"), turno.Respuesta);
        Assert.Equal(0, banco.Proveedor.Llamadas);
    }

    [Fact]
    public async Task Con_mantenimiento_activo_un_admin_sigue_procesando_turnos()
    {
        await SembrarAsync();
        var (basica, pii) = CadenasDeLectura();
        var disponibilidadDelModulo = new DisponibilidadDelModuloFalsa();
        await disponibilidadDelModulo.ActivarAsync(Secretaria, "mantenimiento programado", default);

        var identidad = new ConsultasIdentityFalsa();
        identidad.FijarPermisos(Secretaria, Permisos.AsistenteAdministrar);

        var banco = BancoDelAsistente.Armar(
            basica, pii, ClasificadorDeSensibilidad(), Apertura,
            new OpcionesAsistente(),
            disponibilidadDelModuloFalsa: disponibilidadDelModulo,
            identidadFalsa: identidad,
            guion: [ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes."]);

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes están designados?",
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);
    }

    [Fact]
    public async Task El_admin_sigue_bloqueado_por_su_propio_cupo_durante_el_mantenimiento()
    {
        // El bypass es SÓLO del motivo Mantenimiento (design.md D8): el admin
        // no queda exento de su cupo propio.
        await SembrarAsync();
        var (basica, pii) = CadenasDeLectura();
        var disponibilidadDelModulo = new DisponibilidadDelModuloFalsa();
        await disponibilidadDelModulo.ActivarAsync(Secretaria, "mantenimiento", default);

        var identidad = new ConsultasIdentityFalsa();
        identidad.FijarPermisos(Secretaria, Permisos.AsistenteAdministrar);

        var banco = BancoDelAsistente.Armar(
            basica, pii, ClasificadorDeSensibilidad(), Apertura,
            new OpcionesAsistente(),
            cupoDiario: 1,
            disponibilidadDelModuloFalsa: disponibilidadDelModulo,
            identidadFalsa: identidad,
            guion: [ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes."]);

        var ct = TestContext.Current.CancellationToken;
        await banco.Capa().ResponderAsync(Secretaria, null, "¿cuántos docentes están designados?", ct);

        var segundo = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos pedidos hay?", ct);

        Assert.Equal(EstadoDelTurno.ServicioDegradado, segundo.Estado);
        Assert.Contains("límite", segundo.Respuesta, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------- 6.8, sin Azure

    [Fact]
    public void Ninguna_referencia_a_Azure_App_Configuration_en_el_modulo()
    {
        var csproj = File.ReadAllText(
            Path.Combine(RaizRepositorio.BackendSrc(), "Modules.Asistente", "Modules.Asistente.csproj"));

        Assert.DoesNotContain("Azure.Data.AppConfiguration", csproj, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Microsoft.Azure.AppConfiguration", csproj, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Microsoft.Extensions.Configuration.AzureAppConfiguration", csproj,
            StringComparison.OrdinalIgnoreCase);
    }
}
