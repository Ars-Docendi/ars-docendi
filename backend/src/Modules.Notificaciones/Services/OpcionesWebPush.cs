namespace Modules.Notificaciones.Services;

/// <summary>
/// VAPID configuration (<c>Notificaciones:WebPush</c>). Without keys Web Push is disabled. The keys
/// come from the environment of the process, never from the repository.
/// </summary>
public sealed class OpcionesWebPush
{
    public const string Seccion = "Notificaciones:WebPush";

    private static readonly string[] SufijosReservados =
        [".local", ".localhost", ".test", ".example", ".invalid", "example.com", "example.org", "example.net"];

    public string? PublicKey { get; set; }
    public string? PrivateKey { get; set; }
    public string? Subject { get; set; }

    /// <summary>Compose interpolates an undefined variable as empty: that counts as absent.</summary>
    public bool SinClaves => string.IsNullOrWhiteSpace(PublicKey) && string.IsNullOrWhiteSpace(PrivateKey);

    /// <summary>Problems that make the configuration unusable. Empty when valid or when disabled.</summary>
    public IReadOnlyList<string> Errores()
    {
        if (SinClaves) return [];
        var errores = new List<string>();
        if (string.IsNullOrWhiteSpace(PublicKey)) errores.Add($"{Seccion}:PublicKey falta (hay PrivateKey).");
        else if (!ClaveValida(PublicKey, 65)) errores.Add($"{Seccion}:PublicKey debe ser base64url de 65 bytes.");
        if (string.IsNullOrWhiteSpace(PrivateKey)) errores.Add($"{Seccion}:PrivateKey falta (hay PublicKey).");
        else if (!ClaveValida(PrivateKey, 32)) errores.Add($"{Seccion}:PrivateKey debe ser base64url de 32 bytes.");

        var dominio = DominioDelSubject(out var errorSubject);
        if (errorSubject is not null) errores.Add(errorSubject);
        else if (dominio is null) errores.Add($"{Seccion}:Subject falta; usá mailto:<correo> o https://<sitio>.");
        return errores;
    }

    /// <summary>
    /// The subject names a domain Apple answers 403 BadJwtToken for (reserved TLDs such as .local).
    /// Chrome and Firefox accept it, so outside Production it is only a warning.
    /// </summary>
    public bool SubjectEnDominioReservado()
    {
        var dominio = DominioDelSubject(out var error);
        if (error is not null || dominio is null) return false;
        dominio = dominio.ToLowerInvariant();
        return dominio == "localhost"
            || SufijosReservados.Any(s => s.StartsWith('.') ? dominio.EndsWith(s, StringComparison.Ordinal)
                                                              : dominio == s || dominio.EndsWith("." + s, StringComparison.Ordinal));
    }

    private string? DominioDelSubject(out string? error)
    {
        error = null;
        var subject = Subject?.Trim();
        if (string.IsNullOrEmpty(subject)) return null;
        if (subject.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            var direccion = subject["mailto:".Length..];
            var arroba = direccion.LastIndexOf('@');
            if (arroba > 0 && arroba < direccion.Length - 1 && direccion.IndexOf('.', arroba) > 0)
                return direccion[(arroba + 1)..];
        }
        else if (Uri.TryCreate(subject, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
        {
            return uri.Host;
        }
        error = $"{Seccion}:Subject debe ser mailto:<correo> o https://<sitio>.";
        return null;
    }

    private static bool ClaveValida(string clave, int bytes)
    {
        try
        {
            var base64 = clave.Trim().Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            return Convert.FromBase64String(base64).Length == bytes;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
