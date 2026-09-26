using ArsDocendi.Shared.Persistencia;
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
/// </remarks>
internal sealed class ConsultasDeHistorial(CadenaDuena cadena) : IConsultasDeHistorial
{
    public async Task<IReadOnlyList<ConversacionResumen>> ListarAsync(
        Guid actor, string? busqueda, CancellationToken ct)
    {
        await using var conexion = await AbrirAsync(ct);

        var conBusqueda = !string.IsNullOrWhiteSpace(busqueda);

        await using var comando = new NpgsqlCommand(
            conBusqueda
                ? """
                  SELECT DISTINCT h.id, h.titulo, h.creado_en, h.ultima_actividad
                    FROM asistente.hilo_historico h
                    JOIN asistente.turno_historico t ON t.hilo_id = h.id
                   WHERE h.actor_id = @actor
                     AND to_tsvector('spanish', t.pregunta) @@ plainto_tsquery('spanish', @busqueda)
                   ORDER BY h.ultima_actividad DESC
                  """
                : """
                  SELECT id, titulo, creado_en, ultima_actividad
                    FROM asistente.hilo_historico
                   WHERE actor_id = @actor
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
                lector.GetFieldValue<DateTimeOffset>(3)));
        }

        return resultado;
    }

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
            "UPDATE asistente.hilo_historico SET titulo = @titulo WHERE id = @hilo AND actor_id = @actor",
            conexion);

        comando.Parameters.AddWithValue("titulo", nuevoTitulo.Trim());
        comando.Parameters.AddWithValue("hilo", hiloId);
        comando.Parameters.AddWithValue("actor", actor);

        return await comando.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> EliminarAsync(Guid actor, Guid hiloId, CancellationToken ct)
    {
        await using var conexion = await AbrirAsync(ct);
        await using var comando = new NpgsqlCommand(
            "DELETE FROM asistente.hilo_historico WHERE id = @hilo AND actor_id = @actor", conexion);

        comando.Parameters.AddWithValue("hilo", hiloId);
        comando.Parameters.AddWithValue("actor", actor);

        return await comando.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<int> EliminarTodoAsync(Guid actor, CancellationToken ct)
    {
        await using var conexion = await AbrirAsync(ct);
        await using var comando = new NpgsqlCommand(
            "DELETE FROM asistente.hilo_historico WHERE actor_id = @actor", conexion);

        comando.Parameters.AddWithValue("actor", actor);

        return await comando.ExecuteNonQueryAsync(ct);
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
             WHERE t.id = @turno AND h.actor_id = @actor
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
             WHERE id = @hilo AND actor_id = @actor
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
