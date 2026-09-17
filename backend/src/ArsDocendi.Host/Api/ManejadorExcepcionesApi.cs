using System.Text.Json;
using ArsDocendi.Shared.Aplicacion;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Modules.Designaciones.Domain;

namespace ArsDocendi.Host.Api;

/// <summary>Convierte errores esperados e inesperados al contrato Problem Details.</summary>
public sealed class ManejadorExcepcionesApi(ILogger<ManejadorExcepcionesApi> logger) : IExceptionHandler
{
    private const string BaseTipos = "https://ars-docendi.unlam.edu.ar/errors/";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext contexto,
        Exception excepcion,
        CancellationToken ct)
    {
        var error = Clasificar(excepcion);

        if (error.Status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(excepcion,
                "Error no controlado al procesar {Metodo} {Ruta}; TraceId {TraceId}",
                contexto.Request.Method,
                contexto.Request.Path,
                contexto.TraceIdentifier);
        }
        else
        {
            logger.LogWarning(
                "Solicitud rechazada con {CodigoError} ({Status}) en {Metodo} {Ruta}; TraceId {TraceId}",
                error.Codigo,
                error.Status,
                contexto.Request.Method,
                contexto.Request.Path,
                contexto.TraceIdentifier);
        }

        var problema = new ProblemDetails
        {
            Type = BaseTipos + error.Codigo,
            Title = error.Titulo,
            Status = error.Status,
            Detail = error.Detalle,
            Instance = contexto.Request.Path,
        };
        problema.Extensions["traceId"] = contexto.TraceIdentifier;
        if (error.Errores is not null)
        {
            problema.Extensions["errors"] = error.Errores;
        }

        contexto.Response.StatusCode = error.Status;
        contexto.Response.ContentType = "application/problem+json";
        await JsonSerializer.SerializeAsync(contexto.Response.Body, problema, cancellationToken: ct);
        return true;
    }

    private static ErrorHttp Clasificar(Exception excepcion) => excepcion switch
    {
        ExcepcionAplicacion error => DesdeAplicacion(error),
        ErrorPedidoDuplicado error => new(
            StatusCodes.Status409Conflict,
            "pedido-duplicate-live",
            "Ya existe un pedido en curso",
            error.Message),
        ErrorDominioPedido error => new(
            StatusCodes.Status422UnprocessableEntity,
            "pedido-transition-invalid",
            "La operación no cumple las reglas del pedido",
            error.Message),
        DbUpdateConcurrencyException => new(
            StatusCodes.Status409Conflict,
            "concurrency-conflict",
            "El recurso fue modificado",
            "Actualizá los datos y volvé a intentar."),
        KeyNotFoundException => new(
            StatusCodes.Status404NotFound,
            "resource-not-found",
            "No se encontró el recurso solicitado"),
        ArgumentException error => new(
            StatusCodes.Status400BadRequest,
            "validation",
            "La solicitud contiene datos inválidos",
            error.Message),
        _ => new(
            StatusCodes.Status500InternalServerError,
            "internal-error",
            "Ocurrió un error inesperado",
            "La operación no pudo completarse. Usá el identificador de seguimiento para reportarla."),
    };

    private static ErrorHttp DesdeAplicacion(ExcepcionAplicacion error)
    {
        var status = error.Tipo switch
        {
            TipoErrorAplicacion.Validacion => StatusCodes.Status400BadRequest,
            TipoErrorAplicacion.NoAutenticado => StatusCodes.Status401Unauthorized,
            TipoErrorAplicacion.Prohibido => StatusCodes.Status403Forbidden,
            TipoErrorAplicacion.NoEncontrado => StatusCodes.Status404NotFound,
            TipoErrorAplicacion.Conflicto => StatusCodes.Status409Conflict,
            TipoErrorAplicacion.ReglaDeNegocio => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status500InternalServerError,
        };

        return new ErrorHttp(status, error.Codigo, error.Message, error.Message, error.Errores);
    }

    private sealed record ErrorHttp(
        int Status,
        string Codigo,
        string Titulo,
        string? Detalle = null,
        IReadOnlyDictionary<string, string[]>? Errores = null);
}
