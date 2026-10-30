using System.Security.Claims;
using System.Text.Encodings.Web;
using ArsDocendi.Host.Autenticacion;
using ArsDocendi.Shared.Identity.Desarrollo;
using ArsDocendi.Shared.Identity.Ingreso;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ArsDocendi.Host.Desarrollo;

/// <summary>
/// Ingreso de desarrollo por headers: la identidad sembrada y, una vez elegida, la
/// asignación de rol. Rol, ámbito y permisos se resuelven igual que en la sesión con
/// Microsoft; sin asignación elegida opera con la de por defecto.
/// </summary>
public sealed class AutenticacionDesarrolloHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> opciones,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ServicioIdentidadesDesarrollo identidades,
    ServicioSesion sesiones)
    : AuthenticationHandler<AuthenticationSchemeOptions>(opciones, logger, encoder)
{
    public const string Esquema = "IdentidadDesarrollo";
    public const string HeaderUsuario = "X-Dev-User-Id";
    public const string HeaderAsignacion = "X-Dev-Asignacion-Id";
    /// <summary>
    /// Atajo para tests y herramientas: elige la asignación por código de rol (la
    /// primera, si hay varias de ese rol). El frontend usa <see cref="HeaderAsignacion"/>.
    /// </summary>
    public const string HeaderRol = "X-Dev-Role-Code";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var usuarioTexto = Request.Headers[HeaderUsuario].ToString();
        if (string.IsNullOrWhiteSpace(usuarioTexto))
        {
            return AuthenticateResult.NoResult();
        }
        var asignacionTexto = Request.Headers[HeaderAsignacion].ToString();
        Guid? asignacionId = Guid.TryParse(asignacionTexto, out var elegida) ? elegida : null;
        if (!Guid.TryParse(usuarioTexto, out var usuarioId)
            || (asignacionTexto.Length > 0 && asignacionId is null))
        {
            return AuthenticateResult.Fail("La identidad de desarrollo no es válida.");
        }

        var sesion = await identidades.EsElegibleAsync(usuarioId, Context.RequestAborted)
            ? await sesiones.ObtenerAsync(usuarioId, asignacionId, Context.RequestAborted)
            : null;
        var rol = Request.Headers[HeaderRol].ToString();
        if (sesion is not null && asignacionId is null && rol.Length > 0)
        {
            var delRol = sesion.Asignaciones.FirstOrDefault(a => a.RolCodigo == rol);
            sesion = delRol is null
                ? null
                : await sesiones.ObtenerAsync(usuarioId, delRol.Id, Context.RequestAborted);
        }
        if (sesion is null)
        {
            return AuthenticateResult.Fail("La identidad de desarrollo no es elegible.");
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(EventosSesion.ClaimsDe(sesion), Esquema));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Esquema));
    }
}
