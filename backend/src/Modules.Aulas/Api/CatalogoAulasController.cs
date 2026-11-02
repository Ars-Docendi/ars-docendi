using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Aulas.Services;

namespace Modules.Aulas.Api;

[ApiController]
[Route("api/aulas/catalogo")]
[Authorize]
public sealed class CatalogoAulasController(IServicioCatalogoAulas servicio) : ControllerBase
{
    /// <summary>
    /// Lo lee quien administra el catálogo y quien asigna aulas (<c>aulas.aprobar</c>), que
    /// necesita las activas para elegir.
    /// </summary>
    [HttpGet]
    public Task<IReadOnlyList<AulaDto>> Listar([FromQuery] bool activas, CancellationToken ct)
    {
        if (!User.HasClaim(Permisos.Claim, Permisos.AulasGestionar) &&
            !User.HasClaim(Permisos.Claim, Permisos.AulasAprobar))
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Prohibido,
                "forbidden",
                "No tenés permiso para ver el catálogo de aulas.");
        }
        return servicio.ListarAsync(activas, ct);
    }

    [HttpPost]
    [Authorize(Policy = Permisos.AulasGestionar)]
    public async Task<ActionResult<AulaDto>> Crear(GuardarAulaDto datos, CancellationToken ct)
    {
        var creada = await servicio.CrearAsync(datos, ct);
        return CreatedAtAction(nameof(Listar), creada);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permisos.AulasGestionar)]
    public Task<AulaDto> Editar(Guid id, GuardarAulaDto datos, CancellationToken ct) =>
        servicio.EditarAsync(id, datos, ct);

    [HttpPost("{id:guid}/activar")]
    [Authorize(Policy = Permisos.AulasGestionar)]
    public Task<AulaDto> Activar(Guid id, CambiarEstadoAulaDto datos, CancellationToken ct) =>
        servicio.CambiarEstadoAsync(id, true, datos.Version, ct);

    [HttpPost("{id:guid}/desactivar")]
    [Authorize(Policy = Permisos.AulasGestionar)]
    public Task<AulaDto> Desactivar(Guid id, CambiarEstadoAulaDto datos, CancellationToken ct) =>
        servicio.CambiarEstadoAsync(id, false, datos.Version, ct);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permisos.AulasGestionar)]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
    {
        await servicio.EliminarAsync(id, ct);
        return NoContent();
    }
}
