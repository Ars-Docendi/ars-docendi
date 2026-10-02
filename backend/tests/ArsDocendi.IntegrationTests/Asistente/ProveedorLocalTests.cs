using System.Net;
using System.Text;
using System.Text.Json;
using ArsDocendi.Shared;
using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El adaptador del modelo propio contra el cable de un servidor OpenAI-compatible
/// (asistente-proveedor-local, design.md D2), sin GPU ni red.
/// </summary>
/// <remarks>
/// Igual que <see cref="ProveedorAnthropicTests"/>: todo lo que el adaptador tiene
/// que garantizar es observable en el JSON que sale o en lo que vuelve.
/// </remarks>
public sealed class ProveedorLocalTests
{
    private const string Url = "http://arsdocendi-llm:8000/v1";
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
    public async Task Pega_contra_chat_completions_de_la_url_configurada()
    {
        var servidor = ServidorFalso.QueResponde();

        await Armar(servidor, url: Url + "/").CompletarAsync(Solicitud, Ct);

        Assert.Equal(new Uri("http://arsdocendi-llm:8000/v1/chat/completions"), servidor.Pedidos[0].Uri);
    }

    [Fact]
    public async Task El_prefijo_va_primero_como_sistema_y_es_identico_entre_turnos()
    {
        var servidor = ServidorFalso.QueResponde();
        var proveedor = Armar(servidor);

        await proveedor.CompletarAsync(Solicitud, Ct);
        await proveedor.CompletarAsync(Solicitud with { Mensaje = "¿Cuántos pedidos hay?" }, Ct);

        // La caché de prefijo del servidor es un match de tokens desde el
        // principio: el sistema primero y byte a byte igual, lo variable detrás.
        var primero = servidor.Cuerpo(0).GetProperty("messages");
        var segundo = servidor.Cuerpo(1).GetProperty("messages");

        Assert.Equal("system", primero[0].GetProperty("role").GetString());
        Assert.Equal(Prefijo, primero[0].GetProperty("content").GetString());
        Assert.Equal(primero[0].GetRawText(), segundo[0].GetRawText());
        Assert.Equal("user", primero[1].GetProperty("role").GetString());
        Assert.NotEqual(primero[1].GetRawText(), segundo[1].GetRawText());
    }

    [Fact]
    public async Task Viajan_el_modelo_la_temperatura_y_el_techo_de_tokens()
    {
        var servidor = ServidorFalso.QueResponde();

        await Armar(servidor).CompletarAsync(Solicitud with { Temperatura = 0.3m }, Ct);

        var cuerpo = servidor.Cuerpo(0);
        Assert.Equal("qwen3-8b", cuerpo.GetProperty("model").GetString());
        Assert.Equal(0.3, cuerpo.GetProperty("temperature").GetDouble(), 3);
        Assert.Equal(800, cuerpo.GetProperty("max_tokens").GetInt32());
        Assert.False(cuerpo.GetProperty("stream").GetBoolean());
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

        Assert.Equal(
            piensa,
            servidor.Cuerpo(0).GetProperty("chat_template_kwargs").GetProperty("enable_thinking").GetBoolean());
    }

    [Fact]
    public async Task Con_esquema_de_salida_pide_decodificacion_restringida()
    {
        var servidor = ServidorFalso.QueResponde();

        await Armar(servidor).CompletarAsync(
            Solicitud with { EsquemaDeSalidaJson = GeneradorDeSql.EsquemaDeSalida }, Ct);

        var formato = servidor.Cuerpo(0).GetProperty("response_format");
        Assert.Equal("json_schema", formato.GetProperty("type").GetString());
        Assert.True(formato.GetProperty("json_schema").GetProperty("strict").GetBoolean());
        Assert.Equal(
            "object",
            formato.GetProperty("json_schema").GetProperty("schema").GetProperty("type").GetString());
    }

    [Fact]
    public async Task Sin_esquema_de_salida_el_texto_es_libre()
    {
        var servidor = ServidorFalso.QueResponde();

        await Armar(servidor).CompletarAsync(Solicitud, Ct);

        Assert.False(servidor.Cuerpo(0).TryGetProperty("response_format", out _));
    }

    [Fact]
    public async Task La_clave_viaja_como_bearer_solo_si_esta_puesta()
    {
        var conClave = ServidorFalso.QueResponde();
        var sinClave = ServidorFalso.QueResponde();

        await Armar(conClave, clave: "clave-del-servidor").CompletarAsync(Solicitud, Ct);
        await Armar(sinClave, clave: null).CompletarAsync(Solicitud, Ct);

        Assert.Equal("Bearer clave-del-servidor", conClave.Pedidos[0].Autorizacion);
        Assert.Null(sinClave.Pedidos[0].Autorizacion);
    }

    // ------------------------------------------------------------ lo que vuelve

    [Fact]
    public async Task Traduce_texto_y_tokens_con_la_cache_aparte()
    {
        var servidor = ServidorFalso.QueResponde(
            "{\"es_contestable\":true}", entrada: 12900, salida: 130, cacheados: 12200);

        var respuesta = await Armar(servidor).CompletarAsync(Solicitud, Ct);

        Assert.Equal("{\"es_contestable\":true}", respuesta.Texto);
        Assert.Equal(12900, respuesta.TokensDeEntrada);
        Assert.Equal(130, respuesta.TokensDeSalida);
        Assert.Equal(12200, respuesta.TokensDeCache);
        Assert.False(respuesta.EsSimulada);
        Assert.False(respuesta.SeQuedoSinTokens);
    }

    [Fact]
    public async Task Sin_detalle_de_cache_usa_la_que_informa_llama_server()
    {
        var servidor = new ServidorFalso(_ => Json(
            """
            {"choices":[{"message":{"role":"assistant","content":"Hay 4."},"finish_reason":"stop"}],
             "usage":{"prompt_tokens":900,"completion_tokens":5},
             "timings":{"cache_n":850}}
            """));

        var respuesta = await Armar(servidor).CompletarAsync(Solicitud, Ct);

        Assert.Equal(850, respuesta.TokensDeCache);
    }

    [Fact]
    public async Task Un_corte_por_techo_se_informa()
    {
        var servidor = ServidorFalso.QueResponde("{\"es_contes", motivo: "length");

        var respuesta = await Armar(servidor).CompletarAsync(Solicitud, Ct);

        Assert.True(respuesta.SeQuedoSinTokens);
    }

    [Fact]
    public async Task Un_bloque_de_razonamiento_que_se_coló_se_descarta()
    {
        var servidor = ServidorFalso.QueResponde(
            "<think>\nPienso en los joins…\n</think>\n{\"es_contestable\":false}");

        var respuesta = await Armar(servidor).CompletarAsync(Solicitud, Ct);

        Assert.Equal("{\"es_contestable\":false}", respuesta.Texto);
    }

    [Theory]
    [InlineData("<think>sin cerrar", "")]
    [InlineData("Respuesta.", "Respuesta.")]
    [InlineData("<think>a</think>Uno <think>b</think>dos", "Uno dos")]
    public void El_texto_sin_razonamiento(string crudo, string esperado) =>
        Assert.Equal(esperado, ProveedorLocal.SinRazonamiento(crudo));

    [Fact]
    public async Task Una_respuesta_sin_contenido_no_es_una_caida()
    {
        var servidor = new ServidorFalso(_ => Json(
            """{"choices":[{"message":{"role":"assistant","content":null},"finish_reason":"stop"}],"usage":{"prompt_tokens":10,"completion_tokens":0}}"""));

        var respuesta = await Armar(servidor).CompletarAsync(Solicitud, Ct);

        Assert.Equal(string.Empty, respuesta.Texto);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Toda_falla_llega_como_falla_de_transporte_para_que_el_breaker_la_cuente(
        HttpStatusCode estado)
    {
        var servidor = new ServidorFalso(_ => new HttpResponseMessage(estado)
        {
            Content = new StringContent("""{"error":{"message":"falla simulada"}}""", Encoding.UTF8, "application/json"),
        });

        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => Armar(servidor).CompletarAsync(Solicitud, Ct));

        Assert.Equal(estado, error.StatusCode);
    }

    [Fact]
    public void El_nombre_lleva_el_modelo_para_el_registro_operativo()
    {
        Assert.Equal("local/qwen3-8b", Armar(ServidorFalso.QueResponde()).Nombre);
    }

    // ------------------------------------------------------------ composición

    [Fact]
    public void Configurado_como_local_resuelve_el_adaptador_detras_de_los_decoradores()
    {
        using var servicios = Componer(url: Url).BuildServiceProvider();
        using var turno = servicios.CreateScope();

        var proveedor = turno.ServiceProvider.GetRequiredService<IProveedorDeModelo>();

        Assert.False(proveedor.EsSimulado);
        Assert.Equal("local/qwen3-8b", proveedor.Nombre);
    }

    [Fact]
    public void Sin_url_pedir_el_proveedor_falla_nombrando_el_valor_que_falta()
    {
        using var servicios = Componer(url: null).BuildServiceProvider();
        using var turno = servicios.CreateScope();

        var error = Assert.Throws<InvalidOperationException>(
            turno.ServiceProvider.GetRequiredService<IProveedorDeModelo>);

        Assert.Contains(
            nameof(OpcionesAsistente.UrlDelProveedorLocal), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Compuesto_consume_el_cliente_con_el_reintento_del_modulo()
    {
        var servidor = new ServidorFalso(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var servicios = Componer(url: Url, servidor: servidor, intentos: 2).BuildServiceProvider();
        using var turno = servicios.CreateScope();

        await Assert.ThrowsAsync<HttpRequestException>(
            () => turno.ServiceProvider.GetRequiredService<IProveedorDeModelo>().CompletarAsync(Solicitud, Ct));

        // Una sola autoridad de reintento, también con este adaptador.
        Assert.Equal(2, servidor.Pedidos.Count);
    }

    [Fact]
    public async Task Con_compuerta_configurada_la_llamada_pasa_por_ella()
    {
        var servidor = ServidorFalso.QueResponde();
        using var servicios = Componer(url: Url, servidor: servidor, concurrentes: 2).BuildServiceProvider();
        using var turno = servicios.CreateScope();

        await turno.ServiceProvider.GetRequiredService<IProveedorDeModelo>().CompletarAsync(Solicitud, Ct);

        var compuerta = servicios.GetRequiredService<CompuertaDelModelo>();
        Assert.Equal(2, compuerta.Capacidad);
        // Devolvió el lugar al terminar.
        Assert.Equal(0, compuerta.EnCurso);
        Assert.Single(servidor.Pedidos);
    }

    // ------------------------------------------------------------------ apoyo

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ProveedorLocal Armar(ServidorFalso servidor, string url = Url, string? clave = null) =>
        new(new HttpClient(servidor), url, clave, "qwen3-8b", NullLogger<ProveedorLocal>.Instance);

    private static HttpResponseMessage Json(string cuerpo) =>
        new(HttpStatusCode.OK) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };

    private static ServiceCollection Componer(
        string? url, ServidorFalso? servidor = null, int? intentos = null, int? concurrentes = null)
    {
        static string Ajuste(string nombre) => $"{OpcionesAsistente.Seccion}:{nombre}";

        var valores = new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{CadenaDuena.Clave}"] =
                "Host=arsdocendi-postgres;Port=5432;Database=arsdocendi_pr_123;Username=app_pr_123;Password=x",
            [Ajuste(nameof(OpcionesAsistente.Proveedor))] = ProveedorLocal.Clave,
            [Ajuste(nameof(OpcionesAsistente.Modelo))] = "qwen3-8b",
            [Ajuste(nameof(OpcionesAsistente.EsperaBaseMs))] = "1",
            [Ajuste(nameof(OpcionesAsistente.EsperaMaximaMs))] = "2",
        };

        if (url is not null)
        {
            valores[Ajuste(nameof(OpcionesAsistente.UrlDelProveedorLocal))] = url;
        }

        if (intentos is { } cuantos)
        {
            valores[Ajuste(nameof(OpcionesAsistente.MaximoDeIntentosDeTransporte))] =
                cuantos.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (concurrentes is { } limite)
        {
            valores[Ajuste(nameof(OpcionesAsistente.MaximoDeLlamadasConcurrentes))] =
                limite.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddArsDocendiShared(configuracion);
        servicios.AddAsistenteModule(configuracion);

        if (servidor is not null)
        {
            servicios.AddHttpClient(ModuleExtensions.ClienteDelProveedor)
                .ConfigurePrimaryHttpMessageHandler(() => servidor);
        }

        return servicios;
    }

    /// <summary>
    /// Un servidor OpenAI-compatible falso que guarda lo que recibe.
    /// </summary>
    private sealed class ServidorFalso(Func<int, HttpResponseMessage> responder) : HttpMessageHandler
    {
        private readonly List<(Uri Uri, string? Autorizacion, string Cuerpo)> _pedidos = [];
        private readonly Lock _candado = new();

        public IReadOnlyList<(Uri Uri, string? Autorizacion, string Cuerpo)> Pedidos
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
            int? cacheados = null,
            string motivo = "stop") =>
            new(_ => Json($$"""
                {
                  "id": "chatcmpl-falso",
                  "object": "chat.completion",
                  "model": "qwen3-8b",
                  "choices": [{ "index": 0, "message": { "role": "assistant", "content": {{JsonSerializer.Serialize(texto)}} }, "finish_reason": "{{motivo}}" }],
                  "usage": { "prompt_tokens": {{entrada}}, "completion_tokens": {{salida}}{{(cacheados is { } c ? $", \"prompt_tokens_details\": {{ \"cached_tokens\": {c} }}" : string.Empty)}} }
                }
                """));

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage solicitud, CancellationToken ct)
        {
            var cuerpo = solicitud.Content is null ? string.Empty : await solicitud.Content.ReadAsStringAsync(ct);

            int cual;
            lock (_candado)
            {
                _pedidos.Add((solicitud.RequestUri!, solicitud.Headers.Authorization?.ToString(), cuerpo));
                cual = _pedidos.Count;
            }

            return responder(cual);
        }
    }
}
