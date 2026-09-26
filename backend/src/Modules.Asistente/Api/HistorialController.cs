using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Modules.Asistente.Application;

namespace Modules.Asistente.Api;

/// <summary>
/// El historial PROPIO del actor: listar, buscar, ver, renombrar, borrar,
/// reanudar y volver a consultar un turno ya respondido
/// (asistente-historial-conversaciones).
/// </summary>
/// <remarks>
/// Todo acotado al actor de la sesión, nunca a un id que venga del cliente:
/// mismo criterio que <see cref="AsistenteController"/>. Un hilo que no existe
/// o que pertenece a otro actor da 404 en los dos casos — igual que el hilo
/// ajeno del turno en vivo — para no confirmar cuál de los dos fue.
/// </remarks>
[ApiController]
[Route("api/asistente/historial")]
[Authorize(Policy = Permisos.AsistenteConsultar)]
public sealed class HistorialController(
    IConsultasDeHistorial consultas,
    IAlmacenDeHilos hilos,
    IPerfilDelActor perfiles,
    IEjecutorDeConsulta ejecutor,
    IBuscadorDeMenciones menciones,
    ICurrentUser usuario) : ControllerBase
{
    /// <summary>Las conversaciones propias, opcionalmente filtradas por texto.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConversacionResumenDto>>> Listar(
        [FromQuery] string? q, CancellationToken ct)
    {
        if (!ActorDeLaSesion(out var actor))
        {
            return Unauthorized();
        }

        var conversaciones = await consultas.ListarAsync(actor, q, ct);

        return Ok(conversaciones.Select(ConversacionResumenDto.De).ToList());
    }

    /// <summary>Una conversación propia, con sus turnos.</summary>
    [HttpGet("{hiloId:guid}")]
    public async Task<ActionResult<ConversacionDetalleDto>> Obtener(Guid hiloId, CancellationToken ct)
    {
        if (!ActorDeLaSesion(out var actor))
        {
            return Unauthorized();
        }

        var conversacion = await consultas.ObtenerAsync(actor, hiloId, ct);
        if (conversacion is null)
        {
            return ConversacionNoEncontrada();
        }

        var perfil = await perfiles.ObtenerAsync(actor, ct);

        // Las menciones de cada turno se re-resuelven para ESTE actor (el que lee
        // ahora, siempre el propio dueño acá) — nunca para el que hizo la pregunta
        // originalmente (design.md D11 de asistente-rediseno-v3, decisión 15 del PO).
        return Ok(await ConversacionDetalleDto.DeAsync(conversacion, perfil.VeLaConsulta, actor, menciones, ct));
    }

    /// <summary>Renombra una conversación propia.</summary>
    [HttpPatch("{hiloId:guid}")]
    public async Task<IActionResult> Renombrar(
        Guid hiloId, RenombrarConversacionDto pedido, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        if (!ActorDeLaSesion(out var actor))
        {
            return Unauthorized();
        }

        var renombrada = await consultas.RenombrarAsync(actor, hiloId, pedido.Titulo, ct);

        return renombrada ? NoContent() : ConversacionNoEncontrada();
    }

    /// <summary>Archiva una conversación propia.</summary>
    [HttpPost("{hiloId:guid}/archivar")]
    public async Task<IActionResult> Archivar(Guid hiloId, CancellationToken ct)
    {
        if (!ActorDeLaSesion(out var actor))
        {
            return Unauthorized();
        }

        var archivada = await consultas.ArchivarAsync(actor, hiloId, ct);

        return archivada ? NoContent() : ConversacionNoEncontrada();
    }

    /// <summary>Desarchiva una conversación propia.</summary>
    [HttpPost("{hiloId:guid}/desarchivar")]
    public async Task<IActionResult> Desarchivar(Guid hiloId, CancellationToken ct)
    {
        if (!ActorDeLaSesion(out var actor))
        {
            return Unauthorized();
        }

        var desarchivada = await consultas.DesarchivarAsync(actor, hiloId, ct);

        return desarchivada ? NoContent() : ConversacionNoEncontrada();
    }

    /// <summary>
    /// Marca una conversación propia como pendiente de borrado (design.md D4):
    /// desaparece de inmediato de todo endpoint propio, y puede deshacerse
    /// dentro de su ventana con <see cref="DeshacerBorrado"/>.
    /// </summary>
    [HttpDelete("{hiloId:guid}")]
    public async Task<ActionResult<LoteDeBorradoDto>> Eliminar(Guid hiloId, CancellationToken ct)
    {
        if (!ActorDeLaSesion(out var actor))
        {
            return Unauthorized();
        }

        var lote = await consultas.EliminarAsync(actor, hiloId, ct);

        return lote is null ? ConversacionNoEncontrada() : Ok(new LoteDeBorradoDto(lote.Value));
    }

    /// <summary>
    /// Marca TODAS las conversaciones propias (archivadas incluidas) como
    /// pendientes de borrado, con un lote nuevo.
    /// </summary>
    [HttpDelete]
    public async Task<ActionResult<LoteDeBorradoDto>> EliminarTodo(CancellationToken ct)
    {
        if (!ActorDeLaSesion(out var actor))
        {
            return Unauthorized();
        }

        var lote = await consultas.EliminarTodoAsync(actor, ct);

        return Ok(new LoteDeBorradoDto(lote));
    }

    /// <summary>
    /// Deshace un lote de borrado propio, dentro de su ventana. <c>404</c> si
    /// el lote no existe, no es propio, o venció — los tres casos son
    /// indistinguibles a propósito.
    /// </summary>
    [HttpPost("borrados/{lote:guid}/deshacer")]
    public async Task<IActionResult> DeshacerBorrado(Guid lote, CancellationToken ct)
    {
        if (!ActorDeLaSesion(out var actor))
        {
            return Unauthorized();
        }

        var deshecho = await consultas.DeshacerBorradoAsync(actor, lote, ct);

        return deshecho ? NoContent() : LoteNoEncontrado();
    }

    /// <summary>
    /// Reanuda una conversación propia: siembra un hilo efímero nuevo con sus
    /// turnos persistidos, listo para un seguimiento inmediato.
    /// </summary>
    [HttpPost("{hiloId:guid}/reanudar")]
    public async Task<ActionResult<ReanudarDto>> Reanudar(Guid hiloId, CancellationToken ct)
    {
        if (!ActorDeLaSesion(out var actor))
        {
            return Unauthorized();
        }

        var turnos = await consultas.ObtenerTurnosAsync(actor, hiloId, ct);
        if (turnos is null)
        {
            return ConversacionNoEncontrada();
        }

        var perfil = await perfiles.ObtenerAsync(actor, ct);

        // `TurnoHistoricoId: t.Id`, y no `ClaveDelCliente`: un turno sembrado no
        // tiene Idempotency-Key de esta sesión, así que un reemplazo sobre él se
        // nombra por su id de `turno_historico` (design.md D9 de
        // asistente-rediseno-v3, «Editar y reenviar»).
        var sembrado = hilos.Sembrar(
            actor,
            hiloId,
            [.. turnos.Select(t => new TurnoDelHilo(
                t.Pregunta, t.OcurrioEn, t.SqlResuelto, TurnoHistoricoId: t.Id, Referencias: t.Referencias))]);

        // Mismo criterio que `Obtener`: las menciones se re-resuelven para el actor
        // que reanuda, así que el cliente pinta el chip apenas siembra la
        // conversación (decisión 15 del PO).
        var turnosDto = await Task.WhenAll(
            turnos.Select(t => TurnoDeHistorialDto.DeAsync(t, perfil.VeLaConsulta, actor, menciones, ct)));

        return Ok(new ReanudarDto(sembrado.Id, turnosDto));
    }

    /// <summary>
    /// «Volver a consultar»: vuelve a ejecutar la SQL de un turno propio ya
    /// respondido, bajo el alcance ACTUAL del actor. Nunca llama al modelo, y
    /// nunca escribe una fila nueva de historial (design.md D4).
    /// </summary>
    [HttpPost("turnos/{turnoId:guid}/reejecutar")]
    public async Task<ActionResult<ReejecucionDto>> Reejecutar(Guid turnoId, CancellationToken ct)
    {
        if (!ActorDeLaSesion(out var actor))
        {
            return Unauthorized();
        }

        var turno = await consultas.ObtenerTurnoParaReejecutarAsync(actor, turnoId, ct);
        if (turno is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "El turno no existe",
                Detail = "No existe, o no es un turno propio.",
                Status = StatusCodes.Status404NotFound,
            });
        }

        if (turno.Estado != EstadoDelTurno.Respondida || turno.SqlResuelto is null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Nada para volver a consultar",
                Detail = "Este turno no terminó respondido, o no dejó una consulta que re-ejecutar.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        var perfil = await perfiles.ObtenerAsync(actor, ct);

        // REVALIDACIÓN DE LAS MENCIONES CONTRA EL ALCANCE ACTUAL (design.md D11
        // de asistente-rediseno-v3, tarea 7.4): entre la pregunta original y
        // este «Volver a consultar» el actor puede haber perdido el permiso o
        // el ámbito que alcanzaba a la entidad referenciada. Mismo texto de
        // abstención que un rechazo del motor, para no distinguir las causas.
        var bindings = new Dictionary<string, Guid>(StringComparer.Ordinal);
        if (turno.Referencias is { Count: > 0 } referencias)
        {
            foreach (var (marcador, referencia) in referencias)
            {
                var resuelta = await menciones.ResolverAsync(actor, referencia.Tipo, referencia.Id, ct);
                if (resuelta is null)
                {
                    return Ok(new ReejecucionDto
                    {
                        Exitosa = false,
                        Mensaje = "No pude volver a ejecutar esa consulta. Puede que ya no tengas acceso "
                            + "a esos datos, o que hayan cambiado desde que se hizo la pregunta.",
                    });
                }

                bindings[marcador] = referencia.Id;
            }
        }

        try
        {
            var resultado = await ejecutor.EjecutarAsync(
                turno.SqlResuelto, actor, perfil.VeDatosPersonales, ct, bindings);

            return Ok(new ReejecucionDto
            {
                Exitosa = true,
                Columnas = [.. resultado.Columnas.Select((nombre, i) =>
                    new ColumnaDto(nombre, resultado.SensibilidadDe(i).Tapa))],
                Filas = resultado.Filas,
                Truncado = resultado.Truncado,
            });
        }
        catch (Exception excepcion) when (excepcion is ConsultaSinPrivilegio or ConsultaRechazadaPorElMotor)
        {
            // La SQL guardada ya no corre —privilegios que se achicaron desde
            // que se hizo la pregunta, o un esquema que cambió—. Nunca un
            // error crudo: la misma abstención que ya usa el carril en vivo.
            return Ok(new ReejecucionDto
            {
                Exitosa = false,
                Mensaje = "No pude volver a ejecutar esa consulta. Puede que ya no tengas acceso a "
                    + "esos datos, o que hayan cambiado desde que se hizo la pregunta.",
            });
        }
    }

    private ActionResult ConversacionNoEncontrada() => NotFound(new ProblemDetails
    {
        Title = "La conversación no existe",
        Detail = "No existe, o no es una conversación propia.",
        Status = StatusCodes.Status404NotFound,
    });

    private ActionResult LoteNoEncontrado() => NotFound(new ProblemDetails
    {
        Title = "El borrado no existe",
        Detail = "No existe, no es propio, o venció la ventana para deshacerlo.",
        Status = StatusCodes.Status404NotFound,
    });

    private bool ActorDeLaSesion(out Guid actor) => Guid.TryParse(usuario.UserId, out actor);
}
