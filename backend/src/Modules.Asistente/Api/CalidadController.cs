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
/// <remarks>
/// <b>No class-level policy, on purpose.</b> Two attributes would be ANDed; here each
/// endpoint has its own permission, because they guard different things. The aggregates
/// are counters and need <c>asistente.administrar</c>. The problem list shows question text
/// and needs <c>asistente.revisar_calidad</c>, seeded to no role: holding the administration
/// permission alone is not enough to read it.
///
/// The responses are the Application records as they are. None of them has an actor, a vote's
/// free text or a vote's timestamp, and <c>CalidadControllerTests</c> scans the serialized
/// JSON for them, so adding one later fails a test instead of leaking it.
/// </remarks>
[ApiController]
[Route("api/asistente/administracion/calidad")]
public sealed class CalidadController(
    IConsultasDeCalidad agregados,
    IConsultasDeProblemasDeCalidad problemas,
    IAuditoriaDeRevisionDeCalidad auditoria,
    ICurrentUser usuario) : ControllerBase
{
    /// <summary>
    /// The aggregate metrics: negative rate, reasons, failures by cause, coverage, per-model
    /// table, series and trend against the previous period of equal length.
    /// </summary>
    /// <param name="periodo">7, 30 (the default) or 90 days.</param>
    /// <param name="modelo">Optional <c>empresa/modelo</c> filter.</param>
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
    /// <param name="periodo">7, 30 (the default) or 90 days.</param>
    /// <param name="modelo">Optional <c>empresa/modelo</c> filter.</param>
    /// <param name="tipo">Optional: <c>negativa</c>, <c>fallo</c> or <c>reemplazo</c>.</param>
    /// <param name="pagina">1-based page number. Default 1.</param>
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
