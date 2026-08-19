using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Aplicacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Services;

namespace Modules.Designaciones.Api;

[ApiController]
[Route("api/designaciones/pedidos")]
[Authorize]
public sealed class PedidosController(IServicioPedidosApi servicio) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permisos.DesignacionesVer)]
    public Task<IReadOnlyList<PedidoDto>> Listar([FromQuery] Guid? periodoId, CancellationToken ct) =>
        servicio.ListarAsync(periodoId, ct);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permisos.DesignacionesVer)]
    public Task<PedidoDto> Obtener(Guid id, CancellationToken ct) => servicio.ObtenerAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = Permisos.DesignacionesGestionar)]
    public async Task<ActionResult<PedidoDto>> Crear(GuardarPedidoDto datos, CancellationToken ct)
    {
        var creado = await servicio.CrearAsync(datos, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permisos.DesignacionesGestionar)]
    public Task<PedidoDto> Editar(Guid id, GuardarPedidoDto datos, CancellationToken ct) =>
        servicio.EditarAsync(id, datos, ct);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permisos.DesignacionesGestionar)]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
    {
        await servicio.EliminarAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/enviar")]
    [Authorize(Policy = Permisos.DesignacionesGestionar)]
    public Task<PedidoDto> Enviar(
        Guid id,
        [FromHeader(Name = "Idempotency-Key")] string? clave,
        CancellationToken ct) =>
        servicio.AplicarAccionIdempotenteAsync(
            id, new AccionPedido.Enviar(), RequerirClave(clave), "enviar", string.Empty, ct);

    [HttpPost("{id:guid}/reenviar")]
    [Authorize(Policy = Permisos.DesignacionesGestionar)]
    public Task<PedidoDto> Reenviar(
        Guid id,
        [FromHeader(Name = "Idempotency-Key")] string? clave,
        CancellationToken ct) =>
        servicio.AplicarAccionIdempotenteAsync(
            id, new AccionPedido.Reenviar(), RequerirClave(clave), "reenviar", string.Empty, ct);

    [HttpPost("{id:guid}/aceptar")]
    [Authorize(Policy = Permisos.DesignacionesRevisar)]
    public Task<PedidoDto> Aceptar(
        Guid id,
        AccionPedidoDto datos,
        [FromHeader(Name = "Idempotency-Key")] string? clave,
        CancellationToken ct) =>
        servicio.AplicarAccionIdempotenteAsync(
            id, new AccionPedido.Aceptar(datos.Comentario), RequerirClave(clave),
            "aceptar", datos.Comentario ?? string.Empty, ct);

    [HttpPost("{id:guid}/rechazar")]
    [Authorize(Policy = Permisos.DesignacionesRevisar)]
    public Task<PedidoDto> Rechazar(
        Guid id,
        AccionPedidoDto datos,
        [FromHeader(Name = "Idempotency-Key")] string? clave,
        CancellationToken ct) =>
        servicio.AplicarAccionIdempotenteAsync(
            id, new AccionPedido.Rechazar(datos.Comentario ?? string.Empty), RequerirClave(clave),
            "rechazar", datos.Comentario ?? string.Empty, ct);

    [HttpPost("{id:guid}/devolver")]
    [Authorize(Policy = Permisos.DesignacionesRevisar)]
    public Task<PedidoDto> Devolver(
        Guid id,
        AccionPedidoDto datos,
        [FromHeader(Name = "Idempotency-Key")] string? clave,
        CancellationToken ct) =>
        servicio.AplicarAccionIdempotenteAsync(
            id, new AccionPedido.Devolver(datos.Comentario ?? string.Empty), RequerirClave(clave),
            "devolver", datos.Comentario ?? string.Empty, ct);

    [HttpPost("{id:guid}/priorizar")]
    [Authorize(Policy = Permisos.DesignacionesVer)]
    public Task<PedidoDto> Priorizar(
        Guid id,
        AccionPedidoDto datos,
        [FromHeader(Name = "Idempotency-Key")] string? clave,
        CancellationToken ct) =>
        servicio.AplicarAccionIdempotenteAsync(
            id, new AccionPedido.Priorizar(datos.Comentario ?? string.Empty), RequerirClave(clave),
            "priorizar", datos.Comentario ?? string.Empty, ct);

    [HttpPost("{id:guid}/despriorizar")]
    [Authorize(Policy = Permisos.DesignacionesVer)]
    public Task<PedidoDto> Despriorizar(
        Guid id,
        AccionPedidoDto datos,
        [FromHeader(Name = "Idempotency-Key")] string? clave,
        CancellationToken ct) =>
        servicio.AplicarAccionIdempotenteAsync(
            id, new AccionPedido.Despriorizar(datos.Comentario), RequerirClave(clave),
            "despriorizar", datos.Comentario ?? string.Empty, ct);

    private static Guid RequerirClave(string? valor)
    {
        if (Guid.TryParse(valor, out var clave)) return clave;
        throw new ExcepcionAplicacion(
            TipoErrorAplicacion.Validacion,
            "idempotency-key-required",
            "Idempotency-Key debe contener un UUID válido.");
    }
}
