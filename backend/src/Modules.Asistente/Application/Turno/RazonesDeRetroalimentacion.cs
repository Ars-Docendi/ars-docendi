namespace Modules.Asistente.Application;

/// <summary>
/// The fixed, closed set of reasons a thumbs-down vote may carry, zero or more at once.
/// </summary>
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
