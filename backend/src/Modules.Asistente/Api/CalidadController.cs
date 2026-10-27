using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Modules.Asistente.Application;

namespace Modules.Asistente.Api;

/// <summary>
/// How users rate the Asistente's answers and where it visibly fails
/// (asistente-metricas-de-calidad).
/// </summary>
[ApiController]
[Route("api/asistente/administracion/calidad")]
public sealed class CalidadController(
    IConsultasDeCalidad agregados,
    IConsultasDeProblemasDeCalidad problemas,
    IAuditoriaDeRevisionDeCalidad auditoria,
    ICurrentUser usuario) : ControllerBase
{
    /// <summary>
    /// The aggregate metrics: negative rate, reasons, failures by cause, coverage, per-model table,
    /// series and trend against the previous period of equal length.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Permisos.AsistenteAdministrar)]
    public async Task<ActionResult<CalidadAgregada>> Obtener(int? periodo, string? modelo, CancellationToken ct)
    {
        var dias = periodo ?? 30;
        if (!ParametrosDeCalidad.Periodos.Contains(dias))
        {
            return PeriodoInvalido();
        }

        return Ok(await agregados.ObtenerAsync(dias, LimpiarModelo(modelo), ct));
    }

    /// <summary>
    /// One page of the anonymous problem list. Audits every successful read, before answering.
    /// </summary>
    [HttpGet("problemas")]
    [Authorize(Policy = Permisos.AsistenteRevisarCalidad)]
    public async Task<ActionResult<PaginaDeProblemas>> Problemas(
        int? periodo, string? modelo, string? tipo, int? pagina, CancellationToken ct)
    {
        var dias = periodo ?? 30;
        if (!ParametrosDeCalidad.Periodos.Contains(dias))
        {
            return PeriodoInvalido();
        }

        if (!string.IsNullOrEmpty(tipo) && !ParametrosDeCalidad.Tipos.Contains(tipo))
        {
            return Rechazo("Tipo no válido", "El tipo debe ser negativa, fallo o reemplazo.");
        }

        var numero = pagina ?? 1;
        if (numero is < 1 or > ParametrosDeCalidad.PaginaMaxima)
        {
            return Rechazo("Página no válida", $"La página debe estar entre 1 y {ParametrosDeCalidad.PaginaMaxima}.");
        }

        if (!Guid.TryParse(usuario.UserId, out var lector))
        {
            return Unauthorized();
        }

        var tipoFiltrado = string.IsNullOrEmpty(tipo) ? null : tipo;
        var modeloFiltrado = LimpiarModelo(modelo);

        var resultado = await problemas.ListarAsync(dias, modeloFiltrado, tipoFiltrado, numero, ct);

        // Written BEFORE the response, and a failure here fails the request: no page leaves
        // unaudited. A refused read never reaches this line.
        await auditoria.RegistrarAsync(
            lector,
            $"periodo={dias};modelo={modeloFiltrado ?? "todos"};tipo={tipoFiltrado ?? "todos"};pagina={numero}",
            resultado.Filas.Count,
            ct);

        return Ok(resultado);
    }

    private static string? LimpiarModelo(string? modelo) =>
        string.IsNullOrWhiteSpace(modelo) ? null : modelo.Trim();

    private static ActionResult PeriodoInvalido() =>
        Rechazo("Período no válido", "El período debe ser 7, 30 o 90 días.");

    private static BadRequestObjectResult Rechazo(string titulo, string detalle) =>
        new(new ProblemDetails
        {
            Title = titulo,
            Detail = detalle,
            Status = StatusCodes.Status400BadRequest,
        });
}
