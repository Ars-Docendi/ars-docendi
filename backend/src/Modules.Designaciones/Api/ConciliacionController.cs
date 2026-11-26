using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Designaciones.Services;

namespace Modules.Designaciones.Api;

/// <summary>
/// Conciliación del lote del período activo con la rectificación. El rechazo de un
/// pedido en la conciliación es <c>POST /api/designaciones/pedidos/{id}/no-aprobar</c>.
/// </summary>
[ApiController]
[Route("api/designaciones/conciliacion")]
[Authorize(Policy = Permisos.DesignacionesConciliar)]
public sealed class ConciliacionController(IServicioConciliacion servicio) : ControllerBase
{
    [HttpGet]
    public Task<ConciliacionDto> Consultar(CancellationToken ct) => servicio.ConsultarAsync(ct);

    [HttpPost("sincronizar")]
    public Task<ResultadoSincronizacionDto> Sincronizar(
        SincronizarConciliacionDto datos,
        [FromHeader(Name = "Idempotency-Key")] string? clave,
        CancellationToken ct) =>
        servicio.SincronizarAsync(datos, PedidosController.RequerirClave(clave), ct);
}
