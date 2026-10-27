using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Options;
using Modules.Asistente.Application;
using Npgsql;
using NpgsqlTypes;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// <see cref="IConsultasDeCalidad"/> over <c>asistente.registro_analitico</c> joined with
/// <c>asistente.retroalimentacion_turno</c> (design D1 and D8 of asistente-metricas-de-calidad).
/// </summary>
internal sealed class ConsultasDeCalidad(
    CadenaDuena cadena,
    TimeProvider reloj,
    IOptions<OpcionesAsistente> opciones) : IConsultasDeCalidad
{
    /// <summary>The slice of turns every query starts from: the period, the model, and two flags.</summary>
    private const string Base =
        """
        WITH t AS (
            SELECT a.id, a.dia, a.modelo, a.estado, a.causa, a.reemplazado, r.voto, r.razones,
                   (a.causa IS NULL OR NOT (a.causa = ANY(@politica))) AS atendido,
                   COALESCE(
                       a.causa = ANY(@fallos)
                       OR (a.causa IS NULL AND a.estado IN ('ServicioDegradado', 'Fallo')),
                       false) AS fallo
              FROM asistente.registro_analitico a
              LEFT JOIN asistente.retroalimentacion_turno r ON r.analitico_id = a.id
             WHERE a.dia >= @desde AND a.dia <= @hasta
               AND (@modelo::text IS NULL OR a.modelo = @modelo)
        )
        """;

    private static readonly string[] CausasDePolitica = CausasConFamilia(FamiliaDeCausa.Politica);

    private static readonly string[] CausasDeFallo =
        [.. CausasConFamilia(FamiliaDeCausa.Proveedor), .. CausasConFamilia(FamiliaDeCausa.Consulta)];

    public async Task<CalidadAgregada> ObtenerAsync(int periodoDias, string? modelo, CancellationToken ct)
    {
        if (!ParametrosDeCalidad.Periodos.Contains(periodoDias))
        {
            throw new ArgumentOutOfRangeException(nameof(periodoDias), periodoDias, "Unsupported period.");
        }

        var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);
        var desde = hoy.AddDays(-(periodoDias - 1));
        var porSemana = periodoDias >= 90;

        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        var porModelo = await LeerPorModeloAsync(conexion, desde, hoy, modelo, ct);
        var resumen = Sumar(porModelo.Select(m => m.Resumen));

        var (tendencia, motivoSinTendencia) = await CalcularTendenciaAsync(
            conexion, periodoDias, hoy, desde, modelo, resumen, ct);

        return new CalidadAgregada(
            periodoDias,
            modelo,
            desde,
            hoy,
            porSemana,
            resumen,
            tendencia,
            motivoSinTendencia,
            await LeerMotivosAsync(conexion, desde, hoy, modelo, ct),
            await LeerCausasAsync(conexion, desde, hoy, modelo, ct),
            await LeerPoliticaAsync(conexion, desde, hoy, modelo, ct),
            [.. porModelo.Where(m => m.Resumen.Turnos > 0).OrderByDescending(m => m.Resumen.Turnos)],
            await LeerModelosAsync(conexion, desde, hoy, ct),
            await LeerSerieAsync(conexion, desde, hoy, modelo, porSemana, ct));
    }

    private async Task<(TendenciaDeCalidad? Tendencia, string? Motivo)> CalcularTendenciaAsync(
        NpgsqlConnection conexion, int periodoDias, DateOnly hoy, DateOnly desde, string? modelo,
        ResumenDeCalidad actual, CancellationToken ct)
    {
        var hastaAnterior = desde.AddDays(-1);
        var desdeAnterior = hastaAnterior.AddDays(-(periodoDias - 1));

        // The purge keeps the days from today minus the retention; a previous period that
        // starts before that is partly gone, so a comparison would be against a shorter one.
        var primerDiaRetenido = hoy.AddDays(-opciones.Value.RetencionDeRegistrosDias);
        if (desdeAnterior < primerDiaRetenido)
        {
            return (null, "fuera_de_retencion");
        }

        var anterior = Sumar((await LeerPorModeloAsync(conexion, desdeAnterior, hastaAnterior, modelo, ct))
            .Select(m => m.Resumen));

        if (actual.Votos < ParametrosDeCalidad.MinimoDeVotosParaTendencia
            || anterior.Votos < ParametrosDeCalidad.MinimoDeVotosParaTendencia)
        {
            return (null, "pocos_votos");
        }

        var tasaAnterior = anterior.TasaNegativa!.Value;

        return (new TendenciaDeCalidad(tasaAnterior, actual.TasaNegativa!.Value - tasaAnterior, anterior.Votos), null);
    }

    private static ResumenDeCalidad Sumar(IEnumerable<ResumenDeCalidad> partes) =>
        partes.Aggregate(
            new ResumenDeCalidad(0, 0, 0, 0, 0, 0, 0),
            (suma, p) => new ResumenDeCalidad(
                suma.Turnos + p.Turnos,
                suma.Votos + p.Votos,
                suma.Positivos + p.Positivos,
                suma.Negativos + p.Negativos,
                suma.Respondidas + p.Respondidas,
                suma.ConProblemas + p.ConProblemas,
                suma.FallosDetectables + p.FallosDetectables));

    private static async Task<List<ModeloDeCalidad>> LeerPorModeloAsync(
        NpgsqlConnection conexion, DateOnly desde, DateOnly hasta, string? modelo, CancellationToken ct)
    {
        await using var comando = Preparar(
            conexion,
            Base
            + """

            SELECT modelo,
                   count(*) FILTER (WHERE atendido),
                   count(*) FILTER (WHERE atendido AND voto IS NOT NULL),
                   count(*) FILTER (WHERE atendido AND voto = true),
                   count(*) FILTER (WHERE atendido AND voto = false),
                   count(*) FILTER (WHERE atendido AND estado = 'Respondida'),
                   count(*) FILTER (WHERE atendido AND (voto = false OR reemplazado OR fallo)),
                   count(*) FILTER (WHERE atendido AND fallo)
              FROM t
             GROUP BY modelo
            """,
            desde, hasta, modelo);

        var filas = new List<ModeloDeCalidad>();
        await using var lector = await comando.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
        {
            filas.Add(new ModeloDeCalidad(
                lector.IsDBNull(0) ? null : lector.GetString(0),
                new ResumenDeCalidad(
                    (int)lector.GetInt64(1), (int)lector.GetInt64(2), (int)lector.GetInt64(3),
                    (int)lector.GetInt64(4), (int)lector.GetInt64(5), (int)lector.GetInt64(6),
                    (int)lector.GetInt64(7))));
        }

        return filas;
    }

    private static async Task<IReadOnlyList<PuntoDeCalidad>> LeerSerieAsync(
        NpgsqlConnection conexion, DateOnly desde, DateOnly hasta, string? modelo, bool porSemana,
        CancellationToken ct)
    {
        await using var comando = Preparar(
            conexion,
            Base
            + """

            SELECT CASE WHEN @semanal THEN date_trunc('week', dia::timestamp)::date ELSE dia END AS punto,
                   count(*),
                   count(*) FILTER (WHERE voto IS NOT NULL),
                   count(*) FILTER (WHERE voto = false)
              FROM t
             WHERE atendido
             GROUP BY punto
             ORDER BY punto
            """,
            desde, hasta, modelo);
        comando.Parameters.AddWithValue("semanal", porSemana);

        var conDatos = new Dictionary<DateOnly, PuntoDeCalidad>();
        await using (var lector = await comando.ExecuteReaderAsync(ct))
        {
            while (await lector.ReadAsync(ct))
            {
                var dia = lector.GetFieldValue<DateOnly>(0);
                conDatos[dia] = new PuntoDeCalidad(
                    dia, (int)lector.GetInt64(1), (int)lector.GetInt64(2), (int)lector.GetInt64(3));
            }
        }

        // The series covers every day (or every week) of the period, so a chart cannot stretch one
        // day of data over the whole width.
        var paso = porSemana ? 7 : 1;
        var primero = porSemana ? LunesDe(desde) : desde;
        var puntos = new List<PuntoDeCalidad>();
        for (var dia = primero; dia <= hasta; dia = dia.AddDays(paso))
        {
            puntos.Add(conDatos.GetValueOrDefault(dia) ?? new PuntoDeCalidad(dia, 0, 0, 0));
        }

        return puntos;
    }

    /// <summary>The Monday of the week of <paramref name="dia"/>, as PostgreSQL's <c>date_trunc('week')</c>.</summary>
    private static DateOnly LunesDe(DateOnly dia) =>
        dia.AddDays(-(((int)dia.DayOfWeek + 6) % 7));

    private static async Task<IReadOnlyList<ConteoDeCalidad>> LeerMotivosAsync(
        NpgsqlConnection conexion, DateOnly desde, DateOnly hasta, string? modelo, CancellationToken ct)
    {
        await using var comando = Preparar(
            conexion,
            Base
            + """

            SELECT m, count(*)
              FROM t CROSS JOIN LATERAL unnest(t.razones) AS m
             WHERE atendido AND voto = false
             GROUP BY m
            UNION ALL
            SELECT 'sin_motivo', count(*)
              FROM t
             WHERE atendido AND voto = false AND COALESCE(cardinality(razones), 0) = 0
            """,
            desde, hasta, modelo);

        var cuentas = new Dictionary<string, int>(StringComparer.Ordinal);
        await using (var lector = await comando.ExecuteReaderAsync(ct))
        {
            while (await lector.ReadAsync(ct))
            {
                cuentas[lector.GetString(0)] = (int)lector.GetInt64(1);
            }
        }

        // The closed vocabulary always comes back whole, so a reason nobody picked reads as
        // zero and not as a missing bar.
        return
        [
            .. RazonesDeRetroalimentacion.Todas
                .Select(r => new ConteoDeCalidad(r, cuentas.GetValueOrDefault(r))),
            new ConteoDeCalidad(
                ParametrosDeCalidad.SinMotivo, cuentas.GetValueOrDefault(ParametrosDeCalidad.SinMotivo)),
        ];
    }

    private static async Task<IReadOnlyList<ConteoDeCalidad>> LeerCausasAsync(
        NpgsqlConnection conexion, DateOnly desde, DateOnly hasta, string? modelo, CancellationToken ct)
    {
        // A failing turn that recorded no cause is not dropped: it lands in its own bucket,
        // so a path that forgot to record one is visible instead of silent.
        await using var comando = Preparar(
            conexion,
            Base
            + """

            SELECT COALESCE(causa, 'sin_clasificar'), count(*)
              FROM t
             WHERE atendido AND (fallo OR causa = 'resultado_vacio')
             GROUP BY 1
            """,
            desde, hasta, modelo);

        var causas = new List<ConteoDeCalidad>();
        await using var lector = await comando.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
        {
            var clave = lector.GetString(0);
            causas.Add(new ConteoDeCalidad(clave, (int)lector.GetInt64(1), FamiliaDe(clave)));
        }

        return [.. causas.OrderByDescending(c => c.Cantidad).ThenBy(c => c.Clave, StringComparer.Ordinal)];
    }

    private static async Task<IReadOnlyList<ConteoDeCalidad>> LeerPoliticaAsync(
        NpgsqlConnection conexion, DateOnly desde, DateOnly hasta, string? modelo, CancellationToken ct)
    {
        await using var comando = Preparar(
            conexion,
            Base
            + """

            SELECT causa, count(*)
              FROM t
             WHERE NOT atendido
             GROUP BY causa
            """,
            desde, hasta, modelo);

        var causas = new List<ConteoDeCalidad>();
        await using var lector = await comando.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
        {
            causas.Add(new ConteoDeCalidad(lector.GetString(0), (int)lector.GetInt64(1), "politica"));
        }

        return [.. causas.OrderByDescending(c => c.Cantidad).ThenBy(c => c.Clave, StringComparer.Ordinal)];
    }

    private static async Task<IReadOnlyList<string>> LeerModelosAsync(
        NpgsqlConnection conexion, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        // Ignores the model filter on purpose: the screen's model select needs every model.
        await using var comando = new NpgsqlCommand(
            """
            SELECT DISTINCT modelo
              FROM asistente.registro_analitico
             WHERE dia >= @desde AND dia <= @hasta AND modelo IS NOT NULL
             ORDER BY modelo
            """, conexion);
        comando.Parameters.AddWithValue("desde", NpgsqlDbType.Date, desde);
        comando.Parameters.AddWithValue("hasta", NpgsqlDbType.Date, hasta);

        var modelos = new List<string>();
        await using var lector = await comando.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
        {
            modelos.Add(lector.GetString(0));
        }

        return modelos;
    }

    private static string? FamiliaDe(string causa)
    {
        foreach (var conocida in CausasDelTurno.Todas)
        {
            if (CausasDelTurno.ValorDeCable(conocida) == causa)
            {
                return CausasDelTurno.Familia(conocida) switch
                {
                    FamiliaDeCausa.Proveedor => "proveedor",
                    FamiliaDeCausa.Consulta => "consulta",
                    FamiliaDeCausa.Senal => "senal",
                    _ => "politica",
                };
            }
        }

        return null;
    }

    private static string[] CausasConFamilia(FamiliaDeCausa familia) =>
        [.. CausasDelTurno.Todas
            .Where(c => CausasDelTurno.Familia(c) == familia)
            .Select(CausasDelTurno.ValorDeCable)];

    /// <summary>A command carrying the parameters of <see cref="Base"/>.</summary>
    internal static NpgsqlCommand Preparar(
        NpgsqlConnection conexion, string sql, DateOnly desde, DateOnly hasta, string? modelo)
    {
        var comando = new NpgsqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("desde", NpgsqlDbType.Date, desde);
        comando.Parameters.AddWithValue("hasta", NpgsqlDbType.Date, hasta);
        comando.Parameters.Add(new NpgsqlParameter("modelo", NpgsqlDbType.Text) { Value = (object?)modelo ?? DBNull.Value });
        comando.Parameters.AddWithValue("politica", NpgsqlDbType.Array | NpgsqlDbType.Text, CausasDePolitica);
        comando.Parameters.AddWithValue("fallos", NpgsqlDbType.Array | NpgsqlDbType.Text, CausasDeFallo);

        return comando;
    }
}
