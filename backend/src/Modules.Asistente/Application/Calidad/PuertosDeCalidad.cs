namespace Modules.Asistente.Application;

/// <summary>
/// The counters of one slice of turns (the whole period, or one model) that every
/// quality rate is built from (design D1 of asistente-metricas-de-calidad).
/// </summary>
/// <remarks>
/// Every count is over <b>turns attended</b>: policy denials (quota, organization cap,
/// maintenance, revoked access, concurrent turn) say nothing about a model and are
/// excluded from numerators and denominators alike. A rate is <c>null</c> when its
/// denominator is zero, never zero, so "no votes" cannot read as "no complaints".
/// Rates are fractions between 0 and 1.
/// </remarks>
/// <param name="Turnos">Turns attended.</param>
/// <param name="Votos">Turns that received a vote.</param>
/// <param name="Positivos">Votes that were thumbs up.</param>
/// <param name="Negativos">Votes that were thumbs down.</param>
/// <param name="Respondidas">Turns that ended answered.</param>
/// <param name="ConProblemas">Turns rated negative, replaced or failed, each counted once.</param>
/// <param name="FallosDetectables">Turns with a provider or query cause, or an unclassified failure.</param>
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
/// <param name="Clave">Reason code, cause code or policy cause code.</param>
/// <param name="Cantidad">How many votes or turns carry it.</param>
/// <param name="Familia">
/// For causes only: <c>proveedor</c>, <c>consulta</c>, <c>senal</c> or <c>politica</c>,
/// so the screen can show <c>resultado_vacio</c> as a signal and not as a failure.
/// Null for reasons, and for the "unclassified" bucket.
/// </param>
public sealed record ConteoDeCalidad(string Clave, int Cantidad, string? Familia = null);

/// <summary>The counters of one model.</summary>
/// <param name="Modelo">The <c>empresa/modelo</c> name; null for rows recorded before the model was.</param>
/// <param name="Resumen">Its counters; <see cref="ResumenDeCalidad.Turnos"/> is the volume to read it against.</param>
public sealed record ModeloDeCalidad(string? Modelo, ResumenDeCalidad Resumen);

/// <summary>One point of the series: a day, or the Monday of a week for a 90-day period.</summary>
/// <param name="Dia">The turn's day (never the day the vote was cast).</param>
/// <param name="Turnos">Turns attended that day.</param>
/// <param name="Votos">Votes on those turns.</param>
/// <param name="Negativos">Negative votes on those turns.</param>
public sealed record PuntoDeCalidad(DateOnly Dia, int Turnos, int Votos, int Negativos)
{
    /// <summary>Negative votes over votes that day.</summary>
    public double? TasaNegativa => Votos == 0 ? null : (double)Negativos / Votos;
}

/// <summary>The headline compared with the previous period of equal length.</summary>
/// <param name="TasaAnterior">The negative rate of the previous period.</param>
/// <param name="Variacion">Current minus previous, in fraction points (0.05 is five points).</param>
/// <param name="VotosAnteriores">Votes of the previous period.</param>
public sealed record TendenciaDeCalidad(double TasaAnterior, double Variacion, int VotosAnteriores);

/// <summary>Everything the aggregate endpoint returns for a period and an optional model.</summary>
/// <param name="PeriodoDias">7, 30 or 90.</param>
/// <param name="Modelo">The model filter, or null for all.</param>
/// <param name="Desde">First day of the period (UTC).</param>
/// <param name="Hasta">Last day of the period, today (UTC).</param>
/// <param name="PorSemana">True when the series is weekly (90 days).</param>
/// <param name="Resumen">The counters of the whole period.</param>
/// <param name="Tendencia">Null when it cannot be told: see <paramref name="MotivoSinTendencia"/>.</param>
/// <param name="MotivoSinTendencia">
/// <c>pocos_votos</c> when either period has fewer than 20 votes, <c>fuera_de_retencion</c>
/// when the previous period is not fully inside retention; null when there is a trend.
/// </param>
/// <param name="NegativosPorMotivo">
/// Votes per reason plus <c>sin_motivo</c>. A vote can carry several reasons, so it does
/// not add up to the negative total.
/// </param>
/// <param name="FallosPorCausa">
/// Provider and query causes, <c>resultado_vacio</c> as a signal, and <c>sin_clasificar</c>
/// for a failing turn that recorded no cause.
/// </param>
/// <param name="DenegacionesDePolitica">Policy denials by cause, outside every rate.</param>
/// <param name="PorModelo">One row per model, in the period.</param>
/// <param name="ModelosDisponibles">Every model seen in the period, ignoring the model filter.</param>
/// <param name="Serie">The series, oldest first; days or weeks with no turns are omitted.</param>
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
/// <param name="Tipo"><c>negativa</c>, <c>fallo</c> or <c>reemplazo</c>.</param>
/// <param name="Pregunta">The anonymous question text, as the analytic record keeps it.</param>
/// <param name="Motivo">The vote's reasons (comma-separated) or the failure cause; null when there is none.</param>
/// <param name="Modelo">The model of the turn.</param>
/// <param name="Dia">The turn's day.</param>
/// <param name="Estado">The turn's outcome state.</param>
public sealed record ProblemaDeCalidad(
    string Tipo, string Pregunta, string? Motivo, string? Modelo, DateOnly Dia, string Estado);

/// <summary>One page of the problem list.</summary>
public sealed record PaginaDeProblemas(
    int Pagina, int TamanoDePagina, int Total, IReadOnlyList<ProblemaDeCalidad> Filas);

/// <summary>
/// The aggregate quality reader (asistente-metricas-de-calidad, gated by
/// <c>asistente.administrar</c>).
/// </summary>
/// <remarks>
/// Reads the analytic record and the vote table and nothing else: never the operational
/// record, never an actor, never a vote's free text or its precise time (TD-012).
/// <c>ArquitecturaAsistenteTests</c> enforces it on the source of the implementation.
/// </remarks>
public interface IConsultasDeCalidad
{
    /// <summary>The metrics of the last <paramref name="periodoDias"/> days, optionally for one model.</summary>
    Task<CalidadAgregada> ObtenerAsync(int periodoDias, string? modelo, CancellationToken ct);
}

/// <summary>
/// The anonymous list of problem answers (gated by <c>asistente.revisar_calidad</c>).
/// </summary>
/// <remarks>Same table confinement as <see cref="IConsultasDeCalidad"/>.</remarks>
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
/// <remarks>
/// A failing write MUST fail the read: the guarantee is "audited before it is answered",
/// and a swallowed exception would turn it into "audited unless the audit fails", the
/// unaudited read this trail exists to prevent (same criterion as
/// <see cref="IConsultasDeAuditoriaDeSoporte"/>).
/// </remarks>
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
