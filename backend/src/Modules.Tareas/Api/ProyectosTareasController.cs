using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Tareas.Application;

namespace Modules.Tareas.Api;

/// <summary>Proyectos de Tareas. Consultar requiere `tareas.ver`; crear y cambiar estado, `proyectos.gestionar`.</summary>
[ApiController]
[Route("api/tareas/proyectos")]
public sealed class ProyectosTareasController : ControllerBase
{
    [Authorize(Policy = Permisos.TareasVer)]
    [HttpGet]
    public Task<IReadOnlyList<ProyectoTareasDto>> Listar(ServicioProyectos servicio, CancellationToken ct) =>
        servicio.ListarAsync(ct);

    [Authorize(Policy = Permisos.TareasVer)]
    [HttpGet("{id:guid}")]
    public Task<ProyectoTareasDto> Obtener(Guid id, ServicioProyectos servicio, CancellationToken ct) =>
        servicio.ObtenerAsync(id, ct);

    [Authorize(Policy = Permisos.ProyectosGestionar)]
    [HttpPost]
    public async Task<ActionResult<ProyectoTareasDto>> Crear(
        CrearProyectoRequest datos, ServicioProyectos servicio, CancellationToken ct)
    {
        var proyecto = await servicio.CrearAsync(datos, ct);
        return Created($"/api/tareas/proyectos/{proyecto.Id}", proyecto);
    }

    [Authorize(Policy = Permisos.ProyectosGestionar)]
    [HttpPost("{id:guid}/estado")]
    public Task<ProyectoTareasDto> CambiarEstado(
        Guid id, CambiarEstadoProyectoRequest datos, ServicioProyectos servicio, CancellationToken ct) =>
        servicio.CambiarEstadoAsync(id, datos, ct);
}
