using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Designaciones.Services;

namespace Modules.Designaciones.Api;

[ApiController]
[Route("api/designaciones/periodos")]
[Authorize(Policy = Permisos.PeriodosAdministrar)]
public sealed class PeriodosController(ServicioPeriodos servicio) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<PeriodoDto>> Listar(CancellationToken ct) => servicio.ListarAsync(ct);

    [HttpGet("{id:guid}")]
    public Task<PeriodoDto> Obtener(Guid id, CancellationToken ct) => servicio.ObtenerAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<PeriodoDto>> Crear(GuardarPeriodoDto datos, CancellationToken ct)
    {
        var creado = await servicio.CrearAsync(datos, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:guid}")]
    public Task<PeriodoDto> Editar(Guid id, GuardarPeriodoDto datos, CancellationToken ct) =>
        servicio.EditarAsync(id, datos, ct);

    [HttpPost("{id:guid}/activar")]
    public Task<PeriodoDto> Activar(Guid id, CambiarEstadoPeriodoDto datos, CancellationToken ct) =>
        servicio.CambiarEstadoAsync(id, true, datos.Version, ct);

    [HttpPost("{id:guid}/desactivar")]
    public Task<PeriodoDto> Desactivar(Guid id, CambiarEstadoPeriodoDto datos, CancellationToken ct) =>
        servicio.CambiarEstadoAsync(id, false, datos.Version, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
    {
        await servicio.EliminarAsync(id, ct);
        return NoContent();
    }
}
