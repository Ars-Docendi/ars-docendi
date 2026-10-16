using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El cupo diario de turnos, persistente en Postgres
/// (asistente-presupuesto-persistente, design.md D2/D4 de
/// asistente-administracion-de-uso).
/// </summary>
/// <remarks>
/// Tarea 3.1: un fake en memoria (<see cref="CuotaDeActorFalsa"/>) fija el
/// contrato de <see cref="ICuotaDelActor"/> sin pagar Postgres. Tareas
/// 3.2/3.4/3.5/3.6: <see cref="CuotaPersistente"/> contra una base real,
/// usando el mismo <c>ConsultasIdentity</c> que el resto del sistema.
/// </remarks>
public sealed class CuotaPersistenteTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_cuota_persistente")
{
    private static readonly DateTimeOffset Ancla = new(2026, 9, 25, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");

    // ---------------------------------------------------------- 3.1, el fake

    [Fact]
    public async Task El_fake_bloquea_al_llegar_al_limite_y_desbloquea_al_otro_dia()
    {
        var reloj = new RelojFijo(Ancla);
        var cuota = new CuotaDeActorFalsa(cupoDiario: 2, reloj);
        var ct = TestContext.Current.CancellationToken;

        await cuota.AnotarAsync(Secretaria, ct);
        Assert.True(await cuota.HayCupoAsync(Secretaria, ct));

        await cuota.AnotarAsync(Secretaria, ct);
        Assert.False(await cuota.HayCupoAsync(Secretaria, ct));

        reloj.Avanzar(TimeSpan.FromHours(24));
        Assert.True(await cuota.HayCupoAsync(Secretaria, ct));
    }

    // --------------------------------------------------- 3.2, contra Postgres

    [Fact]
    public async Task Bloquea_al_llegar_al_cupo_del_rol_y_desbloquea_el_dia_calendario_siguiente()
    {
        await SembrarAsync();
        await FijarCupoDeRolAsync("secretaria", 2);
        await AnotarTurnosDeHoyAsync(Secretaria, Ancla, llamadas: 1, cantidad: 2);

        var reloj = new RelojFijo(Ancla);
        var cuota = ArmarCuota(reloj);
        var ct = TestContext.Current.CancellationToken;

        Assert.False(await cuota.HayCupoAsync(Secretaria, ct));

        // El día calendario UTC siguiente, no 24hs después: si Ancla fuera
        // 23:50, +24hs seguiría siendo "hoy" en reloj pero otro día calendario.
        reloj.Avanzar(TimeSpan.FromHours(15));
        Assert.True(await cuota.HayCupoAsync(Secretaria, ct));
    }

    [Fact]
    public async Task Cero_llamadas_al_modelo_no_cuenta_contra_el_cupo()
    {
        // design.md D4: un turno que no llamó al modelo no gasta cupo, aunque
        // haya escrito su fila en registro_operativo (carril sin datos).
        await SembrarAsync();
        await FijarCupoDeRolAsync("secretaria", 1);
        await AnotarTurnosDeHoyAsync(Secretaria, Ancla, llamadas: 0, cantidad: 10);

        var cuota = ArmarCuota(new RelojFijo(Ancla));

        Assert.True(await cuota.HayCupoAsync(Secretaria, TestContext.Current.CancellationToken));
    }

    // --------------------------------------------------------- 3.4, override

    [Fact]
    public async Task El_override_del_usuario_gana_aunque_sea_mas_chico_que_el_del_rol()
    {
        await SembrarAsync();
        await FijarCupoDeRolAsync("secretaria", 10);
        await FijarOverrideAsync(Secretaria, 1);
        await AnotarTurnosDeHoyAsync(Secretaria, Ancla, llamadas: 1, cantidad: 1);

        var cuota = ArmarCuota(new RelojFijo(Ancla));

        Assert.False(await cuota.HayCupoAsync(Secretaria, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task El_override_del_usuario_gana_aunque_sea_mas_grande_que_el_del_rol()
    {
        await SembrarAsync();
        await FijarCupoDeRolAsync("secretaria", 1);
        await FijarOverrideAsync(Secretaria, 10);
        await AnotarTurnosDeHoyAsync(Secretaria, Ancla, llamadas: 1, cantidad: 1);

        var cuota = ArmarCuota(new RelojFijo(Ancla));

        Assert.True(await cuota.HayCupoAsync(Secretaria, TestContext.Current.CancellationToken));
    }

    // ------------------------------------------------- 3.5, cero desactiva

    [Fact]
    public async Task Cupo_de_rol_en_cero_nunca_bloquea_sin_necesitar_ninguna_fila()
    {
        // Sin FijarCupoDeRolAsync: el seed de 006 ya deja cada rol en cero.
        await SembrarAsync();
        await AnotarTurnosDeHoyAsync(Secretaria, Ancla, llamadas: 1, cantidad: 500);

        var cuota = ArmarCuota(new RelojFijo(Ancla));
        var ct = TestContext.Current.CancellationToken;

        Assert.True(await cuota.HayCupoAsync(Secretaria, ct));
        Assert.Null(await cuota.CupoVuelveAAsync(Secretaria, ct));
        Assert.Equal(int.MaxValue, await cuota.CupoRestanteAsync(Secretaria, ct));
    }

    // ----------------------------------------------- roles múltiples (§3.2 doc)

    [Fact]
    public async Task Con_varios_roles_activados_manda_el_minimo_no_el_del_rol_sin_tope()
    {
        // Secretaria queda con cupo=3 (activado) y decanato con cupo=0 (sin
        // tope) sobre el MISMO actor: el mínimo entre los ACTIVADOS es 3, no
        // 0 — un rol sin tope no "gana" por ser el número más chico.
        await SembrarAsync();
        await FijarCupoDeRolAsync("secretaria", 3);
        await FijarCupoDeRolAsync("decanato", 0);
        await AgregarRolAsync(Secretaria, "decanato");
        await AnotarTurnosDeHoyAsync(Secretaria, Ancla, llamadas: 1, cantidad: 3);

        var cuota = ArmarCuota(new RelojFijo(Ancla));

        Assert.False(await cuota.HayCupoAsync(Secretaria, TestContext.Current.CancellationToken));
    }

    // ------------------------------------------------ 9.7, aviso de umbral

    [Fact]
    public async Task AnotarAsync_dispara_el_aviso_de_umbral_cuando_corresponde()
    {
        await SembrarAsync();
        await FijarCupoDeRolAsync("secretaria", 2);
        // Ya hay un turno hoy: el segundo, anotado abajo, llega al 100%.
        await AnotarTurnosDeHoyAsync(Secretaria, Ancla, llamadas: 1, cantidad: 1);

        var eventos = new List<string>();
        var log = new LoggerDeEventosDePrueba<CuotaPersistente>(eventos);
        var identidad = new ConsultasIdentity(PostgresFixture.CrearIdentity(Cadena));
        var cuota = new CuotaPersistente(
            new CadenaDuena(Cadena), identidad, new RelojFijo(Ancla), new DetectorDeUmbrales(), log);

        await cuota.AnotarAsync(Secretaria, TestContext.Current.CancellationToken);

        Assert.NotEmpty(eventos);
    }

    private sealed class LoggerDeEventosDePrueba<T>(List<string> eventos) : Microsoft.Extensions.Logging.ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            eventos.Add(formatter(state, exception));
    }

    // ------------------------------------------------------------------ apoyo

    private CuotaPersistente ArmarCuota(RelojFijo reloj)
    {
        var identidad = new ConsultasIdentity(PostgresFixture.CrearIdentity(Cadena));
        return new CuotaPersistente(new CadenaDuena(Cadena), identidad, reloj, new DetectorDeUmbrales(), NullLogger<CuotaPersistente>.Instance);
    }

    private async Task FijarCupoDeRolAsync(string rol, int cupo)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            UPDATE asistente.presupuesto_rol
               SET cupo_diario_turnos = @cupo, actualizado_en = now()
             WHERE rol_code = @rol
            """, conexion);
        comando.Parameters.AddWithValue("cupo", cupo);
        comando.Parameters.AddWithValue("rol", rol);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task FijarOverrideAsync(Guid actor, int cupo)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.presupuesto_usuario (actor_id, cupo_diario_turnos, vigente_desde, vigente_hasta)
            VALUES (@actor, @cupo, now(), NULL)
            """, conexion);
        comando.Parameters.AddWithValue("actor", actor);
        comando.Parameters.AddWithValue("cupo", cupo);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
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

    /// <summary>
    /// Escribe filas mínimas de <c>registro_operativo</c>, como haría un turno
    /// real, para poder contar cupo consumido "hoy".
    /// </summary>
    private async Task AnotarTurnosDeHoyAsync(
        Guid actor, DateTimeOffset momento, int llamadas, int cantidad)
    {
        await using var conexion = await AbrirConexionAsync();

        for (var i = 0; i < cantidad; i++)
        {
            await using var comando = new NpgsqlCommand(
                """
                INSERT INTO asistente.registro_operativo
                    (actor_id, ocurrido_en, carril, estado, llamadas_al_modelo,
                     tokens_de_entrada, tokens_de_salida, latencia_ms, hubo_reintento, truncado)
                VALUES (@actor, @cuando, 'Sql', 'Respondida', @llamadas, 0, 0, 1, false, false)
                """, conexion);
            comando.Parameters.AddWithValue("actor", actor);
            comando.Parameters.AddWithValue("cuando", momento);
            comando.Parameters.AddWithValue("llamadas", llamadas);
            await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
    }
}
