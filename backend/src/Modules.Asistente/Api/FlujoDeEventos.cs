using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Modules.Asistente.Api;

/// <summary>
/// Escribe eventos <c>text/event-stream</c> sobre la respuesta del request
/// (asistente-optimizaciones-modelo-local, design.md D9).
/// </summary>
internal sealed class FlujoDeEventos(HttpResponse respuesta, JsonSerializerOptions json)
{
    private bool _roto;

    /// <summary>Si ya se mandaron los encabezados.</summary>
    public bool Empezo { get; private set; }

    public async Task EnviarAsync(string evento, object cuerpo, CancellationToken ct)
    {
        if (_roto)
        {
            return;
        }

        try
        {
            if (!Empezo)
            {
                respuesta.StatusCode = StatusCodes.Status200OK;
                respuesta.ContentType = "text/event-stream; charset=utf-8";
                // Un proxy que bufferea (nginx, Traefik con compresión) juntaría los eventos y el
                // flujo llegaría entero al final.
                respuesta.Headers.CacheControl = "no-cache";
                respuesta.Headers["X-Accel-Buffering"] = "no";
                respuesta.HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
                await respuesta.StartAsync(ct);
                Empezo = true;
            }

            var datos = JsonSerializer.Serialize(cuerpo, json);
            await respuesta.WriteAsync($"event: {evento}\ndata: {datos}\n\n", ct);
            await respuesta.Body.FlushAsync(ct);
        }
        catch (Exception excepcion) when (
            excepcion is IOException or OperationCanceledException or ObjectDisposedException)
        {
            _roto = true;
        }
    }
}
