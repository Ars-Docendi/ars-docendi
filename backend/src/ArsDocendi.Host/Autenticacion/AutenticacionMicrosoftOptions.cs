namespace ArsDocendi.Host.Autenticacion;

/// <summary>
/// Configuración del ingreso con cuentas Microsoft. <see cref="ClientSecret"/> nunca
/// se versiona: en local va en <c>dotnet user-secrets</c> y en los despliegues en
/// secretos por ambiente.
/// </summary>
public sealed class AutenticacionMicrosoftOptions
{
    public const string Seccion = "AutenticacionMicrosoft";

    public bool Habilitada { get; set; }

    /// <summary>
    /// <c>common</c> admite cuentas personales y de cualquier organización; el id de
    /// un tenant restringe el ingreso a esa organización sin cambiar código.
    /// </summary>
    public string Tenant { get; set; } = "common";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public int MinutosInactividad { get; set; } = 60;
    public int HorasMaximas { get; set; } = 10;

    public string Authority => $"https://login.microsoftonline.com/{Tenant}/v2.0";
}
