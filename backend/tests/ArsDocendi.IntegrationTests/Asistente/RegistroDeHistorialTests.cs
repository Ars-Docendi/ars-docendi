using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El escritor del historial propio, contra la base real.
/// </summary>
public sealed class RegistroDeHistorialTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_registro_historial")
{
    private static readonly Guid Alguien = Guid.Parse("a0000000-0000-4000-8000-000000000009");
    private static readonly DateTimeOffset Ancla = new(2027, 4, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Un_turno_respondido_produce_un_hilo_y_un_turno_con_la_sql()
    {
        var conversacion = new HiloConversacional(Guid.NewGuid(), Alguien);

        await Registro().RegistrarTurnoAsync(
            conversacion,
            new TurnoParaHistorial(
                Alguien,
                "¿cuántos docentes hay?",
                "SELECT count(*) FROM identity.personas",
                EstadoDelTurno.Respondida,
                Ancla),
            TestContext.Current.CancellationToken);

        Assert.Equal(1L, await EscalarAsync<long>("SELECT count(*) FROM asistente.hilo_historico"));
        Assert.Equal(1L, await EscalarAsync<long>("SELECT count(*) FROM asistente.turno_historico"));

        Assert.Equal(
            "SELECT count(*) FROM identity.personas",
            await EscalarAsync<string>("SELECT sql_resuelto FROM asistente.turno_historico"));

        Assert.Equal(
            "¿cuántos docentes hay?",
            await EscalarAsync<string>("SELECT titulo FROM asistente.hilo_historico"));
    }

    [Fact]
    public async Task Fija_HiloHistorico_en_la_conversacion_efimera()
    {
        var conversacion = new HiloConversacional(Guid.NewGuid(), Alguien);
        Assert.Null(conversacion.HiloHistorico);

        await Registro().RegistrarTurnoAsync(
            conversacion,
            new TurnoParaHistorial(Alguien, "¿y esto?", null, EstadoDelTurno.Respondida, Ancla),
            TestContext.Current.CancellationToken);

        Assert.NotNull(conversacion.HiloHistorico);

        var idEnLaBase = await EscalarAsync<Guid>("SELECT id FROM asistente.hilo_historico");
        Assert.Equal(idEnLaBase, conversacion.HiloHistorico);
    }

    [Fact]
    public async Task Un_segundo_turno_del_mismo_hilo_extiende_la_misma_conversacion()
    {
        var conversacion = new HiloConversacional(Guid.NewGuid(), Alguien);
        var registro = Registro();
        var ct = TestContext.Current.CancellationToken;

        await registro.RegistrarTurnoAsync(
            conversacion,
            new TurnoParaHistorial(Alguien, "primera", "SELECT 1", EstadoDelTurno.Respondida, Ancla),
            ct);

        var primerHiloId = conversacion.HiloHistorico;

        await registro.RegistrarTurnoAsync(
            conversacion,
            new TurnoParaHistorial(
                Alguien, "segunda", "SELECT 2", EstadoDelTurno.Respondida, Ancla.AddMinutes(3)),
            ct);

        Assert.Equal(primerHiloId, conversacion.HiloHistorico);
        Assert.Equal(1L, await EscalarAsync<long>("SELECT count(*) FROM asistente.hilo_historico"));
        Assert.Equal(2L, await EscalarAsync<long>("SELECT count(*) FROM asistente.turno_historico"));

        // El título quedó fijado por la PRIMERA pregunta, no la segunda.
        Assert.Equal("primera", await EscalarAsync<string>("SELECT titulo FROM asistente.hilo_historico"));

        // ultima_actividad se movió al segundo turno.
        Assert.Equal(
            Ancla.AddMinutes(3),
            await EscalarAsync<DateTime>("SELECT ultima_actividad FROM asistente.hilo_historico"),
            TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Un_turno_sin_sql_persiste_sql_resuelto_nulo()
    {
        var conversacion = new HiloConversacional(Guid.NewGuid(), Alguien);

        await Registro().RegistrarTurnoAsync(
            conversacion,
            new TurnoParaHistorial(
                Alguien, "no sé qué preguntar", null, EstadoDelTurno.NoContestable, Ancla),
            TestContext.Current.CancellationToken);

        await using var conexion = await AbrirConexionAsync();
        await using var comando = new Npgsql.NpgsqlCommand(
            "SELECT sql_resuelto FROM asistente.turno_historico", conexion);

        Assert.Equal(DBNull.Value, await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    private RegistroDeHistorial Registro() =>
        new(new CadenaDuena(Cadena), NullLogger<RegistroDeHistorial>.Instance);
}
