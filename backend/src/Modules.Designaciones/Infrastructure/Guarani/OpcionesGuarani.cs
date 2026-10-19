namespace Modules.Designaciones.Infrastructure.Guarani;

/// <summary>
/// Configuración de la API de Guaraní (sección <c>Guarani</c>). Usuario y contraseña son
/// secretos por ambiente (user-secrets en local, <c>Guarani__*</c> en deploy); nunca se
/// persisten ni se envían al navegador. Sin configuración completa la integración queda
/// deshabilitada y los endpoints de importación responden 503: el Host arranca igual.
/// </summary>
public sealed class OpcionesGuarani
{
    public const string Seccion = "Guarani";
    public const string MarcadorPeriodo = "{periodoLectivo}";

    public string UrlBase { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
    public string Contrasena { get; set; } = string.Empty;

    /// <summary>Ruta relativa a <see cref="UrlBase"/> que lista los períodos lectivos.</summary>
    public string RutaPeriodosLectivos { get; set; } = string.Empty;

    /// <summary>Ruta relativa a <see cref="UrlBase"/>; incluye <see cref="MarcadorPeriodo"/>.</summary>
    public string RutaComisiones { get; set; } = string.Empty;

    public int TimeoutSegundos { get; set; } = 60;

    public bool EstaConfigurada =>
        Uri.TryCreate(UrlBase, UriKind.Absolute, out _)
        && !string.IsNullOrWhiteSpace(Usuario)
        && !string.IsNullOrWhiteSpace(Contrasena)
        && !string.IsNullOrWhiteSpace(RutaPeriodosLectivos)
        && RutaComisiones.Contains(MarcadorPeriodo, StringComparison.Ordinal);
}
