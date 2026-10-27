namespace Modules.Asistente.Application;

/// <summary>
/// The counters of one slice of turns (the whole period, or one model) that every quality rate is
/// built from (design D1 of asistente-metricas-de-calidad).
/// </summary>
public sealed record ResumenDeCalidad(
    int Turnos,
    int Votos,
    int Positivos,
    int Negativos,
    int Respondidas,
    int ConProblemas,
    int FallosDetectables)
{
    /// <summary>Negative votes over votes (the headline).</summary>
    public double? TasaNegativa => Votos == 0 ? null : (double)Negativos / Votos;

    /// <summary>Turns with problems over turns attended.</summary>
    public double? TasaConProblemas => Turnos == 0 ? null : (double)ConProblemas / Turnos;

    /// <summary>Detectable failures over turns attended.</summary>
    public double? TasaDeFallos => Turnos == 0 ? null : (double)FallosDetectables / Turnos;

    /// <summary>Votes over answered turns.</summary>
    public double? Cobertura => Respondidas == 0 ? null : (double)Votos / Respondidas;
}

/// <summary>One bar of a breakdown: a code and how many times it occurred.</summary>
public sealed record ConteoDeCalidad(string Clave, int Cantidad, string? Familia = null);

/// <summary>The counters of one model.</summary>
public sealed record ModeloDeCalidad(string? Modelo, ResumenDeCalidad Resumen);

/// <summary>One point of the series: a day, or the Monday of a week for a 90-day period.</summary>
public sealed record PuntoDeCalidad(DateOnly Dia, int Turnos, int Votos, int Negativos)
{
    /// <summary>Negative votes over votes that day.</summary>
    public double? TasaNegativa => Votos == 0 ? null : (double)Negativos / Votos;
}

/// <summary>The headline compared with the previous period of equal length.</summary>
public sealed record TendenciaDeCalidad(double TasaAnterior, double Variacion, int VotosAnteriores);

/// <summary>Everything the aggregate endpoint returns for a period and an optional model.</summary>
public sealed record CalidadAgregada(
    int PeriodoDias,
    string? Modelo,
    DateOnly Desde,
    DateOnly Hasta,
    bool PorSemana,
    ResumenDeCalidad Resumen,
    TendenciaDeCalidad? Tendencia,
    string? MotivoSinTendencia,
    IReadOnlyList<ConteoDeCalidad> NegativosPorMotivo,
    IReadOnlyList<ConteoDeCalidad> FallosPorCausa,
    IReadOnlyList<ConteoDeCalidad> DenegacionesDePolitica,
    IReadOnlyList<ModeloDeCalidad> PorModelo,
    IReadOnlyList<string> ModelosDisponibles,
    IReadOnlyList<PuntoDeCalidad> Serie);

/// <summary>One anonymous row of the problem list.</summary>
public sealed record ProblemaDeCalidad(
    string Tipo, string Pregunta, string? Motivo, string? Modelo, DateOnly Dia, string Estado);

/// <summary>One page of the problem list.</summary>
public sealed record PaginaDeProblemas(
    int Pagina, int TamanoDePagina, int Total, IReadOnlyList<ProblemaDeCalidad> Filas);

/// <summary>
/// The aggregate quality reader (asistente-metricas-de-calidad, gated by
/// <c>asistente.administrar</c>).
/// </summary>
public interface IConsultasDeCalidad
{
    /// <summary>The metrics of the last <paramref name="periodoDias"/> days, optionally for one model.</summary>
    Task<CalidadAgregada> ObtenerAsync(int periodoDias, string? modelo, CancellationToken ct);
}

/// <summary>
/// The anonymous list of problem answers (gated by <c>asistente.revisar_calidad</c>).
/// </summary>
public interface IConsultasDeProblemasDeCalidad
{
    /// <summary>
    /// One page of problem answers. <paramref name="tipo"/> is null for all of them, or one
    /// of <c>negativa</c>, <c>fallo</c>, <c>reemplazo</c>; <paramref name="pagina"/> is 1-based.
    /// </summary>
    Task<PaginaDeProblemas> ListarAsync(
        int periodoDias, string? modelo, string? tipo, int pagina, CancellationToken ct);
}

/// <summary>The append-only audit of every read of the problem list.</summary>
public interface IAuditoriaDeRevisionDeCalidad
{
    /// <summary>Records who read, when (the database clock), with which filters and how many rows.</summary>
    Task RegistrarAsync(Guid lector, string filtros, int filas, CancellationToken ct);
}

/// <summary>Constants shared by the quality readers and the endpoints.</summary>
internal static class ParametrosDeCalidad
{
    /// <summary>The periods the aggregate endpoint accepts, in days. 90 matches the analytic retention.</summary>
    public static readonly IReadOnlyList<int> Periodos = [7, 30, 90];

    /// <summary>
    /// The trend is omitted when either period has fewer votes than this (decided
    /// 2026-10-24, task 0.2): below it a change of one vote moves the rate by five points.
    /// </summary>
    public const int MinimoDeVotosParaTendencia = 20;

    /// <summary>Rows per page of the problem list.</summary>
    public const int TamanoDePagina = 25;

    /// <summary>Longest page number accepted, so a huge OFFSET cannot be requested.</summary>
    public const int PaginaMaxima = 1000;

    public const string TipoNegativa = "negativa";
    public const string TipoFallo = "fallo";
    public const string TipoReemplazo = "reemplazo";

    /// <summary>The three problem types, in the order of priority when a turn has several.</summary>
    public static readonly IReadOnlyList<string> Tipos = [TipoNegativa, TipoFallo, TipoReemplazo];

    /// <summary>Bucket for a failing turn that recorded no cause.</summary>
    public const string SinClasificar = "sin_clasificar";

    /// <summary>Bucket for a negative vote that carried no reason.</summary>
    public const string SinMotivo = "sin_motivo";
}
