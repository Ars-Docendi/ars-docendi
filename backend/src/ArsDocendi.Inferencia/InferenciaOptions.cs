namespace ArsDocendi.Inferencia;

public sealed class InferenciaOptions
{
    public const string Seccion = "Inferencia";
    public string Url { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
    public string Contrasena { get; set; } = string.Empty;
    // Service Token de Cloudflare Access: sólo staging/pr-N; prod llega por la red local.
    public string AccessClientId { get; set; } = string.Empty;
    public string AccessClientSecret { get; set; } = string.Empty;

    public bool Habilitada => !string.IsNullOrWhiteSpace(Url);

    /// <summary>Opciones faltantes o inválidas, por nombre y nunca por valor.</summary>
    public IEnumerable<string> Errores()
    {
        if (!Habilitada) yield break;
        if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            yield return $"{Seccion}:Url debe ser una URL absoluta http o https.";
        if (string.IsNullOrWhiteSpace(Usuario)) yield return $"Falta {Seccion}:Usuario.";
        if (string.IsNullOrWhiteSpace(Contrasena)) yield return $"Falta {Seccion}:Contrasena.";
        if (string.IsNullOrWhiteSpace(AccessClientId) != string.IsNullOrWhiteSpace(AccessClientSecret))
            yield return $"{Seccion}:AccessClientId y {Seccion}:AccessClientSecret deben configurarse juntos.";
    }
}
