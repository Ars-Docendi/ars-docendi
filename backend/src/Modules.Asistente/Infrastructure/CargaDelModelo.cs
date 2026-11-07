using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Modules.Asistente.Infrastructure;

/// <summary>Si el modelo propio está cargado en Ollama (asistente-modelo-en-reposo, D5).</summary>
internal enum EstadoDelModelo
{
    NoAplica,
    Listo,
    EnReposo,
    Activando,
    Desconocido,
}

/// <summary>Lo que se sabe del modelo ahora, más si el último intento de cargarlo falló.</summary>
internal readonly record struct LecturaDelModelo(EstadoDelModelo Estado, bool UltimaActivacionFallida)
{
    /// <summary>El código del contrato HTTP.</summary>
    public string Codigo => Estado switch
    {
        EstadoDelModelo.NoAplica => "no_aplica",
        EstadoDelModelo.Listo => "listo",
        EstadoDelModelo.EnReposo => "en_reposo",
        EstadoDelModelo.Activando => "activando",
        _ => "desconocido",
    };
}

/// <summary>
/// Detecta con <c>GET /api/ps</c> si Ollama tiene cargado el modelo del asistente y lo precarga con
/// <c>POST /api/generate</c> (asistente-modelo-en-reposo, D1-D3). Un singleton, porque el
/// single-flight, la caché y el enfriamiento son del ambiente y no de un request: de ellos depende
/// que el presupuesto por credencial del servicio compartido no se gaste en estados.
/// </summary>
internal sealed class CargaDelModelo(
    IHttpClientFactory clientes,
    IOptions<OpcionesAsistente> asistente,
    IOptions<OpcionesInferencia> inferencia,
    TimeProvider reloj,
    ILogger<CargaDelModelo> log,
    IHostApplicationLifetime? vida = null)
{
    /// <summary>Cliente HTTP con nombre, sin reintentos ni timeout propio.</summary>
    public const string Cliente = "asistente-carga-del-modelo";

    private static readonly TimeSpan TimeoutDeEstado = TimeSpan.FromSeconds(3);

    private readonly Lock _candado = new();
    private readonly Dictionary<string, Entrada> _entradas = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Sólo el proveedor <c>ollama</c> tiene un modelo que pueda estar en reposo.</summary>
    public static bool Aplica(IdentidadDelModelo identidad) =>
        string.Equals(identidad.Empresa, ProveedorOllama.Clave, StringComparison.Ordinal);

    /// <summary>
    /// El estado del modelo. Una lectura vigente no sale a la red; una vencida la hace una sola
    /// vez aunque la pidan muchos a la vez.
    /// </summary>
    public async Task<LecturaDelModelo> ConsultarAsync(string modelo, CancellationToken ct)
    {
        if (!inferencia.Value.Habilitada)
        {
            return new LecturaDelModelo(EstadoDelModelo.Desconocido, false);
        }

        var clave = Normalizar(modelo);
        Task<EstadoDelModelo> refresco;

        lock (_candado)
        {
            var entrada = DeLaClave(clave);
            if (entrada.Carga is not null)
            {
                return Leer(entrada, EstadoDelModelo.Activando);
            }

            if (entrada.Estado is { } vigente && reloj.GetUtcNow() < entrada.Vence)
            {
                return Leer(entrada, vigente);
            }

            entrada.Refresco ??= Task.Run(() => LeerEstadoAsync(clave), CancellationToken.None);
            refresco = entrada.Refresco;
        }

        var estado = await refresco.WaitAsync(ct);

        lock (_candado)
        {
            var entrada = DeLaClave(clave);
            return Leer(entrada, entrada.Carga is not null ? EstadoDelModelo.Activando : estado);
        }
    }

    /// <summary>
    /// Pide cargar el modelo. Una sola carga por ambiente y modelo aunque lleguen muchos pedidos, y
    /// ninguna nueva hasta pasado el enfriamiento; el pedido nunca espera a la carga.
    /// </summary>
    public async Task<LecturaDelModelo> ActivarAsync(string modelo, CancellationToken ct)
    {
        var lectura = await ConsultarAsync(modelo, ct);
        if (lectura.Estado != EstadoDelModelo.EnReposo)
        {
            return lectura;
        }

        var clave = Normalizar(modelo);
        var enfriamiento = TimeSpan.FromSeconds(asistente.Value.EnfriamientoDeActivacionSegundos);

        lock (_candado)
        {
            var entrada = DeLaClave(clave);
            if (entrada.Carga is not null)
            {
                return Leer(entrada, EstadoDelModelo.Activando);
            }

            if (entrada.FinDeLaUltimaCarga is { } fin && reloj.GetUtcNow() < fin + enfriamiento)
            {
                return Leer(entrada, EstadoDelModelo.EnReposo);
            }

            entrada.UltimaFallida = false;
            entrada.Carga = Task.Run(() => CargarAsync(clave, modelo), CancellationToken.None);
            return Leer(entrada, EstadoDelModelo.Activando);
        }
    }

    /// <summary>
    /// Anota que el modelo respondió: un turno exitoso es la prueba más barata de que está cargado
    /// (D2). Un fallo nunca marca reposo; sólo <c>/api/ps</c> lo hace.
    /// </summary>
    public void MarcarListo(string modelo)
    {
        var clave = Normalizar(modelo);

        lock (_candado)
        {
            var entrada = DeLaClave(clave);
            entrada.Estado = EstadoDelModelo.Listo;
            entrada.Vence = reloj.GetUtcNow() + Vida();
            entrada.UltimaFallida = false;
        }
    }

    /// <summary>Un nombre sin etiqueta es <c>:latest</c>, igual que para Ollama.</summary>
    internal static string Normalizar(string modelo)
    {
        var limpio = modelo.Trim();
        var barra = limpio.LastIndexOf('/');
        return limpio.IndexOf(':', barra + 1) < 0 ? limpio + ":latest" : limpio;
    }

    private TimeSpan Vida() => TimeSpan.FromSeconds(asistente.Value.TiempoDeVidaDisponibilidadSegundos);

    private Entrada DeLaClave(string clave)
    {
        if (!_entradas.TryGetValue(clave, out var entrada))
        {
            entrada = new Entrada();
            _entradas[clave] = entrada;
        }

        return entrada;
    }

    private static LecturaDelModelo Leer(Entrada entrada, EstadoDelModelo estado) =>
        new(estado, entrada.UltimaFallida);

    private async Task<EstadoDelModelo> LeerEstadoAsync(string clave)
    {
        var estado = EstadoDelModelo.Desconocido;

        try
        {
            var servicio = inferencia.Value;
            using var tope = new CancellationTokenSource(TimeoutDeEstado, reloj);
            using var pedido = new HttpRequestMessage(HttpMethod.Get, servicio.Ruta("api/ps"));
            servicio.Autenticar(pedido);

            using var respuesta = await clientes.CreateClient(Cliente).SendAsync(pedido, tope.Token);

            // 404 es la ruta que Traefik todavía no admite; 401/429, credenciales o presupuesto. En
            // todos los casos no se sabe, y no saber no se le muestra a nadie.
            if (respuesta.IsSuccessStatusCode)
            {
                var raiz = JsonNode.Parse(await respuesta.Content.ReadAsStringAsync(tope.Token));
                if (raiz?["models"] is JsonArray cargados)
                {
                    estado = EstaCargado(cargados, clave) ? EstadoDelModelo.Listo : EstadoDelModelo.EnReposo;
                }
            }
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or OperationCanceledException
            or JsonException or InvalidOperationException or FormatException)
        {
            log.LogInformation(
                "El servicio de inferencia no informó qué modelos tiene cargados: {Causa}",
                excepcion.GetType().Name);
        }

        lock (_candado)
        {
            var entrada = DeLaClave(clave);
            entrada.Estado = estado;
            entrada.Vence = reloj.GetUtcNow() + Vida();
            entrada.Refresco = null;
            if (estado == EstadoDelModelo.Listo)
            {
                entrada.UltimaFallida = false;
            }
        }

        return estado;
    }

    /// <summary>
    /// Está cargado si figura por nombre y, cuando Ollama informa el contexto con que lo cargó, ese
    /// contexto es el que pedirían los turnos: con otro, el primer turno lo recargaría (D1).
    /// </summary>
    private bool EstaCargado(JsonArray cargados, string clave)
    {
        var contexto = asistente.Value.ContextoDelModelo;

        foreach (var modelo in cargados)
        {
            var nombre = (modelo?["model"] ?? modelo?["name"])?.GetValue<string>();
            if (nombre is null || !string.Equals(Normalizar(nombre), clave, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (contexto > 0
                && modelo?["context_length"] is JsonValue informado
                && informado.TryGetValue<long>(out var real)
                && real != contexto)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private async Task CargarAsync(string clave, string modelo)
    {
        var opciones = asistente.Value;
        var inicio = Stopwatch.GetTimestamp();
        var resultado = "cargado";
        long? cargaMs = null;

        try
        {
            using var tope = new CancellationTokenSource(
                TimeSpan.FromSeconds(opciones.TiempoMaximoDeActivacionSegundos), reloj);
            using var union = CancellationTokenSource.CreateLinkedTokenSource(
                tope.Token, vida?.ApplicationStopping ?? CancellationToken.None);

            var servicio = inferencia.Value;
            using var pedido = new HttpRequestMessage(HttpMethod.Post, servicio.Ruta("api/generate"))
            {
                Content = new StringContent(Cuerpo(modelo, opciones.ContextoDelModelo), Encoding.UTF8, "application/json"),
            };
            servicio.Autenticar(pedido);

            try
            {
                using var respuesta = await clientes.CreateClient(Cliente).SendAsync(pedido, union.Token);
                if (respuesta.IsSuccessStatusCode)
                {
                    cargaMs = DuracionDeCarga(await respuesta.Content.ReadAsStringAsync(union.Token));
                }
                else
                {
                    resultado = $"rechazado ({Categoria(respuesta.StatusCode)})";
                }
            }
            catch (OperationCanceledException)
            {
                resultado = tope.IsCancellationRequested ? "tiempo_agotado" : "interrumpido";
            }
            catch (HttpRequestException)
            {
                resultado = "inalcanzable";
            }
        }
        catch (InvalidOperationException)
        {
            resultado = "mal_configurado";
        }

        var correcto = resultado == "cargado";
        var ms = (long)Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;

        // Sin actor, sin URL, sin cuerpo (D7): es un dato del ambiente, no de quien lo pidió.
        log.LogInformation(
            "Precarga del modelo {Modelo}: {Resultado} en {Milisegundos} ms (carga informada: {CargaMs} ms).",
            modelo,
            resultado,
            ms,
            cargaMs);

        lock (_candado)
        {
            var entrada = DeLaClave(clave);
            var ahora = reloj.GetUtcNow();
            entrada.Carga = null;
            entrada.FinDeLaUltimaCarga = ahora;
            entrada.Estado = correcto ? EstadoDelModelo.Listo : EstadoDelModelo.EnReposo;
            entrada.Vence = ahora + Vida();
            entrada.UltimaFallida = !correcto;
        }
    }

    /// <summary>
    /// <c>/api/generate</c> sólo con el modelo: es la forma documentada de precargar. Con el mismo
    /// <c>num_ctx</c> que los turnos, porque otro lo haría recargar; sin <c>prompt</c> ni
    /// <c>keep_alive</c>, que es política del operador.
    /// </summary>
    internal static string Cuerpo(string modelo, int contexto)
    {
        var cuerpo = new JsonObject { ["model"] = modelo, ["stream"] = false };
        if (contexto > 0)
        {
            cuerpo["options"] = new JsonObject { ["num_ctx"] = contexto };
        }

        return cuerpo.ToJsonString();
    }

    private static long? DuracionDeCarga(string json)
    {
        try
        {
            return JsonNode.Parse(json)?["load_duration"] is JsonValue ns && ns.TryGetValue<long>(out var nanos)
                ? nanos / 1_000_000
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Categoria(HttpStatusCode codigo) => $"{(int)codigo / 100}xx";

    private sealed class Entrada
    {
        public EstadoDelModelo? Estado { get; set; }

        public DateTimeOffset Vence { get; set; }

        public Task<EstadoDelModelo>? Refresco { get; set; }

        public Task? Carga { get; set; }

        public DateTimeOffset? FinDeLaUltimaCarga { get; set; }

        public bool UltimaFallida { get; set; }
    }
}
