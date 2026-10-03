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
    IBuscadorDeMenciones menciones,
    ICurrentUser usuario) : ControllerBase
{
    /// <summary>Cabecera con la clave de idempotencia del turno.</summary>
    public const string CabeceraDeIdempotencia = "Idempotency-Key";

    /// <summary>Tipos de mención admitidos en la URL y en el cuerpo del turno.</summary>
    private const string TipoMateria = "materia";
    private const string TipoDocente = "docente";

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

        // REVALIDACIÓN DE LAS MENCIONES, ANTES DEL CANDADO Y DE TODO LO DEMÁS
        // (design.md D11 de asistente-rediseno-v3, tarea 7.3): una mención
        // desconocida o fuera del alcance ACTUAL del actor —sus permisos
        // pueden haber cambiado desde que abrió el popover— corta acá, antes
        // de cobrar cupo o escribir historial. `400` para las dos causas, sin
        // decir cuál: no hay oráculo de existencia.
        IReadOnlyList<(TipoDeMencion Tipo, ResultadoDeMencion Entidad)>? mencionesResueltas = null;
        if (consulta.Referencias is { Count: > 0 } referencias)
        {
            var resueltas = new List<(TipoDeMencion, ResultadoDeMencion)>(referencias.Count);

            foreach (var referencia in referencias)
            {
                var tipo = TipoDeMencionDe(referencia.Tipo);
                var resuelta = tipo is null
                    ? null
                    : await menciones.ResolverAsync(actor, tipo.Value, referencia.Id, ct);

                if (resuelta is null)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Mención no disponible",
                        Detail = "Una de las menciones ya no está disponible. Volvé a elegirla.",
                        Status = StatusCodes.Status400BadRequest,
                    });
                }

                resueltas.Add((tipo!.Value, resuelta));
            }

            mencionesResueltas = resueltas;
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
                actor, consulta.Hilo, consulta.Mensaje, ct, claveDeIdempotencia, consulta.Reemplaza,
                mencionesResueltas);
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
    /// Busca materias o docentes para el popover de menciones «@materia» /
    /// «#docente» del composer (design.md D10 de asistente-rediseno-v3).
    /// </summary>
    /// <remarks>
    /// Corre sobre <see cref="IBuscadorDeMenciones"/>, que hace todo el trabajo
    /// de alcance con el motor —RLS y <c>identity.asistente_materias_visibles()</c>—
    /// y nunca con un filtro de acá. Este método sólo valida la forma del pedido
    /// (<paramref name="tipo"/> y el largo de <paramref name="q"/>) y traduce.
    /// </remarks>
    [Authorize(Policy = Permisos.AsistenteConsultar)]
    [HttpGet("menciones")]
    public async Task<ActionResult<MencionesDto>> Menciones(
        [FromQuery] string? tipo, [FromQuery] string? q, CancellationToken ct)
    {
        if (!ActorDeLaSesion(out var actor))
        {
            return Unauthorized();
        }

        var tipoDeMencion = TipoDeMencionDe(tipo);
        if (tipoDeMencion is null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Tipo de mención desconocido",
                Detail = $"'tipo' tiene que ser '{TipoMateria}' o '{TipoDocente}'.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        if (q is null || q.Length < 2 || q.Length > 100)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Término de búsqueda inválido",
                Detail = "'q' tiene que tener entre 2 y 100 caracteres.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        var busqueda = await menciones.BuscarAsync(actor, tipoDeMencion.Value, q, ct);

        return Ok(MencionesDto.De(busqueda));
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
    /// <summary>Longest <see cref="PedidoDeRetroalimentacion.Comentario"/> accepted, after trimming.</summary>
    private const int LargoMaximoDelComentario = 500;

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

        if (pedido.Razones is { Count: > 0 } razones)
        {
            if (razones.Any(razon => !RazonesDeRetroalimentacion.Todas.Contains(razon)))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Razón desconocida",
                    Detail = "Cada razón tiene que ser una de las cuatro que ofrece la interfaz.",
                    Status = StatusCodes.Status400BadRequest,
                });
            }

            if (razones.Distinct().Count() != razones.Count)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Razón repetida",
                    Detail = "Cada razón puede aparecer una sola vez.",
                    Status = StatusCodes.Status400BadRequest,
                });
            }
        }

        // Trimmed here, antes de validar el largo y de persistir: un comentario que
        // sólo tiene espacios se trata como ausente (asistente-retroalimentacion's spec).
        var comentario = pedido.Comentario?.Trim();
        if (string.IsNullOrEmpty(comentario))
        {
            comentario = null;
        }
        else if (comentario.Length > LargoMaximoDelComentario)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Comentario demasiado largo",
                Detail = $"El comentario admite hasta {LargoMaximoDelComentario} caracteres.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        var resultado = await retroalimentacion.RegistrarAsync(
            pedido.Token, pedido.Voto, pedido.Razones, comentario, ct);

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

    /// <summary>
    /// Traduce el <c>tipo</c> de la URL o del cuerpo a <see cref="TipoDeMencion"/>,
    /// o <c>null</c> si no es ninguno de los dos admitidos —se trata igual que un
    /// identificador que no existe, nunca con un mensaje que lo distinga.
    /// </summary>
    private static TipoDeMencion? TipoDeMencionDe(string? tipo) => tipo switch
    {
        TipoMateria => TipoDeMencion.Materia,
        TipoDocente => TipoDeMencion.Docente,
        _ => null,
    };
}
