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

            var yaExistia = conversacion.HiloHistorico is { } existente;

            var hiloHistoricoId = yaExistia
                ? conversacion.HiloHistorico!.Value
                : await MintarConversacionAsync(conexion, turno, ct);

            if (yaExistia)
            {
                await TocarConversacionAsync(conexion, hiloHistoricoId, turno.OcurrioEn, ct);
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

    private static async Task<Guid> MintarConversacionAsync(
        NpgsqlConnection conexion, TurnoParaHistorial turno, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var titulo = TituloDeConversacion.Derivar(turno.Pregunta);

        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.hilo_historico (id, actor_id, titulo, creado_en, ultima_actividad)
            VALUES (@id, @actor, @titulo, @ahora, @ahora)
            """, conexion);

        comando.Parameters.AddWithValue("id", id);
        comando.Parameters.AddWithValue("actor", turno.Actor);
        comando.Parameters.AddWithValue("titulo", titulo);
        comando.Parameters.AddWithValue("ahora", turno.OcurrioEn);

        await comando.ExecuteNonQueryAsync(ct);

        return id;
    }

    private static async Task TocarConversacionAsync(
        NpgsqlConnection conexion, Guid hiloHistoricoId, DateTimeOffset ahora, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(
            "UPDATE asistente.hilo_historico SET ultima_actividad = @ahora WHERE id = @id",
            conexion);

        comando.Parameters.AddWithValue("ahora", ahora);
        comando.Parameters.AddWithValue("id", hiloHistoricoId);

        await comando.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertarTurnoAsync(
        NpgsqlConnection conexion, Guid hiloHistoricoId, TurnoParaHistorial turno, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.turno_historico (id, hilo_id, pregunta, sql_resuelto, estado, ocurrido_en)
            VALUES (@id, @hilo, @pregunta, @sql, @estado, @ahora)
            """, conexion);

        comando.Parameters.AddWithValue("id", Guid.NewGuid());
        comando.Parameters.AddWithValue("hilo", hiloHistoricoId);
        comando.Parameters.AddWithValue("pregunta", turno.Pregunta);
        comando.Parameters.AddWithValue(
            "sql", NpgsqlTypes.NpgsqlDbType.Text, (object?)turno.SqlResuelto ?? DBNull.Value);
        comando.Parameters.AddWithValue("estado", turno.Estado.ToString());
        comando.Parameters.AddWithValue("ahora", turno.OcurrioEn);

        await comando.ExecuteNonQueryAsync(ct);
    }
}
