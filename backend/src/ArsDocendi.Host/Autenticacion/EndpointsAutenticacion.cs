using System.Security.Claims;
using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace ArsDocendi.Host.Autenticacion;

/// <summary>Sesión vigente tal como la consume el frontend.</summary>
public sealed record SesionDto(
    Guid UsuarioId,
    string NombreParaMostrar,
    string Upn,
    string RolCodigo,
    string RolNombre,
    IReadOnlyList<string> Permisos);

/// <summary>
/// Rutas del ingreso con Microsoft. Sólo se registran si está habilitado, así que con
/// el ingreso apagado no existen. El callback lo atiende el handler OIDC en
/// <see cref="RegistroAutenticacion.RutaCallback"/>.
/// </summary>
public static class EndpointsAutenticacion
{
    public const string RutaInicial = "/portal";

    public static IEndpointRouteBuilder MapAutenticacionMicrosoft(this IEndpointRouteBuilder endpoints)
    {
        var grupo = endpoints.MapGroup("/api/auth");

        grupo.MapGet("/login", (string? returnUrl) => Results.Challenge(
                new AuthenticationProperties { RedirectUri = RutaLocalODefecto(returnUrl) },
                [RegistroAutenticacion.EsquemaMicrosoft]))
            .AllowAnonymous()
            .WithName("IniciarIngresoMicrosoft");

        grupo.MapGet("/sesion", (ClaimsPrincipal usuario) => new SesionDto(
                Guid.Parse(usuario.FindFirstValue(ClaimTypes.NameIdentifier)!),
                usuario.FindFirstValue(ClaimTypes.Name)!,
                usuario.FindFirstValue(ClaimTypes.Email)!,
                usuario.FindFirstValue(ClaimTypes.Role)!,
                usuario.FindFirstValue(EventosSesion.ClaimRolNombre)!,
                usuario.FindAll(Permisos.Claim).Select(c => c.Value).ToArray()))
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = RegistroAutenticacion.EsquemaSesion,
            })
            .WithName("ObtenerSesion");

        grupo.MapPost("/logout", async (HttpContext contexto) =>
            {
                await contexto.SignOutAsync(RegistroAutenticacion.EsquemaSesion);
                return Results.NoContent();
            })
            .AllowAnonymous()
            .WithName("CerrarSesion");

        return endpoints;
    }

    /// <summary>
    /// Sólo rutas relativas al propio sitio: <c>//otro.sitio</c> o <c>/\otro.sitio</c>
    /// el navegador los interpreta como externos.
    /// </summary>
    public static string RutaLocalODefecto(string? ruta) =>
        ruta is { Length: > 0 }
        && ruta[0] == '/'
        && (ruta.Length == 1 || (ruta[1] != '/' && ruta[1] != '\\'))
        && !ruta.Any(char.IsControl)
            ? ruta
            : RutaInicial;
}
