using System.Globalization;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Lo que el servidor del modelo propio y la compuerta del backend dicen de su
/// carga (asistente-optimizaciones-modelo-local, design.md D8).
/// </summary>
/// <param name="Motor"><c>vllm</c>, <c>llama.cpp</c>, o nulo si no se reconoció.</param>
/// <param name="UsoDeKvCache">Fracción de la KV cache ocupada, de 0 a 1.</param>
/// <param name="AciertosDeCacheDePrefijo">
/// Fracción de tokens de prompt servidos desde la caché de prefijo desde que el
/// servidor arrancó, de 0 a 1. Sólo vLLM la publica.
/// </param>
internal sealed record EstadoDelServidorLocal(
    bool Configurado,
    bool Alcanzable,
    string? Motor,
    int? EnCurso,
    int? EnEspera,
    double? UsoDeKvCache,
    double? AciertosDeCacheDePrefijo,
    (int Capacidad, int EnCurso, int EnEspera)? Compuerta);

/// <summary>
/// Lee el <c>/metrics</c> Prometheus del servidor local (D8).
/// </summary>
/// <remarks>
/// <b>Una métrica que el servidor no publica queda nula, nunca en cero.</b> Cero
/// turnos en espera es un dato; «no sé» es otro, y confundirlos le diría al panel
/// que la GPU está libre cuando nadie lo midió.
///
/// No es parte del camino del turno: un servidor que no responde a tiempo se
/// informa como inalcanzable en 3 s y nada más.
/// </remarks>
internal sealed class TelemetriaDelServidorLocal(
    IHttpClientFactory clientes,
    IOptions<OpcionesAsistente> opciones,
    IServiceProvider servicios,
    ILogger<TelemetriaDelServidorLocal> log)
{
    /// <summary>Cliente HTTP con nombre, con timeout corto y sin reintentos.</summary>
    public const string Cliente = "asistente-metricas";

    public async Task<EstadoDelServidorLocal> ConsultarAsync(CancellationToken ct)
    {
        var valores = opciones.Value;
        var compuerta = valores.MaximoDeLlamadasConcurrentes > 0
            && servicios.GetService(typeof(CompuertaDelModelo)) is CompuertaDelModelo laCompuerta
                ? (laCompuerta.Capacidad, laCompuerta.EnCurso, laCompuerta.EnEspera)
                : ((int, int, int)?)null;

        if (!string.Equals(valores.Proveedor, ProveedorLocal.Clave, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(valores.UrlDelProveedorLocal))
        {
            return new EstadoDelServidorLocal(false, false, null, null, null, null, null, compuerta);
        }

        string texto;
        try
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Get, UrlDeMetricas(valores.UrlDelProveedorLocal));
            if (!string.IsNullOrWhiteSpace(valores.ClaveDelProveedor))
            {
                pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", valores.ClaveDelProveedor);
            }

            using var respuesta = await clientes.CreateClient(Cliente).SendAsync(pedido, ct);
            respuesta.EnsureSuccessStatusCode();
            texto = await respuesta.Content.ReadAsStringAsync(ct);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or TaskCanceledException
            && !ct.IsCancellationRequested)
        {
            log.LogInformation("El servidor del modelo local no respondió sus métricas: {Motivo}", excepcion.Message);
            return new EstadoDelServidorLocal(true, false, null, null, null, null, null, compuerta);
        }

        return Interpretar(texto) with { Compuerta = compuerta };
    }

    /// <summary>La URL de métricas: la base del servidor sin el <c>/v1</c>.</summary>
    internal static Uri UrlDeMetricas(string url)
    {
        var baseUrl = url.TrimEnd('/');
        if (baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            baseUrl = baseUrl[..^3];
        }

        return new Uri(baseUrl + "/metrics", UriKind.Absolute);
    }

    /// <summary>Interpreta el formato de texto de Prometheus (D8).</summary>
    /// <remarks>
    /// Suma las muestras de cada métrica sin mirar las etiquetas: un servidor con
    /// un solo modelo publica una serie por métrica, y si publicara varias, la
    /// suma es lo que el panel quiere ver.
    /// </remarks>
    internal static EstadoDelServidorLocal Interpretar(string texto)
    {
        var metricas = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var linea in texto.Split('\n'))
        {
            var limpia = linea.Trim();
            if (limpia.Length == 0 || limpia[0] == '#')
            {
                continue;
            }

            var finDelNombre = limpia.IndexOfAny([' ', '{']);
            if (finDelNombre <= 0)
            {
                continue;
            }

            var nombre = limpia[..finDelNombre];
            var resto = limpia[finDelNombre..];
            if (resto.StartsWith('{'))
            {
                var cierre = resto.IndexOf('}', StringComparison.Ordinal);
                resto = cierre < 0 ? string.Empty : resto[(cierre + 1)..];
            }

            var partes = resto.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length > 0
                && double.TryParse(partes[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var valor))
            {
                metricas[nombre] = metricas.GetValueOrDefault(nombre) + valor;
            }
        }

        double? Leer(params string[] nombres) =>
            nombres.Select(n => metricas.TryGetValue(n, out var v) ? v : (double?)null).FirstOrDefault(v => v is not null);

        if (metricas.Keys.Any(n => n.StartsWith("vllm:", StringComparison.Ordinal)))
        {
            var aciertos = Leer("vllm:prefix_cache_hits_total", "vllm:prefix_cache_hits");
            var consultas = Leer("vllm:prefix_cache_queries_total", "vllm:prefix_cache_queries");

            return new EstadoDelServidorLocal(
                true,
                true,
                "vllm",
                Entero(Leer("vllm:num_requests_running")),
                Entero(Leer("vllm:num_requests_waiting")),
                Leer("vllm:kv_cache_usage_perc", "vllm:gpu_cache_usage_perc"),
                aciertos is { } a && consultas is > 0 ? a / consultas.Value : null,
                null);
        }

        if (metricas.Keys.Any(n => n.StartsWith("llamacpp:", StringComparison.Ordinal)))
        {
            return new EstadoDelServidorLocal(
                true,
                true,
                "llama.cpp",
                Entero(Leer("llamacpp:requests_processing")),
                Entero(Leer("llamacpp:requests_deferred")),
                Leer("llamacpp:kv_cache_usage_ratio"),
                null,
                null);
        }

        return new EstadoDelServidorLocal(true, true, null, null, null, null, null, null);
    }

    private static int? Entero(double? valor) => valor is { } v ? (int)Math.Round(v) : null;
}
