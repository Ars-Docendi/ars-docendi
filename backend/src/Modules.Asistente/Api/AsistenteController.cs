using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Asistente.Application;

namespace Modules.Asistente.Api;

[ApiController]
[Route("api/asistente")]
public sealed class AsistenteController(
    CapaConversacional capa,
    ICatalogoDeCapacidades capacidades,
    IIdempotencia idempotencia,
    ICurrentUser usuario) : ControllerBase
{
    /// <summary>Cabecera con la clave de idempotencia del turno.</summary>
    public const string CabeceraDeIdempotencia = "Idempotency-Key";

    /// <summary>
    /// Un turno del asistente.
    /// </summary>
    /// <remarks>
    /// La <c>Idempotency-Key</c> es obligatoria y no es formalismo: cada turno cuesta
    /// dos o tres llamadas al modelo, así que un doble submit se factura completo dos
    /// veces. Se resuelve en memoria, acotada por actor, con expiración corta.
    /// </remarks>
    [Authorize(Policy = Permisos.AsistenteConsultar)]
    [HttpPost("consultas")]
    public async Task<ActionResult<RespuestaDelAsistente>> Consultar(
        ConsultaDelAsistente consulta,
        [FromHeader(Name = CabeceraDeIdempotencia)] string? claveDeIdempotencia,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        if (string.IsNullOrWhiteSpace(claveDeIdempotencia))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Falta la clave de idempotencia",
                Detail = $"El pedido tiene que traer la cabecera '{CabeceraDeIdempotencia}'. "
                    + "Sin ella, un doble envío del mismo turno se cobra dos veces.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        if (!ActorDeLaSesion(out var actor))
        {
            return Unauthorized();
        }

        var recordado = idempotencia.Recordar(actor, claveDeIdempotencia);
        if (recordado is not null)
        {
            return Ok(RespuestaDelAsistente.De(recordado));
        }

        ResultadoDelTurno turno;
        try
        {
            turno = await capa.ResponderAsync(actor, consulta.Hilo, consulta.Mensaje, ct);
        }
        catch (HiloAjeno)
        {
            // El hilo existe pero es de otro. Se responde 404 y no 403 a propósito:
            // un 403 confirmaría que ese identificador de hilo existe.
            return NotFound(new ProblemDetails
            {
                Title = "El hilo no existe",
                Detail = "Empezá una conversación nueva.",
                Status = StatusCodes.Status404NotFound,
            });
        }

        idempotencia.Guardar(actor, claveDeIdempotencia, turno);

        return Ok(RespuestaDelAsistente.De(turno));
    }

    /// <summary>
    /// Qué puede hacer el asistente para este actor.
    /// </summary>
    /// <remarks>
    /// Una caja de texto libre sin descubrimiento es una falsa promesa: el usuario no
    /// sabe qué preguntar, y averiguarlo le cuesta un turno que termina en rechazo.
    /// Este endpoint es la mitad que falta.
    ///
    /// Se deriva de los GRANT efectivos y nunca del payload del prompt. Cuesta cero
    /// tokens, así que sigue respondiendo con el proveedor caído.
    /// </remarks>
    [Authorize(Policy = Permisos.AsistenteConsultar)]
    [HttpGet("capacidades")]
    public async Task<ActionResult<CapacidadesDto>> Capacidades(CancellationToken ct)
    {
        if (!ActorDeLaSesion(out var actor))
        {
            return Unauthorized();
        }

        return Ok(CapacidadesDto.De(await capacidades.ObtenerAsync(actor, ct)));
    }

    /// <summary>
    /// El actor sale de la identidad de la sesión y de ningún otro lado.
    /// </summary>
    /// <remarks>
    /// Un identificador tomado del cuerpo del pedido sería un selector de alcance
    /// controlado por el cliente: todo el trabajo de RLS y de privilegios por columna
    /// se evapora si el usuario elige con qué identidad se lo evalúa.
    /// </remarks>
    private bool ActorDeLaSesion(out Guid actor) =>
        Guid.TryParse(usuario.UserId, out actor);
}
