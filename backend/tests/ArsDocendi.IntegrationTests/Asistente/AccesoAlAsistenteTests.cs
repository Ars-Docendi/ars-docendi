using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El acceso operativo por rol y la revocación por usuario, contra Postgres
/// real (asistente-acceso-granular), más el corte del turno sin acceso y el
/// restablecimiento del cupo propio (design.md D5).
/// </summary>
public sealed class AccesoAlAsistenteTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_acceso")
{
    private static readonly DateTimeOffset Ancla = new(2026, 9, 30, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static readonly Guid Administrador = Guid.Parse("a0000000-0000-4000-8000-000000000001");

    [Fact]
    public async Task Por_defecto_todos_tienen_acceso()
    {
        await SembrarAsync();

        Assert.True(await Acceso().TieneAccesoAsync(Secretaria, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Revocar_quita_el_acceso_y_restablecer_lo_devuelve()
    {
        await SembrarAsync();
        var presupuestos = Presupuestos();
        var ct = TestContext.Current.CancellationToken;

        var revocar = await presupuestos.EditarAccesoDeUsuarioAsync(Secretaria, false, Administrador, ct);
        Assert.Equal((false, true), revocar);
        Assert.False(await Acceso().TieneAccesoAsync(Secretaria, ct));
        Assert.Contains(Secretaria, (await presupuestos.ObtenerEstadoAsync(ct)).AccesosRevocados);

        var restablecer = await presupuestos.EditarAccesoDeUsuarioAsync(Secretaria, true, Administrador, ct);
        Assert.Equal((true, false), restablecer);
        Assert.True(await Acceso().TieneAccesoAsync(Secretaria, ct));
    }

    [Fact]
    public async Task Apagar_el_rol_quita_el_acceso_a_sus_usuarios()
    {
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;

        var (antes, despues) = await Presupuestos().EditarAccesoDeRolAsync("secretaria", false, ct);

        Assert.True(antes);
        Assert.False(despues);
        Assert.False(await Acceso().TieneAccesoAsync(Secretaria, ct));
        var estado = await Presupuestos().ObtenerEstadoAsync(ct);
        Assert.False(estado.CuposPorRol.Single(c => c.Rol == "secretaria").AccesoHabilitado);
    }

    [Fact]
    public async Task Otro_rol_con_acceso_lo_mantiene()
    {
        await SembrarAsync();
        await AgregarRolAsync(Secretaria, "decanato");
        var ct = TestContext.Current.CancellationToken;

        await Presupuestos().EditarAccesoDeRolAsync("secretaria", false, ct);

        Assert.True(await Acceso().TieneAccesoAsync(Secretaria, ct));
    }

    [Fact]
    public async Task Restablecer_el_cupo_propio_vuelve_al_del_rol_sin_borrar_la_historia()
    {
        await SembrarAsync();
        var presupuestos = Presupuestos();
        var ct = TestContext.Current.CancellationToken;
        await presupuestos.EditarCupoDeRolAsync("secretaria", 20, ct);
        await presupuestos.EditarOverrideDeUsuarioAsync(Secretaria, 60, ct);

        var antes = await presupuestos.RestablecerOverrideDeUsuarioAsync(Secretaria, ct);

        Assert.Equal(60, antes);
        Assert.Equal(20, await Cuota().CupoRestanteAsync(Secretaria, ct));
        Assert.DoesNotContain(
            (await presupuestos.ObtenerEstadoAsync(ct)).OverridesPorUsuario, o => o.ActorId == Secretaria);
        Assert.Equal(1L, await ContarAsync(
            "SELECT count(*) FROM asistente.presupuesto_usuario WHERE vigente_hasta IS NOT NULL"));
    }

    [Fact]
    public async Task Restablecer_sin_override_no_hace_nada()
    {
        await SembrarAsync();

        Assert.Null(await Presupuestos().RestablecerOverrideDeUsuarioAsync(
            Secretaria, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Sin_acceso_el_turno_no_llama_al_modelo_ni_para_saludar()
    {
        await SembrarAsync();
        var (basica, pii) = CadenasDeLectura();
        var banco = BancoDelAsistente.Armar(
            basica, pii, ClasificadorDeSensibilidad(), Apertura,
            reloj: new RelojFijo(Ancla),
            acceso: new AccesoAlAsistenteFalso(tieneAcceso: false),
            guion: [ProveedorGuionado.Generacion("SELECT 1 AS uno"), "Uno."]);
        var ct = TestContext.Current.CancellationToken;

        var consulta = await banco.Capa().ResponderAsync(Secretaria, null, "¿cuántos pedidos hay?", ct);
        var saludo = await banco.Capa().ResponderAsync(Secretaria, null, "hola", ct);

        Assert.Equal(EstadoDelTurno.ServicioDegradado, consulta.Estado);
        Assert.Equal(PoliticaDeAbstencion.TextoSinAcceso, consulta.Respuesta);
        Assert.Equal(EstadoDelTurno.ServicioDegradado, saludo.Estado);
        Assert.Equal(0, banco.Proveedor.Llamadas);
    }

    private AccesoPersistente Acceso() =>
        new(new CadenaDuena(Cadena), new ConsultasIdentity(PostgresFixture.CrearIdentity(Cadena)));

    private PresupuestosAdministrablesReal Presupuestos() =>
        new(new CadenaDuena(Cadena), new RelojFijo(Ancla),
            new ConsultasIdentity(PostgresFixture.CrearIdentity(Cadena)));

    private CuotaPersistente Cuota() =>
        new(new CadenaDuena(Cadena), new ConsultasIdentity(PostgresFixture.CrearIdentity(Cadena)),
            new RelojFijo(Ancla), new DetectorDeUmbrales(), NullLogger<CuotaPersistente>.Instance);

    private async Task<long> ContarAsync(string sql)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        return (long)(await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private async Task AgregarRolAsync(Guid actor, string rol)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO identity.user_roles (user_id, role_id)
            SELECT @actor, id FROM identity.roles WHERE code = @rol
            """, conexion);
        comando.Parameters.AddWithValue("actor", actor);
        comando.Parameters.AddWithValue("rol", rol);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
