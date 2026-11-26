namespace Modules.Notificaciones.Services;

/// <summary>
/// Allowlist of Web Push services. The endpoint is a URL chosen by the client that the server later
/// POSTs to, so accepting any host would be a server-side request forgery vector.
/// </summary>
public static class EndpointsPush
{
    private static readonly string[] HostsExactos = ["fcm.googleapis.com", "updates.push.services.mozilla.com"];

    private static readonly string[] Sufijos =
        [".push.apple.com", ".notify.windows.com", ".push.services.mozilla.com"];

    public static bool EsPermitido(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint) || endpoint.Length > 2048) return false;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || uri.UserInfo.Length > 0) return false;
        var host = uri.IdnHost.ToLowerInvariant();
        return HostsExactos.Contains(host)
            || Sufijos.Any(s => host.EndsWith(s, StringComparison.Ordinal));
    }
}
