using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Asistente;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// La purga extendida: historial propio (por última actividad de la
/// conversación) y auditoría de soporte (con su propia ventana, independiente).
/// </summary>
/// <remarks>
/// Siembra directamente por SQL, con la conexión dueña, y no a través de
/// <c>IRegistroDeHistorial</c>: lo que se prueba acá es el tercer y cuarto
/// barrido de <see cref="PurgaDeRegistros"/>, no el camino de escritura, que
/// tiene su propia suite. Mismo criterio que <c>RegistrosYPurgaTests</c> usa
/// para los dos registros existentes.
/// </remarks>
public sealed class HistorialYAuditoriaPurgaTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_purga_historial")
{
    private static readonly Guid Alguien = Guid.Parse("a0000000-0000-4000-8000-000000000009");
    private static readonly DateTimeOffset Ancla = new(2027, 3, 10, 12, 0, 0, TimeSpan.Zero);

    // --------------------------------------------------------------- historial

    [Fact]
    public async Task La_purga_borra_conversaciones_por_ultima_actividad_no_por_creacion()
    {
        var reloj = new RelojFijo(Ancla);

        // Creada hace mucho, pero tocada hace poco: no se purga.
        var vigente = await SembrarHiloAsync(
            creadoEn: Ancla.AddDays(-400), ultimaActividad: Ancla.AddDays(-10));

        // Inactiva desde hace más de la ventana: se purga, y su turno cascadea.
        var inactiva = await SembrarHiloAsync(
            creadoEn: Ancla.AddDays(-200), ultimaActividad: Ancla.AddDays(-200));

        await SembrarTurnoAsync(vigente);
        await SembrarTurnoAsync(inactiva);

        var borradas = await Purga(reloj, diasHistorial: 180).PurgarAsync(
            TestContext.Current.CancellationToken);

        // El conteo es el de la sentencia DELETE FROM hilo_historico (1 fila);
        // el turno cascadeado no se cuenta ahí — Npgsql sólo reporta las filas
        // afectadas por ESA sentencia, no las que arrastra la FK.
        Assert.Equal(1, borradas);

        Assert.Equal(1L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.hilo_historico"));
        Assert.Equal(vigente, await EscalarAsync<Guid>(
            "SELECT id FROM asistente.hilo_historico"));
        Assert.Equal(1L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.turno_historico"));
    }

    [Fact]
    public async Task Borrar_el_hilo_cascadea_sus_turnos()
    {
        var reloj = new RelojFijo(Ancla);

        var inactivo = await SembrarHiloAsync(
            creadoEn: Ancla.AddDays(-300), ultimaActividad: Ancla.AddDays(-300));
        await SembrarTurnoAsync(inactivo);
        await SembrarTurnoAsync(inactivo);

        await Purga(reloj, diasHistorial: 180).PurgarAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.turno_historico"));
    }

    [Fact]
    public async Task La_purga_del_historial_es_idempotente()
    {
        var reloj = new RelojFijo(Ancla);
        var purga = Purga(reloj, diasHistorial: 180);
        var ct = TestContext.Current.CancellationToken;

        await SembrarHiloAsync(
            creadoEn: Ancla.AddDays(-300), ultimaActividad: Ancla.AddDays(-300));

        Assert.Equal(1, await purga.PurgarAsync(ct));
        Assert.Equal(0, await purga.PurgarAsync(ct));
    }

    [Fact]
    public async Task Una_conversacion_archivada_sigue_la_misma_retencion_y_se_purga()
    {
        // design.md D3 de asistente-rediseno-v3: archivar no es una segunda
        // política de retención. Vencida su ventana de 180 días igual que
        // cualquier otra, se purga igual — archivada_en no la protege.
        var reloj = new RelojFijo(Ancla);

        var archivadaVieja = await SembrarHiloAsync(
            creadoEn: Ancla.AddDays(-300), ultimaActividad: Ancla.AddDays(-200), archivada: true);

        var borradas = await Purga(reloj, diasHistorial: 180).PurgarAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(1, borradas);
        Assert.Equal(0L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.hilo_historico WHERE id = @id", ("id", archivadaVieja)));
    }

    // ---------------------------------------------------------------- auditoría

    [Fact]
    public async Task La_auditoria_de_soporte_tiene_su_propia_ventana_independiente_del_historial()
    {
        var reloj = new RelojFijo(Ancla);

        // Historial: se purga a los 180 días. Auditoría: sobrevive hasta 365.
        var hilo = await SembrarHiloAsync(
            creadoEn: Ancla.AddDays(-200), ultimaActividad: Ancla.AddDays(-200));

        // Una auditoría vieja (se purga) y una reciente (sobrevive), sobre el
        // MISMO hilo (que a su vez se purga por su propia ventana).
        await SembrarAuditoriaAsync(hilo, Ancla.AddDays(-400));
        await SembrarAuditoriaAsync(hilo, Ancla.AddDays(-30));

        var borradas = await Purga(reloj, diasHistorial: 180, diasAuditoria: 365).PurgarAsync(
            TestContext.Current.CancellationToken);

        // 1 hilo + 1 auditoría vieja = 2. La auditoría reciente y el hilo ya
        // purgado (sin FK, no se ve afectada por la cascada) sobreviven.
        Assert.Equal(2, borradas);
        Assert.Equal(1L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.auditoria_acceso_historial"));
    }

    [Fact]
    public async Task Una_auditoria_sobrevive_a_que_su_conversacion_ya_haya_sido_purgada()
    {
        // EL PUNTO DE D10: sin clave foránea, borrar hilo_historico no toca
        // auditoria_acceso_historial en absoluto — ni por cascada, ni por
        // error de "no existe la referencia".
        var reloj = new RelojFijo(Ancla);

        var hilo = await SembrarHiloAsync(
            creadoEn: Ancla.AddDays(-200), ultimaActividad: Ancla.AddDays(-200));
        await SembrarAuditoriaAsync(hilo, Ancla.AddDays(-5));

        await Purga(reloj, diasHistorial: 180, diasAuditoria: 365).PurgarAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(0L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.hilo_historico"));
        Assert.Equal(1L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.auditoria_acceso_historial WHERE hilo_historico_id = @hilo",
            ("hilo", hilo)));
    }

    // ------------------------------------------------------------------ apoyo

    private PurgaDeRegistros Purga(TimeProvider reloj, int diasHistorial, int diasAuditoria = 365) =>
        new(new CadenaDuena(Cadena),
            Options.Create(new OpcionesAsistente
            {
                RetencionDeHistorialDias = diasHistorial,
                RetencionDeAuditoriaDeSoporteDias = diasAuditoria,
            }),
            reloj,
            NullLogger<PurgaDeRegistros>.Instance);

    private Task<Guid> SembrarHiloAsync(DateTimeOffset creadoEn, DateTimeOffset ultimaActividad) =>
        SembrarHiloAsync(creadoEn, ultimaActividad, archivada: false);

    private async Task<Guid> SembrarHiloAsync(
        DateTimeOffset creadoEn, DateTimeOffset ultimaActividad, bool archivada)
    {
        var id = Guid.NewGuid();

        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.hilo_historico
                (id, actor_id, titulo, creado_en, ultima_actividad, archivada_en)
            VALUES (@id, @actor, 'una conversación', @creado, @actividad, @archivadaEn)
            """, conexion);

        comando.Parameters.AddWithValue("id", id);
        comando.Parameters.AddWithValue("actor", Alguien);
        comando.Parameters.AddWithValue("creado", creadoEn);
        comando.Parameters.AddWithValue("actividad", ultimaActividad);
        comando.Parameters.AddWithValue(
            "archivadaEn", NpgsqlTypes.NpgsqlDbType.TimestampTz,
            archivada ? (object)ultimaActividad : DBNull.Value);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        return id;
    }

    private async Task SembrarTurnoAsync(Guid hiloId)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.turno_historico (id, hilo_id, pregunta, sql_resuelto, estado, ocurrido_en)
            VALUES (@id, @hilo, '¿cuántos docentes hay?', 'SELECT 1', 'Respondida', @ahora)
            """, conexion);

        comando.Parameters.AddWithValue("id", Guid.NewGuid());
        comando.Parameters.AddWithValue("hilo", hiloId);
        comando.Parameters.AddWithValue("ahora", Ancla);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task SembrarAuditoriaAsync(Guid hiloId, DateTimeOffset ocurrioEn)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.auditoria_acceso_historial
                (lector_id, sujeto_id, hilo_historico_id, razon, ocurrido_en)
            VALUES (@lector, @sujeto, @hilo, 'reclamo de soporte', @ahora)
            """, conexion);

        comando.Parameters.AddWithValue("lector", Guid.NewGuid());
        comando.Parameters.AddWithValue("sujeto", Alguien);
        comando.Parameters.AddWithValue("hilo", hiloId);
        comando.Parameters.AddWithValue("ahora", ocurrioEn);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
