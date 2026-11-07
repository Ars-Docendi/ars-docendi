using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Designaciones.Services;

namespace Modules.Designaciones.Api;

[ApiController]
[Route("api/designaciones/catalogos")]
[Authorize(Policy = Permisos.DesignacionesVer)]
public sealed class CatalogosController(ServicioCatalogosDesignaciones servicio) : ControllerBase
{
    /// <summary>Con <paramref name="pedidoId"/> (edición), la persona de ese pedido sigue siendo elegible.</summary>
    [HttpGet]
    public Task<CatalogosDesignacionesDto> Obtener([FromQuery] Guid? pedidoId, CancellationToken ct) =>
        servicio.ObtenerAsync(ct, pedidoId);

    /// <summary>
    /// Personas del sistema para un Alta de persona existente: por apellido, nombre, DNI o legajo,
    /// sin las que ya tienen un pedido vivo en el período activo. Hasta 20 resultados.
    /// </summary>
    [HttpGet("/api/designaciones/personas/buscar")]
    [Authorize(Policy = Permisos.DesignacionesGestionar)]
    public Task<IReadOnlyList<PersonaBusquedaDto>> BuscarPersonas([FromQuery] string? texto, CancellationToken ct) =>
        servicio.BuscarPersonasAsync(texto ?? string.Empty, ct);
}
