using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArsDocendi.Inferencia.Infrastructure;

public sealed class ClienteInferencia(HttpClient http, IOptions<InferenciaOptions> opciones, ILogger<ClienteInferencia> logger)
{
    private static readonly TimeSpan TiempoLimiteComprobacion = TimeSpan.FromSeconds(3);

    public bool Habilitada => opciones.Value.Habilitada;

    /// <summary>Consulta la versión del servicio; cualquier falla es "no disponible".</summary>
    public async Task<bool> ComprobarAsync(CancellationToken ct)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TiempoLimiteComprobacion);
        try
        {
            using var respuesta = await http.GetAsync("api/version", limite.Token);
            if (respuesta.IsSuccessStatusCode) return true;
            logger.LogWarning("Inferencia respondió {CodigoEstado} al comprobar su estado", (int)respuesta.StatusCode);
            return false;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Inferencia no respondió dentro de {TiempoLimite}", TiempoLimiteComprobacion);
            return false;
        }
        catch (HttpRequestException ex)
        {
            // Sólo el tipo de error: el mensaje puede incluir host y puerto internos.
            logger.LogWarning("No se pudo conectar con inferencia: {TipoError}", ex.HttpRequestError);
            return false;
        }
    }
}
