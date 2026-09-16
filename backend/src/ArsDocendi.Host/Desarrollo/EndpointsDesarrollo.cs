using ArsDocendi.Shared.Identity.Desarrollo;

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
        return endpoints;
    }
}
