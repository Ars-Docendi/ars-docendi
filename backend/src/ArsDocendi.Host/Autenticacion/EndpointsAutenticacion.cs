using System.Security.Claims;
using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using ArsDocendi.Shared.Identity.Ingreso;

namespace ArsDocendi.Host.Autenticacion;

/// <summary>
/// Sesión vigente tal como la consume el frontend. <c>RequiereElegirRol</c> indica
/// que la persona, con varias asignaciones, todavía no eligió: opera con la de por
/// defecto y el frontend le ofrece elegir.
/// </summary>
public sealed record SesionDto(
    Guid UsuarioId,
    string NombreParaMostrar,
    string Upn,
    string RolCodigo,
    string RolNombre,
    IReadOnlyList<string> Permisos,
    AsignacionSesion Asignacion,
    IReadOnlyList<AsignacionSesion> Asignaciones,
    bool RequiereElegirRol)
{
    /// <summary>A partir del principal ya resuelto en la solicitud.</summary>
    public static async Task<SesionDto?> DeAsync(ClaimsPrincipal usuario, ServicioSesion servicio, CancellationToken ct)
    {
        var sesion = await servicio.ObtenerAsync(
            Guid.Parse(usuario.FindFirstValue(ClaimTypes.NameIdentifier)!),
            Guid.TryParse(usuario.FindFirstValue(ClaimsSesion.Asignacion), out var id) ? id : null,
            ct);
        return sesion is null ? null : Desde(sesion, usuario.HasClaim(c => c.Type == ClaimsSesion.AsignacionElegida));
    }

    public static SesionDto Desde(IdentidadSesion sesion, bool elegida) => new(
        sesion.UsuarioId,
        sesion.NombreParaMostrar,
        sesion.Upn,
        sesion.Asignacion.RolCodigo,
        sesion.Asignacion.RolNombre,
        sesion.Permisos,
        sesion.Asignacion,
        sesion.Asignaciones,
        !elegida);
}

public sealed record ElegirAsignacionDto(Guid AsignacionId);

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

        var sesionPorCookie = new AuthorizeAttribute { AuthenticationSchemes = RegistroAutenticacion.EsquemaSesion };

        grupo.MapGet("/sesion", async (ClaimsPrincipal usuario, ServicioSesion servicio, CancellationToken ct) =>
                await SesionDto.DeAsync(usuario, servicio, ct) is { } sesion
                    ? Results.Ok(sesion)
                    : Results.Unauthorized())
            .RequireAuthorization(sesionPorCookie)
            .WithName("ObtenerSesion");

        // Elige o cambia la asignación de la sesión sin volver a ingresar. Conserva el
        // inicio de la sesión para no extender su duración máxima.
        grupo.MapPost("/asignacion", async (
                ElegirAsignacionDto datos,
                HttpContext contexto,
                ServicioSesion servicio,
                CancellationToken ct) =>
            {
                var usuario = contexto.User;
                var sesion = await servicio.ObtenerAsync(UsuarioId(usuario), datos.AsignacionId, ct);
                if (sesion?.Asignacion.Id != datos.AsignacionId) return Results.Forbid();

                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, sesion.UsuarioId.ToString()),
                    new(EventosSesion.ClaimInicio, usuario.FindFirstValue(EventosSesion.ClaimInicio)!),
                    new(ClaimsSesion.Asignacion, datos.AsignacionId.ToString()),
                };
                await contexto.SignInAsync(
                    RegistroAutenticacion.EsquemaSesion,
                    new ClaimsPrincipal(new ClaimsIdentity(claims, RegistroAutenticacion.EsquemaSesion)));
                return Results.Ok(SesionDto.Desde(sesion, elegida: true));
            })
            .RequireAuthorization(sesionPorCookie)
            .WithName("ElegirAsignacion");

        grupo.MapPost("/logout", async (HttpContext contexto) =>
            {
                await contexto.SignOutAsync(RegistroAutenticacion.EsquemaSesion);
                return Results.NoContent();
            })
            .AllowAnonymous()
            .WithName("CerrarSesion");

        return endpoints;
    }

    private static Guid UsuarioId(ClaimsPrincipal usuario) =>
        Guid.Parse(usuario.FindFirstValue(ClaimTypes.NameIdentifier)!);

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
