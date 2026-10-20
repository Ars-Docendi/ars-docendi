using System.Diagnostics;
using ArsDocendi.Inferencia.Infrastructure;
using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ArsDocendi.Inferencia.Api;

public sealed record EstadoInferenciaDto(string Estado, DateTimeOffset ComprobadoEn, double DuracionMs);

[ApiController]
[Route("api/inferencia")]
[Authorize]
public sealed class InferenciaController(ClienteInferencia cliente) : ControllerBase
{
    [HttpGet("estado")]
    [Authorize(Policy = Permisos.SistemaEstadoVer)]
    [ProducesResponseType<EstadoInferenciaDto>(StatusCodes.Status200OK)]
    public async Task<EstadoInferenciaDto> ObtenerEstado(CancellationToken ct)
    {
        if (!cliente.Habilitada) return new EstadoInferenciaDto("deshabilitado", DateTimeOffset.UtcNow, 0);

        var inicio = Stopwatch.GetTimestamp();
        var disponible = await cliente.ComprobarAsync(ct);
        return new EstadoInferenciaDto(
            disponible ? "disponible" : "no_disponible",
            DateTimeOffset.UtcNow,
            Math.Round(Stopwatch.GetElapsedTime(inicio).TotalMilliseconds, 2));
    }
}
