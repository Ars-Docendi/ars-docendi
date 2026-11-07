namespace Modules.Designaciones.Infrastructure.Guarani;

/// <summary>
/// Configuración de la API de Guaraní (sección <c>Guarani</c>). Usuario y contraseña salen de
/// las variables de entorno <c>guarani_user</c> y <c>guarani_pass</c>; nunca se persisten ni se
/// envían al navegador. Son obligatorias salvo en Development, donde la API local no las pide.
/// Sin configuración completa la integración queda deshabilitada y los endpoints de importación
/// responden 503: el Host arranca igual. Con credenciales, o fuera de Development, la URL debe
/// ser HTTPS; las rutas son siempre relativas, para no mandar las credenciales a otro origen.
/// </summary>
public sealed class OpcionesGuarani
{
    public const string Seccion = "Guarani";
    public const string MarcadorPeriodo = "{periodoLectivo}";
    public const string VariableUsuario = "guarani_user";
    public const string VariableContrasena = "guarani_pass";

    public string UrlBase { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
    public string Contrasena { get; set; } = string.Empty;

    /// <summary>Ruta relativa a <see cref="UrlBase"/> que lista los períodos lectivos.</summary>
    public string RutaPeriodosLectivos { get; set; } = string.Empty;

    /// <summary>Ruta relativa a <see cref="UrlBase"/>; incluye <see cref="MarcadorPeriodo"/>.</summary>
    public string RutaComisiones { get; set; } = string.Empty;

    public int TimeoutSegundos { get; set; } = 60;

    /// <summary>Falso sólo en Development; cualquier otro ambiente exige credenciales.</summary>
    public bool ExigeCredenciales { get; set; } = true;

    public bool TieneCredenciales =>
        !string.IsNullOrWhiteSpace(Usuario) && !string.IsNullOrWhiteSpace(Contrasena);

    public bool TieneEndpoints =>
        Uri.TryCreate(UrlBase, UriKind.Absolute, out _)
        && EsRutaRelativa(RutaPeriodosLectivos)
        && EsRutaRelativa(RutaComisiones)
        && RutaComisiones.Contains(MarcadorPeriodo, StringComparison.Ordinal);

    /// <summary>HTTP sólo se admite en Development y sin credenciales: la API local no las pide.</summary>
    public bool FaltaHttps =>
        (ExigeCredenciales || TieneCredenciales)
        && !(Uri.TryCreate(UrlBase, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps);

    public bool FaltanCredenciales => ExigeCredenciales && !TieneCredenciales;

    public bool EstaConfigurada => TieneEndpoints && !FaltanCredenciales && !FaltaHttps;

    private static bool EsRutaRelativa(string ruta) =>
        !string.IsNullOrWhiteSpace(ruta)
        && ruta[0] is not ('/' or '\\')
        && !Uri.TryCreate(ruta, UriKind.Absolute, out _);
}
