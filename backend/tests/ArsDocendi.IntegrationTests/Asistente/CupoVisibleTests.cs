using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Auth;
using Modules.Asistente;
using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Cupo diario y mantenimiento, tal como se le muestran al usuario
/// (asistente-cupo-visible, grupo 7 de asistente-administracion-de-uso, y
/// tarea 6.5).
/// </summary>
public sealed class CupoVisibleTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_cupo_visible")
{
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private const string ContarDocentes = "SELECT count(*) AS cantidad FROM designaciones.designaciones";

    // ---------------------------------------------------------------- 6.5

    [Fact]
    public async Task Capacidades_reporta_mantenimiento_inactivo_por_default()
    {
        await SembrarAsync();
        var banco = Banco();

        var capacidades = await banco.Capacidades.ObtenerAsync(
            Secretaria, TestContext.Current.CancellationToken);

        Assert.False(capacidades.Mantenimiento.Activo);
        Assert.Null(capacidades.Mantenimiento.Razon);
        Assert.Equal(0, banco.Proveedor.Llamadas);
    }

    [Fact]
    public async Task Capacidades_reporta_mantenimiento_activo_con_su_razon()
    {
        await SembrarAsync();
        var disponibilidadDelModulo = new DisponibilidadDelModuloFalsa();
        await disponibilidadDelModulo.ActivarAsync(Secretaria, "mantenimiento programado", default);
        var banco = Banco(disponibilidadDelModulo: disponibilidadDelModulo);

        var capacidades = await banco.Capacidades.ObtenerAsync(
            Secretaria, TestContext.Current.CancellationToken);

        Assert.True(capacidades.Mantenimiento.Activo);
        Assert.Equal("mantenimiento programado", capacidades.Mantenimiento.Razon);
        Assert.Equal(0, banco.Proveedor.Llamadas);
    }

    // ---------------------------------------------------------------- 7.1

    [Fact]
    public async Task Capacidades_reporta_el_cupo_no_bloqueado_por_default()
    {
        await SembrarAsync();
        var banco = Banco();
        var ct = TestContext.Current.CancellationToken;

        var capacidades = await banco.Capacidades.ObtenerAsync(Secretaria, ct);

        Assert.False(capacidades.Cupo.Bloqueado);
        Assert.Null(capacidades.Cupo.Motivo);
        Assert.Equal(int.MaxValue, capacidades.Cupo.Restante);
    }

    [Fact]
    public async Task Capacidades_reporta_bloqueado_por_presupuesto_propio_con_hora_de_vuelta()
    {
        await SembrarAsync();
        var banco = Banco(cupoDiario: 1);
        var ct = TestContext.Current.CancellationToken;

        await banco.Cuota.AnotarAsync(Secretaria, ct);

        var capacidades = await banco.Capacidades.ObtenerAsync(Secretaria, ct);

        Assert.True(capacidades.Cupo.Bloqueado);
        Assert.Equal(EstadoDelCupoDelActor.MotivoPresupuestoPropio, capacidades.Cupo.Motivo);
        Assert.NotNull(capacidades.Cupo.VuelveA);
    }

    [Fact]
    public async Task Capacidades_reporta_bloqueado_por_tope_organizacional_sin_hora_de_vuelta()
    {
        await SembrarAsync();
        var banco = Banco(topeOrganizacional: 1);
        var ct = TestContext.Current.CancellationToken;

        await banco.PresupuestoOrganizacional.AcumularAsync("guionado/x", DateTimeOffset.UtcNow, 1, 1, null, ct);

        var capacidades = await banco.Capacidades.ObtenerAsync(Secretaria, ct);

        Assert.True(capacidades.Cupo.Bloqueado);
        Assert.Equal(EstadoDelCupoDelActor.MotivoTopeOrganizacional, capacidades.Cupo.Motivo);
        Assert.Null(capacidades.Cupo.VuelveA);
    }

    [Fact]
    public async Task Un_admin_no_se_ve_a_si_mismo_bloqueado_por_mantenimiento()
    {
        await SembrarAsync();
        var disponibilidadDelModulo = new DisponibilidadDelModuloFalsa();
        await disponibilidadDelModulo.ActivarAsync(Secretaria, "mantenimiento", default);
        var identidad = new ConsultasIdentityFalsa();
        identidad.FijarPermisos(Secretaria, Permisos.AsistenteAdministrar);

        var banco = Banco(disponibilidadDelModulo: disponibilidadDelModulo, identidad: identidad);
        var ct = TestContext.Current.CancellationToken;

        var capacidades = await banco.Capacidades.ObtenerAsync(Secretaria, ct);

        // El global sigue diciendo la verdad (para el banner)...
        Assert.True(capacidades.Mantenimiento.Activo);
        // ...pero el admin no se ve a sí mismo bloqueado por esa causa.
        Assert.False(capacidades.Cupo.Bloqueado);
    }

    // ---------------------------------------------------------------- 7.2

    [Fact]
    public async Task La_respuesta_del_turno_trae_el_cupo_posterior_al_cobro_no_el_previo()
    {
        await SembrarAsync();
        var banco = Banco(
            cupoDiario: 5, guion: [ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes."]);
        var ct = TestContext.Current.CancellationToken;

        var antes = (await banco.Capacidades.ObtenerAsync(Secretaria, ct)).Cupo.Restante;

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes están designados?", ct);

        Assert.NotNull(turno.CupoRestante);
        Assert.Equal(antes - 1, turno.CupoRestante);
    }

    [Fact]
    public async Task Un_saludo_no_mueve_el_cupo_restante_del_turno()
    {
        await SembrarAsync();
        var banco = Banco(cupoDiario: 5);
        var ct = TestContext.Current.CancellationToken;

        var antes = (await banco.Capacidades.ObtenerAsync(Secretaria, ct)).Cupo.Restante;

        var turno = await banco.Capa().ResponderAsync(Secretaria, null, "hola", ct);

        Assert.Equal(antes, turno.CupoRestante);
    }

    // ------------------------------------------------------------------ apoyo

    private BancoDelAsistente Banco(
        int cupoDiario = 0,
        decimal topeOrganizacional = 0,
        DisponibilidadDelModuloFalsa? disponibilidadDelModulo = null,
        ConsultasIdentityFalsa? identidad = null,
        params string[] guion)
    {
        var (basica, pii) = CadenasDeLectura();

        return BancoDelAsistente.Armar(
            basica, pii, ClasificadorDeSensibilidad(), Apertura,
            new OpcionesAsistente(),
            cupoDiario: cupoDiario,
            topeOrganizacional: topeOrganizacional,
            disponibilidadDelModuloFalsa: disponibilidadDelModulo,
            identidadFalsa: identidad,
            guion: guion);
    }
}
