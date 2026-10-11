using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Tareas.Application;

namespace Modules.Tareas.Api;

/// <summary>
/// Tareas. Ver y participar (estado/avance del Responsable, comentarios, relaciones) requiere
/// `tareas.ver`, que tienen todos los roles; crear y editar campos requiere `tareas.gestionar`.
/// Las reglas finas (autoridad creadora, Responsable, jerarquía) las aplica el servicio.
/// </summary>
[ApiController]
[Route("api/tareas")]
public sealed class TareasController : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { module = "tareas", status = "ok" });

    [Authorize(Policy = Permisos.TareasVer)]
    [HttpGet]
    public Task<IReadOnlyList<TareaDto>> Listar(ServicioTareas servicio, CancellationToken ct) =>
        servicio.ListarAsync(ct);

    [Authorize(Policy = Permisos.TareasVer)]
    [HttpGet("{id:guid}")]
    public Task<TareaDto> Obtener(Guid id, ServicioTareas servicio, CancellationToken ct) =>
        servicio.ObtenerAsync(id, ct);

    [Authorize(Policy = Permisos.TareasGestionar)]
    [HttpGet("candidatos")]
    public Task<IReadOnlyList<CandidatoResponsableDto>> Candidatos(
        ServicioTareas servicio, CancellationToken ct, [FromQuery] string? para = null, [FromQuery] string? q = null) =>
        servicio.ListarCandidatosAsync(para == "proyecto", q, ct);

    [Authorize(Policy = Permisos.TareasGestionar)]
    [HttpPost]
    public async Task<ActionResult<TareaDto>> Crear(CrearTareaRequest datos, ServicioTareas servicio, CancellationToken ct)
    {
        var tarea = await servicio.CrearAsync(datos, ct);
        return Created($"/api/tareas/{tarea.Id}", tarea);
    }

    [Authorize(Policy = Permisos.TareasGestionar)]
    [HttpPut("{id:guid}")]
    public Task<TareaDto> Editar(Guid id, EditarTareaRequest datos, ServicioTareas servicio, CancellationToken ct) =>
        servicio.EditarAsync(id, datos, ct);

    [Authorize(Policy = Permisos.TareasVer)]
    [HttpPost("{id:guid}/estado")]
    public Task<TareaDto> CambiarEstado(
        Guid id, CambiarEstadoTareaRequest datos, ServicioTareas servicio, CancellationToken ct) =>
        servicio.CambiarEstadoAsync(id, datos, ct);

    [Authorize(Policy = Permisos.TareasVer)]
    [HttpPatch("{id:guid}/avance")]
    public Task<TareaDto> EditarAvance(Guid id, EditarAvanceRequest datos, ServicioTareas servicio, CancellationToken ct) =>
        servicio.EditarAvanceAsync(id, datos, ct);

    [Authorize(Policy = Permisos.TareasVer)]
    [HttpPost("{id:guid}/comentarios")]
    public async Task<ActionResult<TareaDto>> Comentar(
        Guid id, ComentarTareaRequest datos, ServicioTareas servicio, CancellationToken ct) =>
        Created($"/api/tareas/{id}", await servicio.ComentarAsync(id, datos, ct));

    [Authorize(Policy = Permisos.TareasVer)]
    [HttpPost("{id:guid}/relaciones")]
    public async Task<IActionResult> Relacionar(
        Guid id, RelacionarTareaRequest datos, ServicioTareas servicio, CancellationToken ct)
    {
        await servicio.RelacionarAsync(id, datos.OtraTareaId, ct);
        return NoContent();
    }

    [Authorize(Policy = Permisos.TareasVer)]
    [HttpDelete("{id:guid}/relaciones/{otraId:guid}")]
    public async Task<IActionResult> QuitarRelacion(Guid id, Guid otraId, ServicioTareas servicio, CancellationToken ct)
    {
        await servicio.QuitarRelacionAsync(id, otraId, ct);
        return NoContent();
    }
}
