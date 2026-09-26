using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Logging;
using Modules.Asistente.Application;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Escribe el historial propio de conversaciones (RF de
/// asistente-historial-conversaciones).
/// </summary>
/// <remarks>
/// Pide <see cref="CadenaDuena"/> y no una de solo lectura, por el mismo
/// motivo que <see cref="RegistroDelTurno"/>: <c>asistente.hilo_historico</c>
/// y <c>asistente.turno_historico</c> viven en el schema propio del asistente,
/// que los dos roles de solo lectura tienen revocado entero.
/// </remarks>
internal sealed class RegistroDeHistorial(CadenaDuena cadena, ILogger<RegistroDeHistorial> log)
    : IRegistroDeHistorial
{
    public async Task RegistrarTurnoAsync(
        HiloConversacional conversacion, TurnoParaHistorial turno, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(conversacion);
        ArgumentNullException.ThrowIfNull(turno);

        try
        {
            await using var conexion = new NpgsqlConnection(cadena.Valor);
            await conexion.OpenAsync(ct);

            // SI LA CONVERSACIÓN EXISTENTE ESTÁ PENDIENTE DE BORRADO, SE MINTEA
            // UNA NUEVA (design.md D4 de asistente-rediseno-v3) en vez de
            // reusarla: el UPDATE de abajo trae su propio filtro
            // `borrado_pendiente_desde IS NULL` y no afecta ninguna fila
            // cuando está pendiente, así que `tocada` sale en falso y cae al
            // mismo camino que un hilo que todavía no escribió nada. Nunca
            // hay que decidirlo con una consulta aparte: el mismo UPDATE que
            // toca la conversación es el que confirma que se puede tocar.
            var hiloHistoricoId = Guid.Empty;
            var tocada = false;

            if (conversacion.HiloHistorico is { } existente)
            {
                tocada = await TocarConversacionAsync(conexion, existente, turno.OcurrioEn, ct);
                hiloHistoricoId = existente;
            }

            if (!tocada)
            {
                hiloHistoricoId = await MintarConversacionAsync(conexion, turno, ct);
            }

            await InsertarTurnoAsync(conexion, hiloHistoricoId, turno, ct);

            // Se fija al final y no al mintar: si InsertarTurnoAsync fallara
            // después de mintar la conversación, el hilo efímero no queda
            // apuntando a una conversación que se abrió pero nunca recibió su
            // primer turno. La próxima llamada minteará una nueva en vez de
            // heredar una conversación vacía — el peor caso es una fila
            // huérfana sin turnos, no una referencia rota.
            conversacion.HiloHistorico = hiloHistoricoId;
        }
        catch (Exception excepcion) when (excepcion is NpgsqlException or InvalidOperationException)
        {
            // Se traga el fallo a propósito, igual que RegistroDelTurno: el
            // turno ya se resolvió y el usuario ya tiene su respuesta. Lo que
            // se pierde es que esta conversación no quede en su historial, no
            // el servicio entero.
            log.LogError(
                excepcion, "No se pudo escribir el historial de conversaciones del asistente.");
        }
    }

    public async Task ReemplazarUltimoTurnoAsync(
        HiloConversacional conversacion, TurnoParaHistorial turno, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(conversacion);
        ArgumentNullException.ThrowIfNull(turno);

        if (conversacion.HiloHistorico is not { } hiloHistoricoId)
        {
            // La fila vieja nunca llegó a persistirse (la escritura del turno
            // reemplazado falló y se tragó su excepción, como cualquier otra):
            // no hay nada que borrar, así que esto es un turno nuevo cualquiera.
            await RegistrarTurnoAsync(conversacion, turno, ct);
            return;
        }

        try
        {
            await using var conexion = new NpgsqlConnection(cadena.Valor);
            await conexion.OpenAsync(ct);

            // UNA SOLA TRANSACCIÓN (design.md D9, punto 3): si el INSERT de
            // abajo fallara después del DELETE, un commit parcial dejaría la
            // conversación sin su último turno. Con la transacción, un fallo en
            // cualquiera de las tres operaciones deja la fila vieja intacta.
            await using var transaccion = await conexion.BeginTransactionAsync(ct);

            await EliminarUltimoTurnoAsync(conexion, transaccion, hiloHistoricoId, ct);

            var tocada = await TocarConversacionAsync(
                conexion, hiloHistoricoId, turno.OcurrioEn, ct, transaccion);

            if (!tocada)
            {
                // La conversación quedó pendiente de borrado justo entre la
                // resolución del turno y esta escritura: mismo camino que
                // RegistrarTurnoAsync (design.md D4), pero DENTRO de esta
                // transacción, para no dejar el DELETE de arriba sin un INSERT
                // que lo acompañe.
                hiloHistoricoId = await MintarConversacionAsync(conexion, turno, ct, transaccion);
            }

            await InsertarTurnoAsync(conexion, hiloHistoricoId, turno, ct, transaccion);

            await transaccion.CommitAsync(ct);

            conversacion.HiloHistorico = hiloHistoricoId;
        }
        catch (Exception excepcion) when (excepcion is NpgsqlException or InvalidOperationException)
        {
            // Se traga el fallo, igual que RegistrarTurnoAsync: el turno ya se
            // resolvió y el usuario ya tiene su respuesta nueva. Lo que se
            // pierde es el reemplazo en el historial, no el servicio entero —
            // y gracias a la transacción, la fila VIEJA sigue ahí.
            log.LogError(
                excepcion,
                "No se pudo reemplazar el turno del historial de conversaciones del asistente.");
        }
    }

    /// <summary>Borra la fila más reciente de <c>turno_historico</c> de este hilo.</summary>
    private static async Task EliminarUltimoTurnoAsync(
        NpgsqlConnection conexion, NpgsqlTransaction transaccion, Guid hiloHistoricoId, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(
            """
            DELETE FROM asistente.turno_historico
             WHERE id = (
                 SELECT id FROM asistente.turno_historico
                  WHERE hilo_id = @hilo
                  ORDER BY ocurrido_en DESC
                  LIMIT 1
             )
            """, conexion, transaccion);

        comando.Parameters.AddWithValue("hilo", hiloHistoricoId);

        await comando.ExecuteNonQueryAsync(ct);
    }

    private static async Task<Guid> MintarConversacionAsync(
        NpgsqlConnection conexion, TurnoParaHistorial turno, CancellationToken ct, NpgsqlTransaction? transaccion = null)
    {
        var id = Guid.NewGuid();
        var titulo = TituloDeConversacion.Derivar(turno.Pregunta);

        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.hilo_historico (id, actor_id, titulo, creado_en, ultima_actividad)
            VALUES (@id, @actor, @titulo, @ahora, @ahora)
            """, conexion, transaccion);

        comando.Parameters.AddWithValue("id", id);
        comando.Parameters.AddWithValue("actor", turno.Actor);
        comando.Parameters.AddWithValue("titulo", titulo);
        comando.Parameters.AddWithValue("ahora", turno.OcurrioEn);

        await comando.ExecuteNonQueryAsync(ct);

        return id;
    }

    /// <summary>
    /// Toca <c>ultima_actividad</c> y desarchiva (design.md D3: una nueva
    /// actividad desarchiva) — pero SÓLO si la conversación no está pendiente
    /// de borrado. Devuelve si efectivamente la tocó: en falso, el llamador
    /// mintea una conversación nueva en vez de escribir en una pendiente
    /// (design.md D4). Nunca toca el título (design.md D9).
    /// </summary>
    private static async Task<bool> TocarConversacionAsync(
        NpgsqlConnection conexion,
        Guid hiloHistoricoId,
        DateTimeOffset ahora,
        CancellationToken ct,
        NpgsqlTransaction? transaccion = null)
    {
        await using var comando = new NpgsqlCommand(
            """
            UPDATE asistente.hilo_historico
               SET ultima_actividad = @ahora, archivada_en = NULL
             WHERE id = @id AND borrado_pendiente_desde IS NULL
            """, conexion, transaccion);

        comando.Parameters.AddWithValue("ahora", ahora);
        comando.Parameters.AddWithValue("id", hiloHistoricoId);

        return await comando.ExecuteNonQueryAsync(ct) > 0;
    }

    private static async Task InsertarTurnoAsync(
        NpgsqlConnection conexion,
        Guid hiloHistoricoId,
        TurnoParaHistorial turno,
        CancellationToken ct,
        NpgsqlTransaction? transaccion = null)
    {
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.turno_historico
                (id, hilo_id, pregunta, sql_resuelto, estado, ocurrido_en, referencias)
            VALUES (@id, @hilo, @pregunta, @sql, @estado, @ahora, @referencias)
            """, conexion, transaccion);

        comando.Parameters.AddWithValue("id", Guid.NewGuid());
        comando.Parameters.AddWithValue("hilo", hiloHistoricoId);
        comando.Parameters.AddWithValue("pregunta", turno.Pregunta);
        comando.Parameters.AddWithValue(
            "sql", NpgsqlTypes.NpgsqlDbType.Text, (object?)turno.SqlResuelto ?? DBNull.Value);
        comando.Parameters.AddWithValue("estado", turno.Estado.ToString());
        comando.Parameters.AddWithValue("ahora", turno.OcurrioEn);
        comando.Parameters.AddWithValue(
            "referencias", NpgsqlTypes.NpgsqlDbType.Jsonb,
            (object?)SerializacionDeReferencias.Serializar(turno.Referencias) ?? DBNull.Value);

        await comando.ExecuteNonQueryAsync(ct);
    }
}
