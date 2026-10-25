using ArsDocendi.Shared.Persistencia;
using Modules.Asistente.Application;
using Npgsql;
using NpgsqlTypes;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// <see cref="IConsultasDeProblemasDeCalidad"/>: the anonymous list of problem answers
/// (design D5 and D8 of asistente-metricas-de-calidad).
/// </summary>
/// <remarks>
/// Same confinement as <see cref="ConsultasDeCalidad"/>, enforced by the same architecture
/// rule: the analytic record and the vote table, no operational record, no actor, no vote
/// free text, no vote timestamp. A row carries the question text, the reason or cause, the
/// type, the model, the day and the state, which is everything the analytic record knows.
///
/// A turn with several problems is one row. Its type is the one asked for, or, with no
/// filter, the first of negative, failure, replacement. Policy denials and an empty result
/// are not problems and never listed. The audit of the read is written by
/// <see cref="AuditoriaDeRevisionDeCalidad"/>, not here, so this file never needs to name
/// who is reading.
/// </remarks>
internal sealed class ConsultasDeProblemasDeCalidad(
    CadenaDuena cadena,
    TimeProvider reloj) : IConsultasDeProblemasDeCalidad
{
    private const string Base =
        """
        WITH t AS (
            SELECT a.id, a.dia, a.modelo, a.estado, a.causa, a.pregunta, a.reemplazado, r.voto, r.razones,
                   COALESCE(r.voto = false, false) AS negativa,
                   COALESCE(
                       a.causa = ANY(@fallos)
                       OR (a.causa IS NULL AND a.estado IN ('ServicioDegradado', 'Fallo')),
                       false) AS fallo
              FROM asistente.registro_analitico a
              LEFT JOIN asistente.retroalimentacion_turno r ON r.analitico_id = a.id
             WHERE a.dia >= @desde AND a.dia <= @hasta
               AND (@modelo::text IS NULL OR a.modelo = @modelo)
               AND (a.causa IS NULL OR NOT (a.causa = ANY(@politica)))
        ),
        p AS (
            SELECT *
              FROM t
             WHERE CASE @tipo::text
                       WHEN 'negativa' THEN negativa
                       WHEN 'fallo' THEN fallo
                       WHEN 'reemplazo' THEN reemplazado
                       ELSE negativa OR fallo OR reemplazado
                   END
        )
        """;

    public async Task<PaginaDeProblemas> ListarAsync(
        int periodoDias, string? modelo, string? tipo, int pagina, CancellationToken ct)
    {
        if (!ParametrosDeCalidad.Periodos.Contains(periodoDias))
        {
            throw new ArgumentOutOfRangeException(nameof(periodoDias), periodoDias, "Unsupported period.");
        }

        if (tipo is not null && !ParametrosDeCalidad.Tipos.Contains(tipo))
        {
            throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Unsupported problem type.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(pagina, 1);

        var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);
        var desde = hoy.AddDays(-(periodoDias - 1));

        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        int total;
        await using (var conteo = Preparar(conexion, Base + "\nSELECT count(*) FROM p", desde, hoy, modelo, tipo))
        {
            total = (int)(long)(await conteo.ExecuteScalarAsync(ct))!;
        }

        await using var comando = Preparar(
            conexion,
            Base
            + """

            SELECT pregunta, modelo, dia, estado, negativa, fallo, reemplazado, causa, razones
              FROM p
             ORDER BY dia DESC, id
             LIMIT @limite OFFSET @salto
            """,
            desde, hoy, modelo, tipo);
        comando.Parameters.AddWithValue("limite", ParametrosDeCalidad.TamanoDePagina);
        comando.Parameters.AddWithValue("salto", (pagina - 1) * ParametrosDeCalidad.TamanoDePagina);

        var filas = new List<ProblemaDeCalidad>();
        await using var lector = await comando.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
        {
            var causa = lector.IsDBNull(7) ? null : lector.GetString(7);
            var razones = lector.IsDBNull(8) ? [] : lector.GetFieldValue<string[]>(8);
            var tipoDeLaFila = tipo ?? PrimerTipo(lector.GetBoolean(4), lector.GetBoolean(5));

            filas.Add(new ProblemaDeCalidad(
                tipoDeLaFila,
                lector.GetString(0),
                MotivoDe(tipoDeLaFila, causa, razones),
                lector.IsDBNull(1) ? null : lector.GetString(1),
                lector.GetFieldValue<DateOnly>(2),
                lector.GetString(3)));
        }

        return new PaginaDeProblemas(pagina, ParametrosDeCalidad.TamanoDePagina, total, filas);
    }

    /// <summary>The type shown for a turn when no type was asked for: the first that applies.</summary>
    private static string PrimerTipo(bool negativa, bool fallo) =>
        negativa
            ? ParametrosDeCalidad.TipoNegativa
            : fallo ? ParametrosDeCalidad.TipoFallo : ParametrosDeCalidad.TipoReemplazo;

    private static string? MotivoDe(string tipo, string? causa, string[] razones) => tipo switch
    {
        ParametrosDeCalidad.TipoNegativa => razones.Length == 0 ? null : string.Join(',', razones),
        ParametrosDeCalidad.TipoFallo => causa ?? ParametrosDeCalidad.SinClasificar,
        _ => null,
    };

    private static NpgsqlCommand Preparar(
        NpgsqlConnection conexion, string sql, DateOnly desde, DateOnly hasta, string? modelo, string? tipo)
    {
        var comando = ConsultasDeCalidad.Preparar(conexion, sql, desde, hasta, modelo);
        comando.Parameters.Add(new NpgsqlParameter("tipo", NpgsqlDbType.Text) { Value = (object?)tipo ?? DBNull.Value });

        return comando;
    }
}
