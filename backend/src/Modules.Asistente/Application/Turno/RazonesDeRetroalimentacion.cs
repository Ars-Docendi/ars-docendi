namespace Modules.Asistente.Application;

/// <summary>
/// The fixed, closed set of reasons a thumbs-down vote may carry.
/// </summary>
/// <remarks>
/// Closed on purpose (asistente-retroalimentacion's spec: "MUST NOT accept
/// free-text reasons"): free text is how a rare, identifying complaint ends up
/// sitting next to an otherwise-anonymous row, the same class of risk TD-012
/// already calls out for <c>intencion_sombra</c>. These four are exactly the
/// values <see cref="Todas"/> accepts from the API — the database's own CHECK
/// constraint (<c>retroalimentacion_turno_razon_valida</c> in
/// <c>003_asistente_retroalimentacion.sql</c>) additionally allows the retired
/// <c>lento</c>, forever: asistente-rediseno-v3's design.md D7 wanted a second,
/// database-level guard rejecting new <c>lento</c> rows too, but a Postgres
/// CHECK can only be widened by dropping and recreating it, and this module's
/// own migration convention forbids every <c>DROP</c> — see the comment in that
/// SQL file. <see cref="Todas"/> excluding it is therefore the ONLY guard: this
/// table has exactly one writer (this service, through this list), so gating
/// here is sufficient in practice, even though a hand-written SQL statement
/// against the database directly could still write <c>lento</c>.
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
