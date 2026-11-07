using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Modules.Asistente.Infrastructure;

namespace Modules.Asistente.Api;

/// <summary>
/// El modelo propio en reposo y cómo pedir que se cargue (asistente-modelo-en-reposo). Fuera de la
/// cadena del turno: no consume cupo, ni techo, ni breaker, ni compuerta.
/// </summary>
[ApiController]
[Route("api/asistente/modelo")]
[Authorize(Policy = Permisos.AsistenteConsultar)]
public sealed class ModeloDelAsistenteController : ControllerBase
{
    /// <summary>Si el modelo con que respondería un turno está cargado. Sólo aplica a <c>ollama</c>.</summary>
    [HttpGet("disponibilidad")]
    public async Task<ActionResult<DisponibilidadDelModeloDto>> Disponibilidad(CancellationToken ct)
    {
        var (identidad, carga) = await ResolverAsync(ct);

        return Ok(DisponibilidadDelModeloDto.De(
            CargaDelModelo.Aplica(identidad)
                ? await carga.ConsultarAsync(identidad.Modelo, ct)
                : new LecturaDelModelo(EstadoDelModelo.NoAplica, false)));
    }

    /// <summary>
    /// Pide cargar el modelo. Idempotente y de una sola carga por ambiente: responde 202 con el
    /// estado en el momento, sin esperar a la carga.
    /// </summary>
    [HttpPost("activacion")]
    public async Task<IActionResult> Activar(CancellationToken ct)
    {
        var (identidad, carga) = await ResolverAsync(ct);

        return Accepted(DisponibilidadDelModeloDto.De(
            CargaDelModelo.Aplica(identidad)
                ? await carga.ActivarAsync(identidad.Modelo, ct)
                : new LecturaDelModelo(EstadoDelModelo.NoAplica, false)));
    }

    /// <summary>El modelo se pregunta a la identidad del turno, no a la configuración: puede ser una selección.</summary>
    private async Task<(IdentidadDelModelo Identidad, CargaDelModelo Carga)> ResolverAsync(CancellationToken ct)
    {
        var servicios = HttpContext.RequestServices;
        var identidad = await servicios.GetRequiredService<ResolutorDelProveedor>().IdentidadAsync(ct);

        return (identidad, servicios.GetRequiredService<CargaDelModelo>());
    }
}
