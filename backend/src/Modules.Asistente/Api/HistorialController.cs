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

        return Ok(ConversacionDetalleDto.De(conversacion, perfil.VeLaConsulta));
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

    /// <summary>Borra, permanentemente, una conversación propia.</summary>
    [HttpDelete("{hiloId:guid}")]
    public async Task<IActionResult> Eliminar(Guid hiloId, CancellationToken ct)
    {
        if (!ActorDeLaSesion(out var actor))
        {
            return Unauthorized();
        }

        var eliminada = await consultas.EliminarAsync(actor, hiloId, ct);

        return eliminada ? NoContent() : ConversacionNoEncontrada();
    }

    /// <summary>Borra, permanentemente, TODAS las conversaciones propias.</summary>
    [HttpDelete]
    public async Task<IActionResult> EliminarTodo(CancellationToken ct)
    {
        if (!ActorDeLaSesion(out var actor))
        {
            return Unauthorized();
        }

        await consultas.EliminarTodoAsync(actor, ct);

        return NoContent();
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

        var sembrado = hilos.Sembrar(
            actor, hiloId, [.. turnos.Select(t => new TurnoDelHilo(t.Pregunta, t.OcurrioEn, t.SqlResuelto))]);

        return Ok(new ReanudarDto(
            sembrado.Id, [.. turnos.Select(t => TurnoDeHistorialDto.De(t, perfil.VeLaConsulta))]));
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

        try
        {
            var resultado = await ejecutor.EjecutarAsync(
                turno.SqlResuelto, actor, perfil.VeDatosPersonales, ct);

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

    private bool ActorDeLaSesion(out Guid actor) => Guid.TryParse(usuario.UserId, out actor);
}
