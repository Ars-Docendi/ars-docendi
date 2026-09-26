using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Modules.Asistente.Application;

namespace Modules.Asistente.Api;

[ApiController]
[Route("api/asistente")]
public sealed class AsistenteController(
    CapaConversacional capa,
    ICatalogoDeCapacidades capacidades,
    IIdempotencia idempotencia,
    ServicioDeRetroalimentacion retroalimentacion,
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
            turno = await capa.ResponderAsync(
                actor, consulta.Hilo, consulta.Mensaje, ct, claveDeIdempotencia, consulta.Reemplaza);
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
        catch (ReemplazoInvalido)
        {
            // `Reemplaza` no nombra el último turno vigente del hilo —o el hilo
            // venció— (design.md D9 de asistente-rediseno-v3). `409` y nada
            // cambió: ni el hilo, ni el historial, ni el cupo.
            return Conflict(new ProblemDetails
            {
                Title = "Ese turno ya no se puede reemplazar",
                Detail = "Sólo se puede reemplazar la última pregunta vigente de la conversación.",
                Status = StatusCodes.Status409Conflict,
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
    /// Rates an already-answered turn: thumbs up/down, with an optional reason on
    /// a thumbs-down.
    /// </summary>
    /// <remarks>
    /// Authorization here is possession of <paramref name="pedido"/>'s token, not
    /// actor identity (design.md D2): the policy below only gates "is this an
    /// assistant user at all", the token is what says "which turn". An unknown or
    /// expired token 404s — the same outcome for both, so a caller can't tell
    /// which — rather than returning a body that would confirm a token existed.
    /// </remarks>
    [Authorize(Policy = Permisos.AsistenteConsultar)]
    [HttpPost("retroalimentacion")]
    public async Task<IActionResult> Retroalimentacion(
        PedidoDeRetroalimentacion pedido, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        if (!ActorDeLaSesion(out _))
        {
            return Unauthorized();
        }

        if (pedido.Razon is not null && !RazonesDeRetroalimentacion.Todas.Contains(pedido.Razon))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Razón desconocida",
                Detail = "La razón tiene que ser una de las cuatro que ofrece la interfaz.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        var resultado = await retroalimentacion.RegistrarAsync(
            pedido.Token, pedido.Voto, pedido.Razon, ct);

        return resultado switch
        {
            ResultadoDeRetroalimentacion.Aceptada => NoContent(),
            // 404 y no 403 ni 401 a propósito, igual que el hilo ajeno: cualquier
            // otro código confirmaría que ese token existió alguna vez.
            _ => NotFound(new ProblemDetails
            {
                Title = "El token no existe",
                Detail = "Es desconocido o venció. Volvé a preguntar para obtener uno nuevo.",
                Status = StatusCodes.Status404NotFound,
            }),
        };
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
