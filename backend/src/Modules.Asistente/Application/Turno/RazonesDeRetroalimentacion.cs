namespace Modules.Asistente.Application;

/// <summary>
/// The fixed, closed set of reasons a thumbs-down vote may carry.
/// </summary>
/// <remarks>
/// Closed on purpose (asistente-retroalimentacion's spec: "MUST NOT accept
/// free-text reasons"): free text is how a rare, identifying complaint ends up
/// sitting next to an otherwise-anonymous row, the same class of risk TD-012
/// already calls out for <c>intencion_sombra</c>. The four values here are the
/// only ones the database's own CHECK constraint
/// (<c>retroalimentacion_turno_razon_valida</c> in
/// <c>003_asistente_retroalimentacion.sql</c>) allows — this class exists so
/// application code and the SQL migration cannot drift on that list
/// independently.
/// </remarks>
internal static class RazonesDeRetroalimentacion
{
    public const string DatosIncorrectos = "datos_incorrectos";
    public const string NoEntendioLaPregunta = "no_entendio_la_pregunta";
    public const string Lento = "lento";
    public const string Otro = "otro";

    /// <summary>All four, in the order the UI presents them.</summary>
    public static readonly IReadOnlyList<string> Todas =
    [
        DatosIncorrectos,
        NoEntendioLaPregunta,
        Lento,
        Otro,
    ];
}
