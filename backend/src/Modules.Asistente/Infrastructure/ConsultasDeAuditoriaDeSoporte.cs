using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Logging;
using Modules.Asistente.Application;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// El lado de soporte del historial ajeno (asistente-acceso-de-soporte-al-historial).
/// </summary>
/// <remarks>
/// Pide <see cref="CadenaDuena"/> por el mismo motivo que <see cref="ConsultasDeHistorial"/>:
/// las tres tablas del historial viven en el schema propio del asistente, que
/// los dos roles de solo lectura tienen revocado entero.
///
/// El log de acceso NUNCA nombra la pregunta ni la SQL junto al sujeto —sólo
/// que hubo una lectura, quién la hizo y a quién—, extendiendo la disciplina
/// de <c>asistente-feedback-export-seguimiento</c> D3: la fila de auditoría ya
/// es el registro completo del qué y el por qué; el log es sólo para
/// observabilidad operativa.
/// </remarks>
internal sealed class ConsultasDeAuditoriaDeSoporte(
    CadenaDuena cadena, ILogger<ConsultasDeAuditoriaDeSoporte> log)
    : IConsultasDeAuditoriaDeSoporte
{
    public async Task<IReadOnlyList<ConversacionResumen>> ListarAsync(
        Guid lector, Guid sujeto, string razon, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(razon);

        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        // LA AUDITORÍA VA PRIMERO, Y SIN CAPTURAR SU EXCEPCIÓN. Si esta
        // escritura falla, el método entero falla: no hay ninguna rama que
        // devuelva datos sin haber auditado (design.md D10).
        await AuditarAsync(conexion, lector, sujeto, hiloId: null, razon, ct);

        log.LogInformation("Listado de soporte del historial de un actor.");

        await using var comando = new NpgsqlCommand(
            """
            SELECT id, titulo, creado_en, ultima_actividad
              FROM asistente.hilo_historico
             WHERE actor_id = @sujeto
             ORDER BY ultima_actividad DESC
            """, conexion);

        comando.Parameters.AddWithValue("sujeto", sujeto);

        var resultado = new List<ConversacionResumen>();
        await using var lector_ = await comando.ExecuteReaderAsync(ct);
        while (await lector_.ReadAsync(ct))
        {
            resultado.Add(new ConversacionResumen(
                lector_.GetGuid(0), lector_.GetString(1), lector_.GetFieldValue<DateTimeOffset>(2),
                lector_.GetFieldValue<DateTimeOffset>(3)));
        }

        return resultado;
    }

    public async Task<ConversacionDetalle?> LeerAsync(
        Guid lector, Guid sujeto, Guid hiloId, string razon, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(razon);

        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        // Mismo criterio que ListarAsync: se audita ANTES de leer, y una
        // auditoría que falla hace fallar la lectura entera.
        await AuditarAsync(conexion, lector, sujeto, hiloId, razon, ct);

        log.LogInformation("Lectura de soporte de una conversación de un actor.");

        var cabecera = await LeerCabeceraAsync(conexion, sujeto, hiloId, ct);
        if (cabecera is null)
        {
            return null;
        }

        var turnos = await LeerTurnosAsync(conexion, hiloId, ct);

        // La SQL viaja SIEMPRE acá — sin un segundo gate de
        // `asistente.ver_consulta` (design.md D9): el permiso de soporte YA
        // ES el permiso de diagnóstico, y exigir dos permisos para ver la SQL
        // no añadiría un límite real — quien puede leer el historial entero
        // de otra persona ya está confiado con más que la SQL sola.
        return new ConversacionDetalle(
            cabecera.Value.Id, cabecera.Value.Titulo, cabecera.Value.CreadoEn,
            cabecera.Value.UltimaActividad, turnos);
    }

    // ------------------------------------------------------------------ apoyo

    private static async Task AuditarAsync(
        NpgsqlConnection conexion, Guid lector, Guid sujeto, Guid? hiloId, string razon, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.auditoria_acceso_historial
                (lector_id, sujeto_id, hilo_historico_id, razon, ocurrido_en)
            VALUES (@lector, @sujeto, @hilo, @razon, now())
            """, conexion);

        comando.Parameters.AddWithValue("lector", lector);
        comando.Parameters.AddWithValue("sujeto", sujeto);
        comando.Parameters.AddWithValue(
            "hilo", NpgsqlTypes.NpgsqlDbType.Uuid, (object?)hiloId ?? DBNull.Value);
        comando.Parameters.AddWithValue("razon", razon.Trim());

        await comando.ExecuteNonQueryAsync(ct);
    }

    private static async Task<(Guid Id, string Titulo, DateTimeOffset CreadoEn, DateTimeOffset UltimaActividad)?>
        LeerCabeceraAsync(NpgsqlConnection conexion, Guid sujeto, Guid hiloId, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(
            """
            SELECT id, titulo, creado_en, ultima_actividad
              FROM asistente.hilo_historico
             WHERE id = @hilo AND actor_id = @sujeto
            """, conexion);

        comando.Parameters.AddWithValue("hilo", hiloId);
        comando.Parameters.AddWithValue("sujeto", sujeto);

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
}
