namespace Modules.Asistente.Application;

/// <summary>
/// The fixed, closed set of reasons a thumbs-down vote may carry, zero or more at once.
/// </summary>
/// <remarks>
/// Closed on purpose (asistente-retroalimentacion's spec): an unbounded vocabulary is how
/// a rare, identifying detail ends up sitting next to an otherwise-anonymous row, the
/// same class of risk TD-012 already calls out for <c>intencion_sombra</c>. These four
/// are exactly the values <see cref="Todas"/> accepts from the API, and exactly the
/// values the database's own CHECK constraint
/// (<c>retroalimentacion_turno_razones_validas</c> in
/// <c>003_asistente_retroalimentacion.sql</c>) restricts <c>razones</c>' elements to.
/// "No duplicates" is validated by the controller and not by that CHECK — this table has
/// exactly one writer, so gating here is sufficient in practice.
/// </remarks>
internal static class RazonesDeRetroalimentacion
{
    public const string DatosIncorrectos = "datos_incorrectos";
    public const string NoEntendioLaPregunta = "no_entendio_la_pregunta";
    public const string FaltanDatos = "faltan_datos";
    public const string Otro = "otro";

    /// <summary>The four the API accepts, in the order the UI presents them.</summary>
    public static readonly IReadOnlyList<string> Todas =
    [
        DatosIncorrectos,
        NoEntendioLaPregunta,
        FaltanDatos,
        Otro,
    ];
}
