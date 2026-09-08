using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Designaciones.Services;

namespace Modules.Designaciones.Api;

[ApiController]
[Route("api/designaciones/periodos")]
[Authorize(Policy = Permisos.DesignacionesVer)]
public sealed class LoteController(IServicioLoteDesignaciones servicio) : ControllerBase
{
    [HttpGet("{id:guid}/lote.xlsx")]
    public async Task<IActionResult> Exportar(Guid id, CancellationToken ct)
    {
        var archivo = await servicio.ExportarAsync(id, ct);
        return File(
            archivo.Contenido,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            archivo.NombreArchivo);
    }
}
