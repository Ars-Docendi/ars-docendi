using System.Security.Claims;
using ArsDocendi.Host.Autenticacion;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity.Desarrollo;
using ArsDocendi.Shared.Identity.Ingreso;
using Microsoft.AspNetCore.Authorization;

namespace ArsDocendi.Host.Desarrollo;

public static class EndpointsDesarrollo
{
    public static IEndpointRouteBuilder MapIdentidadesDesarrollo(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/desarrollo/identidades", async (
            ServicioIdentidadesDesarrollo servicio,
            CancellationToken ct) => await servicio.ListarAsync(ct))
            .AllowAnonymous()
            .WithName("ListarIdentidadesDesarrollo");

        // Misma forma que /api/auth/sesion, para que el frontend trate igual ambos ingresos.
        endpoints.MapGet("/api/desarrollo/sesion", async (
                ClaimsPrincipal usuario,
                ServicioSesion servicio,
                CancellationToken ct) =>
                await SesionDto.DeAsync(usuario, servicio, ct) is { } sesion
                    ? Results.Ok(sesion)
                    : Results.Unauthorized())
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = AutenticacionDesarrolloHandler.Esquema,
            })
            .WithName("ObtenerSesionDesarrollo");
        return endpoints;
    }
}
