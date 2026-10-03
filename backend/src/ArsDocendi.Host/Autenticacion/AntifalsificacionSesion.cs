using Microsoft.AspNetCore.Antiforgery;

namespace ArsDocendi.Host.Autenticacion;

/// <summary>
/// Protección contra solicitudes cruzadas para la sesión por cookie. SameSite no
/// alcanza: pr-N, staging y producción comparten sitio, así que una página de un
/// ambiente hermano puede disparar solicitudes que viajan con la cookie.
/// <para>
/// En cada GET autenticado por cookie se entrega <c>XSRF-TOKEN</c>, legible por el
/// frontend; axios lo devuelve en <c>X-XSRF-TOKEN</c> en las solicitudes al mismo
/// origen. Las mutaciones autenticadas por cookie sin ese token se rechazan. Las
/// identidades de desarrollo viajan en headers y no son vulnerables a CSRF.
/// </para>
/// </summary>
public static class AntifalsificacionSesion
{
    public const string CookieToken = "XSRF-TOKEN";
    public const string HeaderToken = "X-XSRF-TOKEN";

    private static readonly string[] MetodosSeguros = ["GET", "HEAD", "OPTIONS", "TRACE"];

    public static IServiceCollection AddAntifalsificacionSesion(this IServiceCollection servicios) =>
        servicios.AddAntiforgery(opciones =>
        {
            opciones.HeaderName = HeaderToken;
            // Secure según el request: en local se corre por http://localhost y en los
            // despliegues los headers de proxy hacen que el request se vea como https.
            // Sin Secure garantizado no puede llevar el prefijo __Host-.
            opciones.Cookie.Name = "ars-af";
            opciones.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            opciones.Cookie.SameSite = SameSiteMode.Strict;
        });

    /// <summary>Va después de <c>UseAuthentication</c>: el token queda ligado al usuario de la sesión.</summary>
    public static IApplicationBuilder UseAntifalsificacionSesion(this IApplicationBuilder app) =>
        app.Use(async (contexto, siguiente) =>
        {
            if (contexto.User.Identity is not { IsAuthenticated: true, AuthenticationType: RegistroAutenticacion.EsquemaSesion })
            {
                await siguiente();
                return;
            }

            var antiforgery = contexto.RequestServices.GetRequiredService<IAntiforgery>();
            if (MetodosSeguros.Contains(contexto.Request.Method, StringComparer.OrdinalIgnoreCase))
            {
                var tokens = antiforgery.GetAndStoreTokens(contexto);
                contexto.Response.Cookies.Append(CookieToken, tokens.RequestToken!, new CookieOptions
                {
                    HttpOnly = false,
                    Secure = contexto.Request.IsHttps,
                    SameSite = SameSiteMode.Strict,
                    Path = "/",
                });
                await siguiente();
                return;
            }

            if (!await antiforgery.IsRequestValidAsync(contexto))
            {
                await Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "La solicitud no trae una prueba anti-falsificación válida.")
                    .ExecuteAsync(contexto);
                return;
            }

            await siguiente();
        });
}
