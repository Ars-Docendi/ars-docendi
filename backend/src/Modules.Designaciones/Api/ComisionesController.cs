using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Designaciones.Services;

namespace Modules.Designaciones.Api;

/// <summary>
/// Comisiones importadas desde Guaraní. Las consultas leen sólo el snapshot local; únicamente
/// los endpoints de importación contactan a Guaraní.
/// </summary>
[ApiController]
[Route("api/designaciones/comisiones")]
public sealed class ComisionesController(ServicioComisiones servicio) : ControllerBase
{
    [HttpGet("periodos")]
    [Authorize(Policy = Permisos.ComisionesVer)]
    public Task<IReadOnlyList<PeriodoImportadoDto>> ListarPeriodos(CancellationToken ct) =>
        servicio.ListarPeriodosAsync(ct);

    [HttpGet]
    [Authorize(Policy = Permisos.ComisionesVer)]
    public Task<IReadOnlyList<ComisionDto>> Listar([FromQuery] string periodoLectivoId, CancellationToken ct) =>
        servicio.ListarAsync(periodoLectivoId, ct);

    [HttpPost("exportacion")]
    [Authorize(Policy = Permisos.ComisionesVer)]
    public async Task<IActionResult> Exportar(ExportarComisionesDto datos, CancellationToken ct)
    {
        var archivo = await servicio.ExportarAsync(datos, ct);
        return File(
            archivo.Contenido,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            archivo.NombreArchivo);
    }

    [HttpGet("guarani/periodos-lectivos")]
    [Authorize(Policy = Permisos.ComisionesImportar)]
    public Task<IReadOnlyList<PeriodoLectivoGuaraniDto>> ListarPeriodosGuarani(CancellationToken ct) =>
        servicio.ListarPeriodosGuaraniAsync(ct);

    [HttpPost("importaciones")]
    [Authorize(Policy = Permisos.ComisionesImportar)]
    public Task<ResultadoImportacionDto> Importar(ImportarComisionesDto datos, CancellationToken ct) =>
        servicio.ImportarAsync(datos, ct);
}
