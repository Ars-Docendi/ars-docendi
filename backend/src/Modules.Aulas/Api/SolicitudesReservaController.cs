using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Aulas.Services;

namespace Modules.Aulas.Api;

[ApiController]
[Route("api/aulas/solicitudes")]
[Authorize]
public sealed class SolicitudesReservaController(IServicioSolicitudesAula servicio) : ControllerBase
{
    [HttpGet("materias-propias")]
    [Authorize(Policy = Permisos.AulasSolicitar)]
    public Task<IReadOnlyList<MateriaOpcionDto>> ListarMateriasPropias(CancellationToken ct) =>
        servicio.ListarMateriasPropiasAsync(ct);

    [HttpGet("mias")]
    [Authorize(Policy = Permisos.AulasSolicitar)]
    public Task<IReadOnlyList<SolicitudReservaAulaDto>> ListarMias(CancellationToken ct) =>
        servicio.ListarMiasAsync(ct);

    [HttpPost]
    [Authorize(Policy = Permisos.AulasSolicitar)]
    public async Task<ActionResult<SolicitudReservaAulaDto>> Crear(
        CrearSolicitudReservaAulaDto datos, CancellationToken ct)
    {
        var creada = await servicio.CrearAsync(datos, ct);
        return CreatedAtAction(nameof(ListarMias), creada);
    }

    [HttpPost("{id:guid}/cancelar")]
    [Authorize(Policy = Permisos.AulasSolicitar)]
    public async Task<IActionResult> Cancelar(Guid id, CancellationToken ct)
    {
        await servicio.CancelarAsync(id, ct);
        return NoContent();
    }

    [HttpGet]
    [Authorize(Policy = Permisos.AulasAprobar)]
    public Task<IReadOnlyList<SolicitudReservaAulaDto>> ListarTodas(CancellationToken ct) =>
        servicio.ListarTodasAsync(ct);

    [HttpPost("{id:guid}/asignar")]
    [Authorize(Policy = Permisos.AulasAprobar)]
    public Task<SolicitudReservaAulaDto> Asignar(Guid id, AsignarAulaDto datos, CancellationToken ct) =>
        servicio.AsignarAulaAsync(id, datos, ct);

    [HttpPost("{id:guid}/rechazar")]
    [Authorize(Policy = Permisos.AulasAprobar)]
    public Task<SolicitudReservaAulaDto> Rechazar(
        Guid id, RechazarSolicitudDto datos, CancellationToken ct) =>
        servicio.RechazarAsync(id, datos, ct);
}
