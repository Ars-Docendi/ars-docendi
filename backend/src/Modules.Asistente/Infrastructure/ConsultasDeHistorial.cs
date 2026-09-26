using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Options;
using Modules.Asistente.Application;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// El lado de lectura propia del historial (asistente-historial-conversaciones §6-8).
/// </summary>
/// <remarks>
/// Pide <see cref="CadenaDuena"/> y no una de solo lectura: <c>asistente.hilo_historico</c>
/// y <c>asistente.turno_historico</c> viven en el schema propio del asistente,
/// que los dos roles de solo lectura tienen revocado entero — leerlas necesita
/// la misma conexión que escribirlas.
///
/// <b>Toda consulta filtra <c>borrado_pendiente_desde IS NULL</c></b>
/// (asistente-rediseno-v3, design.md D4): una conversación pendiente de
/// borrado no existe para su propio dueño ni por esta interfaz, aunque su
/// ventana de «Deshacer» todavía no haya vencido — eso lo ve únicamente
/// <c>IConsultasDeAuditoriaDeSoporte</c>, marcada.
/// </remarks>
internal sealed class ConsultasDeHistorial(
    CadenaDuena cadena, TimeProvider reloj, IOptions<OpcionesAsistente> opciones)
    : IConsultasDeHistorial
{
    public async Task<IReadOnlyList<ConversacionResumen>> ListarAsync(
        Guid actor, string? busqueda, CancellationToken ct)
    {
        await using var conexion = await AbrirAsync(ct);

        var conBusqueda = !string.IsNullOrWhiteSpace(busqueda);

        await using var comando = new NpgsqlCommand(
            conBusqueda
                ? """
                  SELECT DISTINCT h.id, h.titulo, h.creado_en, h.ultima_actividad, h.archivada_en
                    FROM asistente.hilo_historico h
                    JOIN asistente.turno_historico t ON t.hilo_id = h.id
                   WHERE h.actor_id = @actor
                     AND h.borrado_pendiente_desde IS NULL
                     AND to_tsvector('spanish', t.pregunta) @@ plainto_tsquery('spanish', @busqueda)
                   ORDER BY h.ultima_actividad DESC
                  """
                : """
                  SELECT id, titulo, creado_en, ultima_actividad, archivada_en
                    FROM asistente.hilo_historico
                   WHERE actor_id = @actor
                     AND borrado_pendiente_desde IS NULL
                   ORDER BY ultima_actividad DESC
                  """,
            conexion);

        comando.Parameters.AddWithValue("actor", actor);
        if (conBusqueda)
        {
            comando.Parameters.AddWithValue("busqueda", busqueda!);
        }

        var resultado = new List<ConversacionResumen>();
        await using var lector = await comando.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
        {
            resultado.Add(new ConversacionResumen(
                lector.GetGuid(0), lector.GetString(1), lector.GetFieldValue<DateTimeOffset>(2),
                lector.GetFieldValue<DateTimeOffset>(3), Archivada: !lector.IsDBNull(4)));
        }

        return resultado;
    }

    public async Task<bool> ArchivarAsync(Guid actor, Guid hiloId, CancellationToken ct) =>
        await MarcarArchivadaAsync(actor, hiloId, archivar: true, ct);

    public async Task<bool> DesarchivarAsync(Guid actor, Guid hiloId, CancellationToken ct) =>
        await MarcarArchivadaAsync(actor, hiloId, archivar: false, ct);

    public async Task<Guid?> EliminarAsync(Guid actor, Guid hiloId, CancellationToken ct)
    {
        var lote = Guid.NewGuid();
        var ahora = reloj.GetUtcNow();

        await using var conexion = await AbrirAsync(ct);
        await using var comando = new NpgsqlCommand(
            """
            UPDATE asistente.hilo_historico
               SET borrado_pendiente_desde = @ahora, lote_de_borrado = @lote
             WHERE id = @hilo AND actor_id = @actor AND borrado_pendiente_desde IS NULL
            """, conexion);

        comando.Parameters.AddWithValue("ahora", ahora);
        comando.Parameters.AddWithValue("lote", lote);
        comando.Parameters.AddWithValue("hilo", hiloId);
        comando.Parameters.AddWithValue("actor", actor);

        var afectadas = await comando.ExecuteNonQueryAsync(ct);
        return afectadas > 0 ? lote : null;
    }

    public async Task<Guid> EliminarTodoAsync(Guid actor, CancellationToken ct)
    {
        var lote = Guid.NewGuid();
        var ahora = reloj.GetUtcNow();

        await using var conexion = await AbrirAsync(ct);
        await using var comando = new NpgsqlCommand(
            """
            UPDATE asistente.hilo_historico
               SET borrado_pendiente_desde = @ahora, lote_de_borrado = @lote
             WHERE actor_id = @actor AND borrado_pendiente_desde IS NULL
            """, conexion);

        comando.Parameters.AddWithValue("ahora", ahora);
        comando.Parameters.AddWithValue("lote", lote);
        comando.Parameters.AddWithValue("actor", actor);

        await comando.ExecuteNonQueryAsync(ct);
        return lote;
    }

    public async Task<bool> DeshacerBorradoAsync(Guid actor, Guid lote, CancellationToken ct)
    {
        var corte = VentanaDesde(reloj.GetUtcNow());

        await using var conexion = await AbrirAsync(ct);
        await using var comando = new NpgsqlCommand(
            """
            UPDATE asistente.hilo_historico
               SET borrado_pendiente_desde = NULL, lote_de_borrado = NULL
             WHERE actor_id = @actor AND lote_de_borrado = @lote
               AND borrado_pendiente_desde > @corte
            """, conexion);

        comando.Parameters.AddWithValue("actor", actor);
        comando.Parameters.AddWithValue("lote", lote);
        comando.Parameters.AddWithValue("corte", corte);

        return await comando.ExecuteNonQueryAsync(ct) > 0;
    }

    private async Task<bool> MarcarArchivadaAsync(
        Guid actor, Guid hiloId, bool archivar, CancellationToken ct)
    {
        await using var conexion = await AbrirAsync(ct);
        await using var comando = new NpgsqlCommand(
            """
            UPDATE asistente.hilo_historico
               SET archivada_en = @archivadaEn
             WHERE id = @hilo AND actor_id = @actor AND borrado_pendiente_desde IS NULL
            """, conexion);

        comando.Parameters.AddWithValue(
            "archivadaEn", NpgsqlTypes.NpgsqlDbType.TimestampTz,
            archivar ? (object)reloj.GetUtcNow() : DBNull.Value);
        comando.Parameters.AddWithValue("hilo", hiloId);
        comando.Parameters.AddWithValue("actor", actor);

        return await comando.ExecuteNonQueryAsync(ct) > 0;
    }

    /// <summary>El corte a partir del cual un borrado sigue dentro de su ventana.</summary>
    private DateTimeOffset VentanaDesde(DateTimeOffset ahora) =>
        ahora - TimeSpan.FromSeconds(opciones.Value.VentanaDeDeshacerSegundos);

    public async Task<ConversacionDetalle?> ObtenerAsync(Guid actor, Guid hiloId, CancellationToken ct)
    {
        await using var conexion = await AbrirAsync(ct);

        var cabecera = await LeerCabeceraAsync(conexion, actor, hiloId, ct);
        if (cabecera is null)
        {
            return null;
        }

        var turnos = await LeerTurnosAsync(conexion, hiloId, ct);

        return new ConversacionDetalle(
            cabecera.Value.Id, cabecera.Value.Titulo, cabecera.Value.CreadoEn,
            cabecera.Value.UltimaActividad, turnos);
    }

    public async Task<bool> RenombrarAsync(
        Guid actor, Guid hiloId, string nuevoTitulo, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nuevoTitulo);

        await using var conexion = await AbrirAsync(ct);
        await using var comando = new NpgsqlCommand(
            """
            UPDATE asistente.hilo_historico
               SET titulo = @titulo
             WHERE id = @hilo AND actor_id = @actor AND borrado_pendiente_desde IS NULL
            """, conexion);

        comando.Parameters.AddWithValue("titulo", nuevoTitulo.Trim());
        comando.Parameters.AddWithValue("hilo", hiloId);
        comando.Parameters.AddWithValue("actor", actor);

        return await comando.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<IReadOnlyList<TurnoDeHistorial>?> ObtenerTurnosAsync(
        Guid actor, Guid hiloId, CancellationToken ct)
    {
        await using var conexion = await AbrirAsync(ct);

        var cabecera = await LeerCabeceraAsync(conexion, actor, hiloId, ct);
        return cabecera is null ? null : await LeerTurnosAsync(conexion, hiloId, ct);
    }

    public async Task<TurnoParaReejecutar?> ObtenerTurnoParaReejecutarAsync(
        Guid actor, Guid turnoId, CancellationToken ct)
    {
        await using var conexion = await AbrirAsync(ct);
        await using var comando = new NpgsqlCommand(
            """
            SELECT t.estado, t.sql_resuelto
              FROM asistente.turno_historico t
              JOIN asistente.hilo_historico h ON h.id = t.hilo_id
             WHERE t.id = @turno AND h.actor_id = @actor AND h.borrado_pendiente_desde IS NULL
            """, conexion);

        comando.Parameters.AddWithValue("turno", turnoId);
        comando.Parameters.AddWithValue("actor", actor);

        await using var lector = await comando.ExecuteReaderAsync(ct);
        if (!await lector.ReadAsync(ct))
        {
            return null;
        }

        var estado = Enum.Parse<EstadoDelTurno>(lector.GetString(0));
        var sql = lector.IsDBNull(1) ? null : lector.GetString(1);

        return new TurnoParaReejecutar(estado, sql);
    }

    // ------------------------------------------------------------------ apoyo

    private static async Task<(Guid Id, string Titulo, DateTimeOffset CreadoEn, DateTimeOffset UltimaActividad)?>
        LeerCabeceraAsync(NpgsqlConnection conexion, Guid actor, Guid hiloId, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(
            """
            SELECT id, titulo, creado_en, ultima_actividad
              FROM asistente.hilo_historico
             WHERE id = @hilo AND actor_id = @actor AND borrado_pendiente_desde IS NULL
            """, conexion);

        comando.Parameters.AddWithValue("hilo", hiloId);
        comando.Parameters.AddWithValue("actor", actor);

        await using var lector = await comando.ExecuteReaderAsync(ct);
        if (!await lector.ReadAsync(ct))
        {
            return null;
        }

        return (lector.GetGuid(0), lector.GetString(1), lector.GetFieldValue<DateTimeOffset>(2),
            lector.GetFieldValue<DateTimeOffset>(3));
    }

    private static async Task<IReadOnlyList<TurnoDeHistorial>> LeerTurnosAsync(
        NpgsqlConnection conexion, Guid hiloId, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(
            """
            SELECT id, pregunta, sql_resuelto, estado, ocurrido_en
              FROM asistente.turno_historico
             WHERE hilo_id = @hilo
             ORDER BY ocurrido_en
            """, conexion);

        comando.Parameters.AddWithValue("hilo", hiloId);

        var turnos = new List<TurnoDeHistorial>();
        await using var lector = await comando.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
        {
            turnos.Add(new TurnoDeHistorial(
                lector.GetGuid(0),
                lector.GetString(1),
                lector.IsDBNull(2) ? null : lector.GetString(2),
                Enum.Parse<EstadoDelTurno>(lector.GetString(3)),
                lector.GetFieldValue<DateTimeOffset>(4)));
        }

        return turnos;
    }

    private async Task<NpgsqlConnection> AbrirAsync(CancellationToken ct)
    {
        var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);
        return conexion;
    }
}
