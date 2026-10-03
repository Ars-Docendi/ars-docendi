using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity.Ingreso;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace ArsDocendi.Host.Autenticacion;

/// <summary>
/// Eventos del ingreso con Microsoft y de la cookie de sesión. La cookie sólo guarda
/// quién es el usuario y cuándo ingresó; rol y permisos se leen de identity en cada
/// solicitud, así que desactivar o cambiar el rol aplica en la solicitud siguiente.
/// </summary>
public static partial class EventosSesion
{
    public const string ClaimInicio = "ars:inicio";
    public const string ClaimRolNombre = "ars:rol-nombre";

    /// <summary>Después de validar el id_token: aplica la regla de ingreso y arma la sesión.</summary>
    public static async Task AceptarIngresoAsync(TokenValidatedContext contexto)
    {
        var servicios = contexto.HttpContext.RequestServices;
        var cuenta = LeerCuenta(contexto.Principal);
        var resultado = cuenta is null
            ? ResultadoIngreso.Rechazar(MotivoRechazoIngreso.MailNoVerificado)
            : await servicios.GetRequiredService<ServicioIngreso>()
                .ResolverAsync(cuenta, contexto.HttpContext.RequestAborted);

        if (!resultado.Aceptado)
        {
            contexto.HandleResponse();
            contexto.Response.Redirect("/login?error=forbidden");
            return;
        }

        var ahora = servicios.GetRequiredService<TimeProvider>().GetUtcNow();
        var identidad = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, resultado.UsuarioId!.Value.ToString()),
                new Claim(ClaimInicio, ahora.ToString("O", CultureInfo.InvariantCulture)),
            ],
            RegistroAutenticacion.EsquemaSesion);
        contexto.Principal = new ClaimsPrincipal(identidad);

        // El vínculo y el último ingreso se auditan con el propio usuario como actor.
        contexto.HttpContext.User = contexto.Principal;
        await servicios.GetRequiredService<ServicioIngreso>()
            .RegistrarIngresoAsync(resultado.UsuarioId.Value, cuenta!, contexto.HttpContext.RequestAborted);
    }

    /// <summary>Microsoft devolvió un error o la persona canceló el ingreso.</summary>
    public static Task InformarFalloRemotoAsync(RemoteFailureContext contexto)
    {
        // El mensaje de Microsoft puede incluir el mail de la cuenta: sólo se registra el código.
        var codigo = CodigoMicrosoft().Match(contexto.Failure?.Message ?? string.Empty);
        contexto.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(EventosSesion))
            .LogWarning(
                "Ingreso con Microsoft fallido: {Codigo}",
                codigo.Success ? codigo.Value : "sin código");
        contexto.HandleResponse();
        contexto.Response.Redirect("/login?error=auth");
        return Task.CompletedTask;
    }

    /// <summary>
    /// En cada solicitud con cookie: rechaza la sesión que superó su duración máxima o
    /// la de un usuario inactivo o sin rol y, si sigue vigente, reemplaza el principal
    /// por rol y permisos actuales. El reemplazo no se persiste en la cookie. La
    /// inactividad la vence el propio handler de cookies (expiración deslizante).
    /// </summary>
    public static async Task ValidarAsync(CookieValidatePrincipalContext contexto)
    {
        var usuarioTexto = contexto.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var inicio = contexto.Principal?.FindFirstValue(ClaimInicio);
        IdentidadSesion? sesion = null;
        if (Guid.TryParse(usuarioTexto, out var usuarioId) && DentroDeLaDuracionMaxima(contexto, inicio))
        {
            sesion = await contexto.HttpContext.RequestServices
                .GetRequiredService<ServicioSesion>()
                .ObtenerAsync(usuarioId, contexto.HttpContext.RequestAborted);
        }

        if (sesion is null)
        {
            contexto.RejectPrincipal();
            await contexto.HttpContext.SignOutAsync(RegistroAutenticacion.EsquemaSesion);
            return;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, sesion.UsuarioId.ToString()),
            new(ClaimTypes.Name, sesion.NombreParaMostrar),
            new(ClaimTypes.Email, sesion.Upn),
            new(ClaimTypes.Role, sesion.RolCodigo),
            new(ClaimRolNombre, sesion.RolNombre),
            new(ClaimInicio, inicio!),
        };
        claims.AddRange(sesion.Permisos.Select(p => new Claim(Permisos.Claim, p)));
        contexto.ReplacePrincipal(new ClaimsPrincipal(
            new ClaimsIdentity(claims, RegistroAutenticacion.EsquemaSesion)));
    }

    private static bool DentroDeLaDuracionMaxima(CookieValidatePrincipalContext contexto, string? inicio)
    {
        if (!DateTimeOffset.TryParse(inicio, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var desde))
        {
            return false;
        }

        var horasMaximas = contexto.HttpContext.RequestServices
            .GetRequiredService<IOptions<AutenticacionMicrosoftOptions>>().Value.HorasMaximas;
        return contexto.Options.TimeProvider!.GetUtcNow() < desde.AddHours(horasMaximas);
    }

    private static DatosCuentaMicrosoft? LeerCuenta(ClaimsPrincipal? principal)
    {
        if (!Guid.TryParse(principal?.FindFirstValue("tid"), out var tenant)
            || !Guid.TryParse(principal?.FindFirstValue("oid"), out var objeto))
        {
            return null;
        }

        var verificado = principal!.FindFirstValue("xms_edov");
        return new DatosCuentaMicrosoft(
            tenant,
            objeto,
            principal.FindFirstValue("email"),
            verificado is null ? null : verificado is "true" or "True" or "1");
    }

    [GeneratedRegex(@"AADSTS\d+")]
    private static partial Regex CodigoMicrosoft();
}
