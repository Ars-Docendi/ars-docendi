using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Modules.Asistente.Application;

namespace Modules.Asistente.Api;

/// <summary>
/// Lectura de soporte del historial AJENO (asistente-acceso-de-soporte-al-historial).
/// </summary>
/// <remarks>
/// Todo el controller exige <c>asistente.leer_historial_ajeno</c> — un permiso
/// propio, sembrado a NINGÚN rol (ni siquiera <c>sys_admin</c>), y distinto de
/// <c>asistente.consultar</c>: tener admisión al asistente NO alcanza para
/// leer el historial de otra persona.
///
/// <c>POST</c> y no <c>GET</c> en los dos endpoints, deliberadamente: la razón
/// obligatoria viaja en el cuerpo y nunca en la URL (design.md D10).
///
/// Ningún endpoint de acá ofrece re-ejecución ni filas de resultado — sólo
/// texto y momentos —, y ningún endpoint del módulo le dice al SUJETO que
/// alguien leyó su historial (design.md D11): eso no es un olvido, es la
/// decisión final del cliente.
/// </remarks>
[ApiController]
[Route("api/asistente/soporte/historial")]
[Authorize(Policy = Permisos.AsistenteLeerHistorialAjeno)]
public sealed class SoporteHistorialController(
    IConsultasDeAuditoriaDeSoporte consultas, ICurrentUser usuario) : ControllerBase
{
    /// <summary>Lista las conversaciones de otro actor. Audita como listado.</summary>
    [HttpPost("{actorId:guid}/listar")]
    public async Task<ActionResult<IReadOnlyList<ConversacionResumenDto>>> Listar(
        Guid actorId, RazonDto pedido, CancellationToken ct)
    {
        if (!ActorDeLaSesion(out var lector))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(pedido?.Razon))
        {
            return RazonFaltante();
        }

        var conversaciones = await consultas.ListarAsync(lector, actorId, pedido.Razon, ct);

        return Ok(conversaciones.Select(ConversacionResumenDto.De).ToList());
    }

    /// <summary>Lee una conversación de otro actor. Audita nombrándola.</summary>
    [HttpPost("{actorId:guid}/{hiloId:guid}/leer")]
    public async Task<ActionResult<ConversacionDetalleDto>> Leer(
        Guid actorId, Guid hiloId, RazonDto pedido, CancellationToken ct)
    {
        if (!ActorDeLaSesion(out var lector))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(pedido?.Razon))
        {
            return RazonFaltante();
        }

        var conversacion = await consultas.LeerAsync(lector, actorId, hiloId, pedido.Razon, ct);
        if (conversacion is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "La conversación no existe",
                Detail = "No existe, o no es una conversación de ese actor.",
                Status = StatusCodes.Status404NotFound,
            });
        }

        // SIEMPRE con SQL — sin un segundo gate de asistente.ver_consulta,
        // design.md D9: el permiso de soporte ya es el de diagnóstico.
        return Ok(ConversacionDetalleDto.De(conversacion, veLaConsulta: true));
    }

    private static ActionResult RazonFaltante() => new BadRequestObjectResult(new ProblemDetails
    {
        Title = "Falta la razón",
        Detail = "Todo acceso de soporte al historial ajeno exige una razón, y no puede ser sólo espacios.",
        Status = StatusCodes.Status400BadRequest,
    });

    private bool ActorDeLaSesion(out Guid actor) => Guid.TryParse(usuario.UserId, out actor);
}
