using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// <c>ProveedorChat</c> over Microsoft.Extensions.AI against the wire, with no key and no network
/// (asistente-modelo-en-caliente, tasks 2.3, 2.4, 2.12).
/// </summary>
public sealed class ProveedorChatTests
{
    private const string Prefijo = "Schema of identity and designaciones. Answer with SQL only.";
    private const string ClaveSecreta = "sk-live-ECHOED-KEY-0123456789";

    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private static SolicitudAlModelo Solicitud(
        EsfuerzoDelModelo esfuerzo = EsfuerzoDelModelo.Medio, int maximo = 777) => new()
        {
            PrefijoEstable = Prefijo,
            Mensaje = "Which teachers teach Databases?",
            Temperatura = 0.0m,
            Esfuerzo = esfuerzo,
            MaximoDeTokens = maximo,
        };

    // ------------------------------------------------------------ helpers

    private static HttpResponseMessage Json(HttpStatusCode estado, string cuerpo) =>
        new(estado) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage RespuestaDeAnthropic(
        string stop = "end_turn", int entrada = 10, int salida = 5, int lectura = 7, int creacion = 3) =>
        Json(HttpStatusCode.OK, $$$"""
            {"id":"msg_1","type":"message","role":"assistant","model":"claude-x",
             "content":[{"type":"text","text":"SELECT 1"}],
             "stop_reason":"{{{stop}}}","stop_sequence":null,
             "usage":{"input_tokens":{{{entrada}}},"output_tokens":{{{salida}}},
                      "cache_creation_input_tokens":{{{creacion}}},"cache_read_input_tokens":{{{lectura}}} }}
            """);

    private static HttpResponseMessage RespuestaCompatible(
        string finish = "stop", int entrada = 10, int salida = 5, int cache = 7) =>
        Json(HttpStatusCode.OK, $$$"""
            {"id":"c1","object":"chat.completion","created":1,"model":"m",
             "choices":[{"index":0,"message":{"role":"assistant","content":"SELECT 1"},"finish_reason":"{{{finish}}}"}],
             "usage":{"prompt_tokens":{{{entrada}}},"completion_tokens":{{{salida}}},"total_tokens":{{{entrada + salida}}},
                      "prompt_tokens_details":{"cached_tokens":{{{cache}}} } } }
            """);

    private static ProveedorChat Chat(string empresa, string modelo, HttpMessageHandler transporte) =>
        ChatConLog(empresa, modelo, transporte, NullLogger<ProveedorChat>.Instance);

    private static ProveedorChat ChatConLog(
        string empresa, string modelo, HttpMessageHandler transporte,
        Microsoft.Extensions.Logging.ILogger<ProveedorChat> log)
    {
        var cliente = ClientesDeChat.Crear(empresa, modelo, ClaveSecreta, new HttpClient(transporte));
        return new ProveedorChat(empresa, modelo, cliente.Cliente, cliente.Constructor, log, cliente.Dueno);
    }

    private static JsonNode Cuerpo(TransporteFalso transporte, int i = 0) => JsonNode.Parse(transporte.Cuerpos[i])!;

    // ----------------------------------------------- 2.12 wire-level parity

    [Theory]
    [InlineData(EsfuerzoDelModelo.Minimo, 777, "end_turn")]
    [InlineData(EsfuerzoDelModelo.Bajo, 777, "end_turn")]
    [InlineData(EsfuerzoDelModelo.Medio, 1500, "end_turn")]
    [InlineData(EsfuerzoDelModelo.Alto, 64, "max_tokens")]
    [InlineData(EsfuerzoDelModelo.Maximo, 4096, "end_turn")]
    public async Task Anthropic_chat_sends_the_same_request_and_reads_the_same_response_as_the_hand_written_adapter(
        EsfuerzoDelModelo esfuerzo, int maximo, string stop)
    {
        var solicitud = Solicitud(esfuerzo, maximo);

        using var cableDeMano = new TransporteFalso(_ => RespuestaDeAnthropic(stop));
        using var cableDeChat = new TransporteFalso(_ => RespuestaDeAnthropic(stop));

        using var aMano = new ProveedorAnthropic(
            new HttpClient(cableDeMano), ClaveSecreta, "claude-x", NullLogger<ProveedorAnthropic>.Instance);
        using var porChat = Chat("anthropic", "claude-x", cableDeChat);

        var deMano = await aMano.CompletarAsync(solicitud, Ct);
        var deChat = await porChat.CompletarAsync(solicitud, Ct);

        // The body: system with cache_control, output_config.effort, token ceiling,
        // model, one user turn, and nothing else (no temperature, no thinking).
        var esperado = Cuerpo(cableDeMano);
        var real = Cuerpo(cableDeChat);
        Assert.True(
            JsonNode.DeepEquals(esperado, real),
            $"Anthropic request bodies differ.\nhand-written: {esperado.ToJsonString()}\nchat:         {real.ToJsonString()}");

        Assert.Equal(maximo, real["max_tokens"]!.GetValue<int>());
        Assert.Equal("ephemeral", real["system"]![0]!["cache_control"]!["type"]!.GetValue<string>());
        Assert.Null(real["thinking"]);
        Assert.Null(real["temperature"]);

        // The response: text, tokens (input + cache read, NOT cache creation),
        // cache tokens and the cut-off flag.
        Assert.Equal(deMano, deChat);
        Assert.Equal("SELECT 1", deChat.Texto);
        Assert.Equal(10 + 7, deChat.TokensDeEntrada);
        Assert.Equal(7, deChat.TokensDeCache);
        Assert.Equal(stop == "max_tokens", deChat.SeQuedoSinTokens);
    }

    [Fact]
    public async Task Anthropic_chat_with_minimo_sends_no_output_config()
    {
        using var cable = new TransporteFalso(_ => RespuestaDeAnthropic());

        await Chat("anthropic", "claude-x", cable)
            .CompletarAsync(Solicitud(EsfuerzoDelModelo.Minimo), Ct);

        Assert.Null(Cuerpo(cable)["output_config"]);
    }

    // ------------------------------------------- OpenAI-compatible companies

    [Theory]
    [InlineData("openai", "https://api.openai.com/v1/chat/completions")]
    [InlineData("mistral", "https://api.mistral.ai/v1/chat/completions")]
    [InlineData("google", "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions")]
    public async Task Compatible_companies_post_to_their_own_base_url_with_the_prefix_as_system_message(
        string empresa, string url)
    {
        var uris = new List<Uri>();
        using var cable = new TransporteFalso(_ => RespuestaCompatible());
        var visor = new CapturaDeUri(cable, uris);

        var respuesta = await Chat(empresa, "m-1", visor)
            .CompletarAsync(Solicitud(EsfuerzoDelModelo.Minimo), Ct);

        Assert.Equal(url, uris[0].ToString());

        var cuerpo = Cuerpo(cable);
        Assert.Equal("system", cuerpo["messages"]![0]!["role"]!.GetValue<string>());
        Assert.Equal(Prefijo, cuerpo["messages"]![0]!["content"]!.ToString());
        Assert.Equal("user", cuerpo["messages"]![1]!["role"]!.GetValue<string>());
        Assert.Equal(777, cuerpo["max_completion_tokens"]!.GetValue<int>());
        Assert.Null(cuerpo["temperature"]);
        Assert.Null(cuerpo["reasoning_effort"]);

        Assert.Equal($"{empresa}/m-1", Chat(empresa, "m-1", cable).Nombre);
        Assert.Equal("SELECT 1", respuesta.Texto);
        Assert.Equal(10, respuesta.TokensDeEntrada);     // prompt_tokens already counts the cached ones
        Assert.Equal(7, respuesta.TokensDeCache);
        Assert.False(respuesta.SeQuedoSinTokens);
    }

    [Theory]
    [InlineData("openai", EsfuerzoDelModelo.Bajo, "low")]
    [InlineData("openai", EsfuerzoDelModelo.Medio, "medium")]
    [InlineData("openai", EsfuerzoDelModelo.Alto, "high")]
    [InlineData("openai", EsfuerzoDelModelo.Maximo, "high")]
    [InlineData("google", EsfuerzoDelModelo.Medio, "medium")]
    [InlineData("openai", EsfuerzoDelModelo.Minimo, null)]
    [InlineData("mistral", EsfuerzoDelModelo.Alto, null)]
    public async Task Compatible_companies_map_the_effort_to_reasoning_effort_where_supported(
        string empresa, EsfuerzoDelModelo esfuerzo, string? esperado)
    {
        using var cable = new TransporteFalso(_ => RespuestaCompatible());

        await Chat(empresa, "m-1", cable).CompletarAsync(Solicitud(esfuerzo), Ct);

        Assert.Equal(esperado, Cuerpo(cable)["reasoning_effort"]?.GetValue<string>());
    }

    [Fact]
    public async Task A_compatible_company_flags_a_response_cut_by_the_token_ceiling()
    {
        using var cable = new TransporteFalso(_ => RespuestaCompatible("length"));

        var respuesta = await Chat("openai", "m-1", cable).CompletarAsync(Solicitud(), Ct);

        Assert.True(respuesta.SeQuedoSinTokens);
    }

    [Fact]
    public async Task A_compatible_company_that_omits_cache_details_reports_no_cache_tokens()
    {
        using var cable = new TransporteFalso(_ => Json(HttpStatusCode.OK, """
            {"id":"c","object":"chat.completion","created":1,"model":"m",
             "choices":[{"index":0,"message":{"role":"assistant","content":"x"},"finish_reason":"stop"}],
             "usage":{"prompt_tokens":4,"completion_tokens":2,"total_tokens":6}}
            """));

        var respuesta = await Chat("mistral", "m-1", cable).CompletarAsync(Solicitud(), Ct);

        Assert.Equal(4, respuesta.TokensDeEntrada);
        Assert.Equal(0, respuesta.TokensDeCache);
    }

    // ------------------------------------------------------------- failures

    [Theory]
    [InlineData("anthropic", 401)]
    [InlineData("anthropic", 429)]
    [InlineData("anthropic", 500)]
    [InlineData("openai", 401)]
    [InlineData("openai", 429)]
    [InlineData("openai", 500)]
    [InlineData("google", 503)]
    public async Task A_provider_failure_leaves_as_HttpRequestException_with_the_status_and_no_provider_text(
        string empresa, int estado)
    {
        var registro = new RegistroDeCapturas();
        using var cable = new TransporteFalso(_ => Json((HttpStatusCode)estado, $$$"""
            {"type":"error","error":{"type":"x","message":"Incorrect API key provided: {{{ClaveSecreta}}}"} }
            """));

        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => ChatConLog(empresa, "m-1", cable, registro.Logger<ProveedorChat>())
                .CompletarAsync(Solicitud(), Ct));

        Assert.Equal(estado, (int?)error.StatusCode);

        // The provider text can echo part of the key, and the carril logs what it
        // catches: nothing of it may travel in the exception or reach a log.
        Assert.Null(error.InnerException);
        Assert.DoesNotContain("API key", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ClaveSecreta, error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(ClaveSecreta, registro.Todo(), StringComparison.Ordinal);

        // One attempt per call: the SDK's own retries are off.
        Assert.Equal(1, cable.Intentos);
    }

    [Theory]
    [InlineData("anthropic")]
    [InlineData("openai")]
    public async Task A_transport_failure_leaves_as_HttpRequestException_without_a_status(string empresa)
    {
        var cable = new CableQueFalla();

        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => Chat(empresa, "m-1", cable).CompletarAsync(Solicitud(), Ct));

        Assert.Null(error.StatusCode);
        Assert.Null(error.InnerException);
    }

    [Theory]
    [InlineData("anthropic")]
    [InlineData("openai")]
    public async Task The_callers_cancellation_is_not_translated(string empresa)
    {
        using var cable = new TransporteFalso(_ => RespuestaCompatible());
        using var cancelada = new CancellationTokenSource();
        await cancelada.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Chat(empresa, "m-1", cable).CompletarAsync(Solicitud(), cancelada.Token));
    }

    [Fact]
    public async Task A_translated_provider_failure_is_counted_by_the_breaker()
    {
        var breaker = new BreakerDelProveedor(
            Options.Create(new OpcionesAsistente { FallosParaAbrirElBreaker = 1, EsperaDelBreakerSegundos = 60 }),
            TimeProvider.System,
            NullLogger<BreakerDelProveedor>.Instance);
        using var cable = TransporteFalso.QueFalla(HttpStatusCode.TooManyRequests);
        var proveedor = new ProveedorConBreaker(
            Chat("anthropic", "claude-x", cable), breaker, TimeSpan.FromSeconds(30), TimeProvider.System);

        await Assert.ThrowsAsync<HttpRequestException>(() => proveedor.CompletarAsync(Solicitud(), Ct));

        Assert.Equal(EstadoDelBreaker.Abierto, breaker.Estado);
    }

    [Fact]
    public void An_unknown_company_has_no_chat_client()
    {
        Assert.Throws<InvalidOperationException>(
            () => ClientesDeChat.Crear("desconocida", "m", ClaveSecreta, new HttpClient()));
    }

    // -------------------------------------------------------------- doubles

    private sealed class CapturaDeUri(HttpMessageHandler interno, List<Uri> uris) : DelegatingHandler(interno)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken ct)
        {
            uris.Add(pedido.RequestUri!);
            return base.SendAsync(pedido, ct);
        }
    }

    private sealed class CableQueFalla : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken ct) =>
            throw new HttpRequestException("connection refused");
    }
}
