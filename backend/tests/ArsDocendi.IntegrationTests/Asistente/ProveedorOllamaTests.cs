using System.Net;
using System.Text;
using System.Text.Json;
using ArsDocendi.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El adaptador del servicio de inferencia compartido contra el cable de la API
/// nativa de Ollama (asistente-proveedor-ollama, design.md D1), sin GPU ni red.
/// </summary>
public sealed class ProveedorOllamaTests
{
    private const string Url = "https://ia.example.net";
    private const string Prefijo = "Esquema de identity y designaciones. Respondé con un objeto JSON.";

    private static readonly SolicitudAlModelo Solicitud = new()
    {
        PrefijoEstable = Prefijo,
        Mensaje = "¿Qué docentes dictan Bases de Datos?",
        Temperatura = 0.0m,
        Esfuerzo = EsfuerzoDelModelo.Medio,
        MaximoDeTokens = 800,
    };

    // ------------------------------------------------------------ lo que sale

    [Fact]
    public async Task Pega_contra_api_chat_del_servicio_configurado()
    {
        var servidor = ServidorFalso.QueResponde();

        await Armar(servidor, Servicio(url: Url + "/")).CompletarAsync(Solicitud, Ct);

        Assert.Equal(new Uri("https://ia.example.net/api/chat"), servidor.Pedidos[0].Uri);
    }

    [Fact]
    public async Task El_prefijo_va_primero_como_sistema_y_es_identico_entre_turnos()
    {
        var servidor = ServidorFalso.QueResponde();
        var proveedor = Armar(servidor);

        await proveedor.CompletarAsync(Solicitud, Ct);
        await proveedor.CompletarAsync(Solicitud with { Mensaje = "¿Cuántos pedidos hay?" }, Ct);

        var primero = servidor.Cuerpo(0).GetProperty("messages");
        var segundo = servidor.Cuerpo(1).GetProperty("messages");

        Assert.Equal("system", primero[0].GetProperty("role").GetString());
        Assert.Equal(Prefijo, primero[0].GetProperty("content").GetString());
        Assert.Equal(primero[0].GetRawText(), segundo[0].GetRawText());
        Assert.Equal("user", primero[1].GetProperty("role").GetString());
        Assert.NotEqual(primero[1].GetRawText(), segundo[1].GetRawText());
    }

    [Fact]
    public async Task Viajan_el_modelo_la_temperatura_el_techo_y_el_contexto()
    {
        var servidor = ServidorFalso.QueResponde();

        await Armar(servidor, contexto: 16384).CompletarAsync(Solicitud with { Temperatura = 0.3m }, Ct);

        var cuerpo = servidor.Cuerpo(0);
        var opciones = cuerpo.GetProperty("options");
        Assert.Equal("qwen3.5:9b", cuerpo.GetProperty("model").GetString());
        Assert.Equal(0.3, opciones.GetProperty("temperature").GetDouble(), 3);
        Assert.Equal(800, opciones.GetProperty("num_predict").GetInt32());
        Assert.Equal(16384, opciones.GetProperty("num_ctx").GetInt32());
        Assert.False(cuerpo.GetProperty("stream").GetBoolean());
        // Un prompt que no entra es un error, nunca un recorte silencioso del esquema.
        Assert.False(cuerpo.GetProperty("truncate").GetBoolean());
    }

    [Fact]
    public async Task Sin_contexto_configurado_vale_el_del_servidor()
    {
        var servidor = ServidorFalso.QueResponde();

        await Armar(servidor, contexto: 0).CompletarAsync(Solicitud, Ct);

        Assert.False(servidor.Cuerpo(0).GetProperty("options").TryGetProperty("num_ctx", out _));
    }

    [Theory]
    [InlineData(EsfuerzoDelModelo.Minimo, false)]
    [InlineData(EsfuerzoDelModelo.Bajo, false)]
    [InlineData(EsfuerzoDelModelo.Medio, false)]
    [InlineData(EsfuerzoDelModelo.Alto, true)]
    [InlineData(EsfuerzoDelModelo.Maximo, true)]
    public async Task El_razonamiento_solo_se_prende_con_esfuerzo_alto(EsfuerzoDelModelo esfuerzo, bool piensa)
    {
        var servidor = ServidorFalso.QueResponde();

        await Armar(servidor).CompletarAsync(Solicitud with { Esfuerzo = esfuerzo }, Ct);

        Assert.Equal(piensa, servidor.Cuerpo(0).GetProperty("think").GetBoolean());
    }

    [Fact]
    public async Task Con_esquema_de_salida_lo_manda_como_format()
    {
        var servidor = ServidorFalso.QueResponde();

        await Armar(servidor).CompletarAsync(
            Solicitud with { EsquemaDeSalidaJson = GeneradorDeSql.EsquemaDeSalida }, Ct);

        Assert.Equal("object", servidor.Cuerpo(0).GetProperty("format").GetProperty("type").GetString());
    }

    [Fact]
    public async Task Sin_esquema_de_salida_el_texto_es_libre()
    {
        var servidor = ServidorFalso.QueResponde();

        await Armar(servidor).CompletarAsync(Solicitud, Ct);

        Assert.False(servidor.Cuerpo(0).TryGetProperty("format", out _));
    }

    [Fact]
    public async Task Prod_manda_basic_auth_sin_service_token()
    {
        var servidor = ServidorFalso.QueResponde();

        await Armar(servidor, Servicio(usuario: "prod", contrasena: "secreta")).CompletarAsync(Solicitud, Ct);

        var pedido = servidor.Pedidos[0];
        Assert.Equal(
            "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("prod:secreta")),
            pedido.Autorizacion);
        Assert.Null(pedido.AccessClientId);
    }

    [Fact]
    public async Task Fuera_de_prod_manda_ademas_el_service_token()
    {
        var servidor = ServidorFalso.QueResponde();

        await Armar(servidor, Servicio(accessClientId: "id.access", accessClientSecret: "secreto"))
            .CompletarAsync(Solicitud, Ct);

        Assert.Equal("id.access", servidor.Pedidos[0].AccessClientId);
    }

    // ------------------------------------------------------------ lo que vuelve

    [Fact]
    public async Task Traduce_texto_y_tokens()
    {
        var servidor = ServidorFalso.QueResponde("SELECT 2", entrada: 11900, salida: 37);

        var respuesta = await Armar(servidor).CompletarAsync(Solicitud, Ct);

        Assert.Equal("SELECT 2", respuesta.Texto);
        Assert.Equal(11900, respuesta.TokensDeEntrada);
        Assert.Equal(37, respuesta.TokensDeSalida);
        Assert.Equal(0, respuesta.TokensDeCache);
        Assert.False(respuesta.EsSimulada);
        Assert.False(respuesta.SeQuedoSinTokens);
    }

    [Fact]
    public async Task Los_tokens_de_cache_salen_de_prompt_eval_cached_count()
    {
        var servidor = ServidorFalso.QueResponde(entrada: 12000, cacheados: 11000);

        var respuesta = await Armar(servidor).CompletarAsync(Solicitud, Ct);

        Assert.Equal(12000, respuesta.TokensDeEntrada);
        Assert.Equal(11000, respuesta.TokensDeCache);
    }

    [Fact]
    public async Task Un_corte_por_techo_se_informa()
    {
        var servidor = ServidorFalso.QueResponde(motivo: "length");

        var respuesta = await Armar(servidor).CompletarAsync(Solicitud, Ct);

        Assert.True(respuesta.SeQuedoSinTokens);
    }

    [Fact]
    public async Task Un_bloque_de_razonamiento_que_se_colo_se_descarta()
    {
        var servidor = ServidorFalso.QueResponde("<think>pienso</think>\n{\"sql\":\"SELECT 1\"}");

        var respuesta = await Armar(servidor).CompletarAsync(Solicitud, Ct);

        Assert.Equal("{\"sql\":\"SELECT 1\"}", respuesta.Texto);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task Toda_falla_llega_como_falla_de_transporte_para_que_el_breaker_la_cuente(
        HttpStatusCode estado)
    {
        var servidor = new ServidorFalso(_ => new HttpResponseMessage(estado)
        {
            Content = new StringContent("""{"error":"detalle"}"""),
        });

        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => Armar(servidor).CompletarAsync(Solicitud, Ct));

        Assert.Equal(estado, error.StatusCode);
    }

    [Fact]
    public void El_nombre_lleva_el_proveedor_y_el_modelo()
    {
        Assert.Equal("ollama/qwen3.5:9b", Armar(ServidorFalso.QueResponde()).Nombre);
    }

    [Fact]
    public void Sin_url_del_servicio_no_se_puede_armar()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => Armar(ServidorFalso.QueResponde(), Servicio(url: string.Empty)));

        Assert.Contains("Inferencia:Url", error.Message, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------ por fragmentos

    [Fact]
    public async Task Con_quien_escuche_pide_flujo_y_entrega_los_fragmentos_y_el_texto_completo()
    {
        var servidor = new ServidorFalso(_ => Flujo(
            Fragmento("Hay "),
            Fragmento("4 docentes"),
            Fragmento(" designados."),
            """{"model":"qwen3.5:9b","message":{"role":"assistant","content":""},"done":true,"done_reason":"stop","prompt_eval_count":300,"eval_count":9}"""));
        var fragmentos = new List<string>();

        var respuesta = await Armar(servidor).CompletarAsync(
            Solicitud with { AlRecibirTexto = Anotar(fragmentos) }, Ct);

        Assert.True(servidor.Cuerpo(0).GetProperty("stream").GetBoolean());
        Assert.Equal(["Hay ", "4 docentes", " designados."], fragmentos);
        Assert.Equal("Hay 4 docentes designados.", respuesta.Texto);
        Assert.Equal(300, respuesta.TokensDeEntrada);
        Assert.Equal(9, respuesta.TokensDeSalida);
    }

    [Fact]
    public async Task En_flujo_un_error_del_servidor_es_falla_de_transporte()
    {
        var servidor = new ServidorFalso(_ => Flujo(Fragmento("Hay "), """{"error":"model runner crashed"}"""));

        await Assert.ThrowsAsync<HttpRequestException>(() => Armar(servidor).CompletarAsync(
            Solicitud with { AlRecibirTexto = Anotar([]) }, Ct));
    }

    [Fact]
    public async Task En_flujo_un_corte_sin_cierre_es_falla_de_transporte()
    {
        var servidor = new ServidorFalso(_ => Flujo(Fragmento("Hay ")));

        await Assert.ThrowsAsync<HttpRequestException>(() => Armar(servidor).CompletarAsync(
            Solicitud with { AlRecibirTexto = Anotar([]) }, Ct));
    }

    // ------------------------------------------------------------ composición

    [Fact]
    public async Task Compuesto_usa_la_configuracion_inferencia_y_el_transporte_del_modulo()
    {
        var servidor = new ServidorFalso(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var servicios = Componer(servidor, intentos: 2).BuildServiceProvider();
        using var turno = servicios.CreateScope();

        await Assert.ThrowsAsync<HttpRequestException>(
            () => turno.ServiceProvider.GetRequiredService<IProveedorDeModelo>().CompletarAsync(Solicitud, Ct));

        // Una sola autoridad de reintento, también con este adaptador.
        Assert.Equal(2, servidor.Pedidos.Count);
        Assert.Equal(new Uri("https://ia.example.net/api/chat"), servidor.Pedidos[0].Uri);
        var cuerpo = servidor.Cuerpo(0);
        Assert.Equal("qwen3.5:9b", cuerpo.GetProperty("model").GetString());
        Assert.Equal(16384, cuerpo.GetProperty("options").GetProperty("num_ctx").GetInt32());
    }

    [Fact]
    public async Task Las_credenciales_del_servicio_no_viajan_a_otro_proveedor()
    {
        var servidor = new ServidorFalso(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var servicios = Componer(servidor, intentos: 1, proveedor: ProveedorAnthropic.Clave)
            .BuildServiceProvider();
        using var turno = servicios.CreateScope();

        await Assert.ThrowsAnyAsync<Exception>(
            () => turno.ServiceProvider.GetRequiredService<IProveedorDeModelo>().CompletarAsync(Solicitud, Ct));

        var pedido = Assert.Single(servidor.Pedidos);
        Assert.DoesNotContain("ia.example.net", pedido.Uri.Host, StringComparison.Ordinal);
        Assert.False(pedido.Autorizacion?.StartsWith("Basic", StringComparison.Ordinal) ?? false);
        Assert.Null(pedido.AccessClientId);
    }

    // ------------------------------------------------------------------ apoyo

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static OpcionesInferencia Servicio(
        string url = Url,
        string usuario = "preview",
        string contrasena = "clave",
        string accessClientId = "",
        string accessClientSecret = "") => new()
        {
            Url = url,
            Usuario = usuario,
            Contrasena = contrasena,
            AccessClientId = accessClientId,
            AccessClientSecret = accessClientSecret,
        };

    private static ProveedorOllama Armar(
        ServidorFalso servidor, OpcionesInferencia? servicio = null, int contexto = 16384) =>
        new(new HttpClient(servidor), servicio ?? Servicio(), "qwen3.5:9b", contexto,
            NullLogger<ProveedorOllama>.Instance);

    private static ServiceCollection Componer(
        ServidorFalso servidor, int intentos, string proveedor = ProveedorOllama.Clave)
    {
        static string Ajuste(string nombre) => $"{OpcionesAsistente.Seccion}:{nombre}";

        var valores = new Dictionary<string, string?>
        {
            ["ConnectionStrings:ArsDocendi"] = "Host=localhost;Database=arsdocendi;Username=app;Password=secreto",
            [Ajuste(nameof(OpcionesAsistente.Proveedor))] = proveedor,
            [Ajuste(nameof(OpcionesAsistente.ClaveDelProveedor))] = "sk-ant-sintetica",
            [Ajuste(nameof(OpcionesAsistente.Modelo))] = "qwen3.5:9b",
            [Ajuste(nameof(OpcionesAsistente.ContextoDelModelo))] = "16384",
            [Ajuste(nameof(OpcionesAsistente.MaximoDeIntentosDeTransporte))] =
                intentos.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [$"{OpcionesInferencia.Seccion}:Url"] = Url,
            [$"{OpcionesInferencia.Seccion}:Usuario"] = "preview",
            [$"{OpcionesInferencia.Seccion}:Contrasena"] = "clave",
            [$"{OpcionesInferencia.Seccion}:AccessClientId"] = "id.access",
            [$"{OpcionesInferencia.Seccion}:AccessClientSecret"] = "secreto",
        };

        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(
            new AmbienteDePrueba());
        servicios.AddArsDocendiShared(configuracion);
        servicios.AddAsistenteModule(configuracion);
        servicios.AddHttpClient(ModuleExtensions.ClienteDelProveedor)
            .ConfigurePrimaryHttpMessageHandler(() => servidor);

        return servicios;
    }

    private static Func<string, CancellationToken, Task> Anotar(List<string> fragmentos) =>
        (fragmento, _) =>
        {
            fragmentos.Add(fragmento);
            return Task.CompletedTask;
        };

    private static string Fragmento(string texto) =>
        $$"""{"model":"qwen3.5:9b","message":{"role":"assistant","content":{{JsonSerializer.Serialize(texto)}}},"done":false}""";

    private static HttpResponseMessage Flujo(params string[] lineas) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(string.Join('\n', lineas) + "\n", Encoding.UTF8, "application/x-ndjson"),
    };

    private sealed class AmbienteDePrueba : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "pruebas";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    /// <summary>Un Ollama falso que guarda lo que recibe.</summary>
    private sealed class ServidorFalso(Func<int, HttpResponseMessage> responder) : HttpMessageHandler
    {
        private readonly List<(Uri Uri, string? Autorizacion, string? AccessClientId, string Cuerpo)> _pedidos = [];
        private readonly Lock _candado = new();

        public IReadOnlyList<(Uri Uri, string? Autorizacion, string? AccessClientId, string Cuerpo)> Pedidos
        {
            get
            {
                lock (_candado)
                {
                    return [.. _pedidos];
                }
            }
        }

        public JsonElement Cuerpo(int cual) => JsonDocument.Parse(Pedidos[cual].Cuerpo).RootElement;

        public static ServidorFalso QueResponde(
            string texto = "SELECT 1",
            int entrada = 120,
            int salida = 8,
            string motivo = "stop",
            int? cacheados = null) =>
            new(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""
                    {
                      "model": "qwen3.5:9b",
                      "message": { "role": "assistant", "content": {{JsonSerializer.Serialize(texto)}} },
                      "done": true,
                      "done_reason": "{{motivo}}",
                      "prompt_eval_count": {{entrada}},
                      "eval_count": {{salida}}{{(cacheados is { } c ? $", \"prompt_eval_cached_count\": {c}" : string.Empty)}}
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"),
            });

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage solicitud, CancellationToken ct)
        {
            var cuerpo = solicitud.Content is null ? string.Empty : await solicitud.Content.ReadAsStringAsync(ct);
            var accessId = solicitud.Headers.TryGetValues("CF-Access-Client-Id", out var valores)
                ? valores.Single()
                : null;

            int cual;
            lock (_candado)
            {
                _pedidos.Add((solicitud.RequestUri!, solicitud.Headers.Authorization?.ToString(), accessId, cuerpo));
                cual = _pedidos.Count;
            }

            return responder(cual);
        }
    }
}
