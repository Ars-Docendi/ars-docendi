using ArsDocendi.Host.Desarrollo;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Validators;

namespace ArsDocendi.Host.Autenticacion;

/// <summary>Qué accesos quedaron registrados, para que el pipeline active lo que corresponde.</summary>
public sealed record AccesosHabilitados(bool Microsoft, bool Desarrollo)
{
    public bool Alguno => Microsoft || Desarrollo;
}

/// <summary>
/// Compone los esquemas de autenticación del Host:
/// <list type="bullet">
/// <item>Ingreso con Microsoft: OIDC para entrar y una cookie propia para la sesión.</item>
/// <item>Identidades de desarrollo por headers, sólo fuera de Production y con opt-in.</item>
/// </list>
/// Con ambos activos, un policy scheme elige por solicitud: los headers de desarrollo
/// mandan si están presentes; si no, la cookie.
/// </summary>
public static class RegistroAutenticacion
{
    public const string EsquemaSesion = "SesionArsDocendi";
    public const string EsquemaMicrosoft = "Microsoft";
    public const string EsquemaPorDefecto = "Sesion";
    public const string NombreCookie = "__Host-ars-sesion";
    public const string RutaCallback = "/api/auth/signin-oidc";

    public static AccesosHabilitados AddAutenticacionArsDocendi(this WebApplicationBuilder builder)
    {
        var desarrollo = !builder.Environment.IsProduction()
            && builder.Configuration.GetValue<bool>($"{AutenticacionDesarrolloOptions.Seccion}:Enabled");
        var microsoft = builder.Configuration
            .GetSection(AutenticacionMicrosoftOptions.Seccion)
            .Get<AutenticacionMicrosoftOptions>() ?? new AutenticacionMicrosoftOptions();
        var accesos = new AccesosHabilitados(microsoft.Habilitada, desarrollo);

        if (!accesos.Microsoft)
        {
            if (desarrollo)
            {
                builder.Services
                    .AddAuthentication(AutenticacionDesarrolloHandler.Esquema)
                    .AddScheme<AuthenticationSchemeOptions, AutenticacionDesarrolloHandler>(
                        AutenticacionDesarrolloHandler.Esquema, _ => { });
            }

            return accesos;
        }

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddAntifalsificacionSesion();
        builder.Services.Configure<AutenticacionMicrosoftOptions>(
            builder.Configuration.GetSection(AutenticacionMicrosoftOptions.Seccion));
        var autenticacion = builder.Services
            .AddAuthentication(opciones =>
            {
                opciones.DefaultScheme = EsquemaPorDefecto;
                opciones.DefaultChallengeScheme = EsquemaSesion;
                opciones.DefaultSignInScheme = EsquemaSesion;
            })
            .AddPolicyScheme(EsquemaPorDefecto, "Sesión o identidad de desarrollo", opciones =>
                opciones.ForwardDefaultSelector = contexto =>
                    desarrollo && contexto.Request.Headers.ContainsKey(AutenticacionDesarrolloHandler.HeaderUsuario)
                        ? AutenticacionDesarrolloHandler.Esquema
                        : EsquemaSesion)
            .AddCookie(EsquemaSesion, opciones => ConfigurarCookie(opciones, microsoft))
            .AddOpenIdConnect(EsquemaMicrosoft, opciones => ConfigurarMicrosoft(opciones, microsoft));

        if (desarrollo)
        {
            autenticacion.AddScheme<AuthenticationSchemeOptions, AutenticacionDesarrolloHandler>(
                AutenticacionDesarrolloHandler.Esquema, _ => { });
        }

        return accesos;
    }

    private static void ConfigurarCookie(
        CookieAuthenticationOptions opciones,
        AutenticacionMicrosoftOptions microsoft)
    {
        opciones.Cookie.Name = NombreCookie;
        opciones.Cookie.HttpOnly = true;
        opciones.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        opciones.Cookie.SameSite = SameSiteMode.Lax;
        opciones.Cookie.Path = "/";
        opciones.ExpireTimeSpan = TimeSpan.FromMinutes(microsoft.MinutosInactividad);
        opciones.SlidingExpiration = true;
        opciones.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = EventosSesion.ValidarAsync,
            // La API nunca redirige: el frontend decide qué hacer con 401/403.
            OnRedirectToLogin = contexto =>
            {
                contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = contexto =>
            {
                contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            },
        };
    }

    private static void ConfigurarMicrosoft(
        OpenIdConnectOptions opciones,
        AutenticacionMicrosoftOptions microsoft)
    {
        opciones.Authority = microsoft.Authority;
        opciones.ClientId = microsoft.ClientId;
        opciones.ClientSecret = microsoft.ClientSecret;
        opciones.ResponseType = "code";
        opciones.UsePkce = true;
        opciones.SaveTokens = false;
        opciones.MapInboundClaims = false;
        opciones.GetClaimsFromUserInfoEndpoint = false;
        opciones.CallbackPath = RutaCallback;
        opciones.SignedOutCallbackPath = "/api/auth/signout-callback-oidc";
        opciones.RemoteSignOutPath = "/api/auth/signout-oidc";
        opciones.SignInScheme = EsquemaSesion;
        opciones.Scope.Clear();
        opciones.Scope.Add("openid");
        opciones.Scope.Add("profile");
        opciones.Scope.Add("email");

        // Con `common` cada organización firma con su propio issuer: se valida contra
        // el tenant del token en lugar de un issuer fijo de la metadata.
        opciones.TokenValidationParameters.IssuerValidator =
            AadIssuerValidator.GetAadIssuerValidator(microsoft.Authority).Validate;
        opciones.TokenValidationParameters.EnableAadSigningKeyIssuerValidation();

        opciones.Events = new OpenIdConnectEvents
        {
            OnTokenValidated = EventosSesion.AceptarIngresoAsync,
            OnRemoteFailure = EventosSesion.InformarFalloRemotoAsync,
        };
    }
}
