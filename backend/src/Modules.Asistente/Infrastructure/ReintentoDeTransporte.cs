using System.Net;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Reintento de transporte con backoff exponencial y jitter (RNF-11).
/// </summary>
/// <remarks>
/// Va como <see cref="DelegatingHandler"/> del cliente HTTP del proveedor, así que
/// cubre cualquier implementación real que llegue después sin que ella tenga que
/// saber nada de reintentos.
///
/// Reintenta lo que puede mejorar solo: <c>429</c>, los <c>5xx</c> de servidor y
/// las fallas de red. NO reintenta ningún <c>400</c> —incluido el del límite de
/// gasto— porque reintentar un rechazo por presupuesto agotado gasta presupuesto
/// que ya no hay. Tampoco <c>401</c> ni <c>403</c>: una credencial no se arregla
/// esperando.
/// </remarks>
internal sealed class ReintentoDeTransporte(
    int maximoDeIntentos,
    TimeSpan esperaBase,
    TimeSpan esperaMaxima,
    Random azar) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage solicitud, CancellationToken ct)
    {
        HttpResponseMessage? respuesta = null;

        for (var intento = 1; ; intento++)
        {
            respuesta?.Dispose();
            respuesta = await base.SendAsync(solicitud, ct);

            if (intento >= maximoDeIntentos || !EsReintentable(respuesta.StatusCode))
            {
                return respuesta;
            }

            var espera = CalcularEspera(
                intento, esperaBase, esperaMaxima, LeerRetryAfter(respuesta), azar);
            await Task.Delay(espera, ct);
        }
    }

    /// <summary>
    /// Cuánto esperar antes del intento siguiente.
    /// </summary>
    /// <remarks>
    /// Si el proveedor mandó <c>retry-after</c>, manda él: sabe cuándo se le
    /// libera la cuota mejor que cualquier fórmula, y desoírlo es la forma más
    /// rápida de que la ventana se extienda.
    ///
    /// Si no, backoff exponencial con jitter COMPLETO: la espera es un valor al
    /// azar entre cero y el backoff. Sin jitter, todos los turnos que chocaron con
    /// el mismo <c>429</c> vuelven juntos al mismo milisegundo y reconstruyen el
    /// pico que los rechazó.
    /// </remarks>
    internal static TimeSpan CalcularEspera(
        int intento, TimeSpan esperaBase, TimeSpan esperaMaxima, TimeSpan? retryAfter, Random azar)
    {
        if (retryAfter is { } indicada)
        {
            return indicada <= esperaMaxima ? indicada : esperaMaxima;
        }

        var exponencial = esperaBase * Math.Pow(2, intento - 1);
        var techo = exponencial <= esperaMaxima ? exponencial : esperaMaxima;

        return techo * azar.NextDouble();
    }

    /// <summary>Qué respuestas vale la pena repetir.</summary>
    internal static bool EsReintentable(HttpStatusCode codigo) => codigo switch
    {
        HttpStatusCode.TooManyRequests => true,
        HttpStatusCode.InternalServerError => true,
        HttpStatusCode.BadGateway => true,
        HttpStatusCode.ServiceUnavailable => true,
        HttpStatusCode.GatewayTimeout => true,
        _ => false,
    };

    /// <summary>
    /// <c>retry-after</c> viene como segundos o como fecha; se admiten las dos.
    /// </summary>
    private static TimeSpan? LeerRetryAfter(HttpResponseMessage respuesta)
    {
        var cabecera = respuesta.Headers.RetryAfter;
        if (cabecera is null)
        {
            return null;
        }

        if (cabecera.Delta is { } delta)
        {
            return delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
        }

        if (cabecera.Date is { } fecha)
        {
            var falta = fecha - DateTimeOffset.UtcNow;
            return falta > TimeSpan.Zero ? falta : TimeSpan.Zero;
        }

        return null;
    }
}
