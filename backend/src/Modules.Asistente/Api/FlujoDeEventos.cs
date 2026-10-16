using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Modules.Asistente.Api;

/// <summary>
/// Escribe eventos <c>text/event-stream</c> sobre la respuesta del request
/// (asistente-optimizaciones-modelo-local, design.md D9).
/// </summary>
/// <remarks>
/// Los encabezados salen con el primer evento, no antes: mientras no se escribió
/// nada, el endpoint todavía puede responder un 400 o un 404 como siempre.
///
/// <b>Nunca lanza por un cliente que se fue.</b> La escritura se marca rota y los
/// eventos siguientes se descartan: el turno no es de la conexión, y una falla
/// acá que llegara al adaptador del modelo se contaría como una caída del
/// proveedor.
/// </remarks>
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
                // Un proxy que bufferea (nginx, Traefik con compresión) juntaría los
                // eventos y el flujo llegaría entero al final. Si igual lo hace, el
                // resultado llega completo como en el endpoint de siempre.
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
