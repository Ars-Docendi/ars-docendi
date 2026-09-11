using System.Security.Claims;
using System.Text.Encodings.Web;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity.Desarrollo;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ArsDocendi.Host.Desarrollo;

public sealed class AutenticacionDesarrolloHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> opciones,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ServicioIdentidadesDesarrollo identidades)
    : AuthenticationHandler<AuthenticationSchemeOptions>(opciones, logger, encoder)
{
    public const string Esquema = "IdentidadDesarrollo";
    public const string HeaderUsuario = "X-Dev-User-Id";
    public const string HeaderRol = "X-Dev-Role-Code";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var usuarioTexto = Request.Headers[HeaderUsuario].ToString();
        var rol = Request.Headers[HeaderRol].ToString();
        if (string.IsNullOrWhiteSpace(usuarioTexto) || string.IsNullOrWhiteSpace(rol))
        {
            return AuthenticateResult.NoResult();
        }
        if (!Guid.TryParse(usuarioTexto, out var usuarioId))
        {
            return AuthenticateResult.Fail("La identidad de desarrollo no es válida.");
        }

        var identidad = await identidades.ValidarAsync(usuarioId, rol, Context.RequestAborted);
        if (identidad is null)
        {
            return AuthenticateResult.Fail("La identidad o el rol de desarrollo no son elegibles.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, identidad.UsuarioId.ToString()),
            new(ClaimTypes.Name, identidad.NombreParaMostrar),
            new(ClaimTypes.Email, identidad.Upn),
            new(ClaimTypes.Role, identidad.RolCodigo),
        };
        claims.AddRange(identidad.Permisos.Select(p => new Claim(Permisos.Claim, p)));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Esquema));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Esquema));
    }
}
