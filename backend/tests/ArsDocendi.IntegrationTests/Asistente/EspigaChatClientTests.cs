using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.AI;
using Modules.Asistente.Infrastructure;
using OpenAI;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Spike (asistente-modelo-en-caliente, task 2.1): can Microsoft.Extensions.AI carry
/// what the Asistente port needs? Results are recorded in design.md under D10.
/// No network, no real keys: every answer comes from a fake handler.
/// </summary>
public sealed class EspigaChatClientTests
{
    private const string Prefijo = "Schema of identity and designaciones. Answer with SQL only.";

    // ---------------------------------------------------------------- helpers

    private sealed class Captura(Func<int, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<(Uri? Uri, string Cuerpo, HttpRequestMessage Pedido)> Pedidos { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage pedido, CancellationToken ct)
        {
            var cuerpo = pedido.Content is null ? "" : await pedido.Content.ReadAsStringAsync(ct);
            Pedidos.Add((pedido.RequestUri, cuerpo, pedido));
            return responder(Pedidos.Count);
        }

        public JsonElement Cuerpo(int i = 0) => JsonDocument.Parse(Pedidos[i].Cuerpo).RootElement;
    }

    /// <summary>Stands in for GrabadorDeCassettes: records that it saw the request.</summary>
    private sealed class Testigo : DelegatingHandler
    {
        public int Vistos { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage pedido, CancellationToken ct)
        {
            Vistos++;
            return base.SendAsync(pedido, ct);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode estado, string cuerpo) =>
        new(estado) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage AnthropicOk(
        string stop = "end_turn", int entrada = 10, int salida = 5, int cache = 7, int creacion = 3) =>
        Json(HttpStatusCode.OK, $$$"""
            {"id":"msg_1","type":"message","role":"assistant","model":"claude-x",
             "content":[{"type":"text","text":"SELECT 1"}],
             "stop_reason":"{{{stop}}}","stop_sequence":null,
             "usage":{"input_tokens":{{{entrada}}},"output_tokens":{{{salida}}},
                      "cache_creation_input_tokens":{{{creacion}}},"cache_read_input_tokens":{{{cache}}} } }
            """);

    private static HttpResponseMessage OpenAiOk(
        string finish = "stop", int entrada = 10, int salida = 5, int cache = 7) =>
        Json(HttpStatusCode.OK, $$$"""
            {"id":"c1","object":"chat.completion","created":1,"model":"gpt-x",
             "choices":[{"index":0,"message":{"role":"assistant","content":"SELECT 1"},"finish_reason":"{{{finish}}}"}],
             "usage":{"prompt_tokens":{{{entrada}}},"completion_tokens":{{{salida}}},"total_tokens":{{{entrada + salida}}},
                      "prompt_tokens_details":{"cached_tokens":{{{cache}}} } } }
            """);

    private static IChatClient Anthropic(HttpClient http) =>
        new AnthropicClient { ApiKey = "k", HttpClient = http, MaxRetries = 0 }
            .AsIChatClient("claude-x", 256);

    private static IChatClient OpenAi(HttpClient http, string baseUrl = "https://api.openai.com/v1/") =>
        new OpenAIClient(
            new ApiKeyCredential("k"),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(baseUrl),
                Transport = new HttpClientPipelineTransport(http),
                RetryPolicy = new ClientRetryPolicy(0),
            })
            .GetChatClient("gpt-x")
            .AsIChatClient();

    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private static List<ChatMessage> Mensajes() =>
    [
        new(ChatRole.System, Prefijo),
        new(ChatRole.User, "Which teachers teach Databases?"),
    ];

    // ------------------------------------------------- (a) injected HttpClient

    [Fact]
    public async Task A_anthropic_client_sends_through_the_injected_handler_chain()
    {
        var falso = new Captura(n => n == 1 ? Json(HttpStatusCode.InternalServerError, "{}") : AnthropicOk());
        var testigo = new Testigo { InnerHandler = new ReintentoDeTransporte(
            3, TimeSpan.Zero, TimeSpan.Zero, new Random(1)) { InnerHandler = falso } };
        // Order: testigo (recorder stand-in) -> retry -> fake wire.
        using var http = new HttpClient(testigo);

        var respuesta = await Anthropic(http).GetResponseAsync(Mensajes(), null, Ct);

        Assert.Equal("SELECT 1", respuesta.Text);
        Assert.Equal(1, testigo.Vistos);              // outer handler saw the logical call
        Assert.Equal(2, falso.Pedidos.Count);         // the retry handler retried below it
        Assert.Equal("api.anthropic.com", falso.Pedidos[0].Uri!.Host);
    }

    [Fact]
    public async Task A_openai_client_sends_through_the_injected_handler_chain()
    {
        var falso = new Captura(n => n == 1 ? Json(HttpStatusCode.InternalServerError, "{}") : OpenAiOk());
        var testigo = new Testigo { InnerHandler = new ReintentoDeTransporte(
            3, TimeSpan.Zero, TimeSpan.Zero, new Random(1)) { InnerHandler = falso } };
        using var http = new HttpClient(testigo);

        var respuesta = await OpenAi(http).GetResponseAsync(Mensajes(), null, Ct);

        Assert.Equal("SELECT 1", respuesta.Text);
        Assert.Equal(1, testigo.Vistos);
        Assert.Equal(2, falso.Pedidos.Count);         // SDK retry is off (ClientRetryPolicy(0)); only ours retried
    }

    // --------------------------------------------- (b) cache_control on system

    [Fact]
    public async Task B_anthropic_AdditionalProperties_do_NOT_carry_cache_control_on_system_text()
    {
        // Negative result: the SDK adapter copies CacheControl from AdditionalProperties
        // for tools, not for system text. Neither key spelling reaches the wire.
        foreach (var clave in new[] { "cache_control", "CacheControl" })
        {
            var falso = new Captura(_ => AnthropicOk());
            using var http = new HttpClient(falso);
            var contenido = new TextContent(Prefijo)
            {
                AdditionalProperties = new() { [clave] = new CacheControlEphemeral() },
            };

            await Anthropic(http).GetResponseAsync(
                [new(ChatRole.System, [contenido]), new(ChatRole.User, "q")], null, Ct);

            Assert.False(falso.Cuerpo().GetProperty("system")[0].TryGetProperty("cache_control", out _));
        }
    }

    [Fact]
    public async Task B_anthropic_system_text_RawRepresentation_carries_cache_control()
    {
        var falso = new Captura(_ => AnthropicOk());
        using var http = new HttpClient(falso);
        var contenido = new TextContent(Prefijo)
        {
            RawRepresentation = new TextBlockParam
            {
                Text = Prefijo,
                CacheControl = new CacheControlEphemeral(),
            },
        };

        await Anthropic(http).GetResponseAsync(
            [new(ChatRole.System, [contenido]), new(ChatRole.User, "q")], null, Ct);

        var sistema = falso.Cuerpo().GetProperty("system")[0];
        Assert.Equal(Prefijo, sistema.GetProperty("text").GetString());
        Assert.Equal("ephemeral", sistema.GetProperty("cache_control").GetProperty("type").GetString());
    }

    // ------------------------------------------------------------- (c) effort

    [Fact]
    public async Task B_and_C_anthropic_RawRepresentationFactory_carries_cache_control_and_output_config_effort()
    {
        // The route ProveedorChat would use for Anthropic: one MessageCreateParams
        // with System (cache_control) and OutputConfig, Messages left empty so MEAI
        // appends the user turn (filling Messages here would duplicate it).
        var falso = new Captura(_ => AnthropicOk());
        using var http = new HttpClient(falso);
        var opciones = new ChatOptions
        {
            RawRepresentationFactory = _ => new MessageCreateParams
            {
                Model = "claude-x",
                MaxTokens = 256,
                System = new List<TextBlockParam>
                {
                    new() { Text = Prefijo, CacheControl = new CacheControlEphemeral() },
                },
                Messages = [],
                OutputConfig = new OutputConfig { Effort = Effort.Medium },
            },
        };

        await Anthropic(http).GetResponseAsync([new(ChatRole.User, "q")], opciones, Ct);

        var cuerpo = falso.Cuerpo();
        Assert.Equal("ephemeral",
            cuerpo.GetProperty("system")[0].GetProperty("cache_control").GetProperty("type").GetString());
        Assert.Equal("medium", cuerpo.GetProperty("output_config").GetProperty("effort").GetString());
        Assert.Equal(256, cuerpo.GetProperty("max_tokens").GetInt32());
        Assert.Single(cuerpo.GetProperty("messages").EnumerateArray());
        Assert.False(cuerpo.TryGetProperty("thinking", out _));
    }

    [Fact]
    public async Task C_anthropic_ChatOptions_Reasoning_maps_to_thinking_budget_NOT_output_config_effort()
    {
        // Negative result: the portable knob selects a different Anthropic feature
        // (extended thinking with budget_tokens, and it inflates max_tokens).
        var falso = new Captura(_ => AnthropicOk());
        using var http = new HttpClient(falso);

        await Anthropic(http).GetResponseAsync(Mensajes(),
            new ChatOptions { Reasoning = new ReasoningOptions { Effort = ReasoningEffort.Medium } }, Ct);

        var cuerpo = falso.Cuerpo();
        Assert.Equal("enabled", cuerpo.GetProperty("thinking").GetProperty("type").GetString());
        Assert.False(cuerpo.TryGetProperty("output_config", out _));
        Assert.True(cuerpo.GetProperty("max_tokens").GetInt32() > 256);
    }

    [Theory]
    [InlineData(ReasoningEffort.Low, "low")]
    [InlineData(ReasoningEffort.Medium, "medium")]
    [InlineData(ReasoningEffort.High, "high")]
    public async Task C_openai_compatible_ChatOptions_Reasoning_maps_to_reasoning_effort(
        ReasoningEffort esfuerzo, string esperado)
    {
        var falso = new Captura(_ => OpenAiOk());
        using var http = new HttpClient(falso);

        await OpenAi(http).GetResponseAsync(Mensajes(),
            new ChatOptions { Reasoning = new ReasoningOptions { Effort = esfuerzo }, MaxOutputTokens = 300 }, Ct);

        var cuerpo = falso.Cuerpo();
        Assert.Equal(esperado, cuerpo.GetProperty("reasoning_effort").GetString());
        Assert.Equal(300, cuerpo.GetProperty("max_completion_tokens").GetInt32());
    }

    [Fact]
    public async Task C_openai_compatible_without_Reasoning_omits_reasoning_effort()
    {
        // EsfuerzoDelModelo.Minimo: omit the field entirely, as ProveedorAnthropic does.
        var falso = new Captura(_ => OpenAiOk());
        using var http = new HttpClient(falso);

        await OpenAi(http).GetResponseAsync(Mensajes(), new ChatOptions(), Ct);

        Assert.False(falso.Cuerpo().TryGetProperty("reasoning_effort", out _));
    }

    // --------------------------------------------------------------- (d) gemini

    [Theory]
    [InlineData("https://generativelanguage.googleapis.com/v1beta/openai/",
        "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions")]
    [InlineData("https://api.mistral.ai/v1/", "https://api.mistral.ai/v1/chat/completions")]
    [InlineData("https://api.openai.com/v1/", "https://api.openai.com/v1/chat/completions")]
    public async Task D_openai_client_posts_to_chat_completions_under_the_given_base_url(
        string baseUrl, string esperada)
    {
        var falso = new Captura(_ => OpenAiOk());
        using var http = new HttpClient(falso);

        await OpenAi(http, baseUrl).GetResponseAsync(Mensajes(), null, Ct);

        Assert.Equal(esperada, falso.Pedidos[0].Uri!.ToString());
        Assert.Equal(HttpMethod.Post, falso.Pedidos[0].Pedido.Method);
        Assert.Equal("Bearer k", falso.Pedidos[0].Pedido.Headers.Authorization!.ToString());
    }

    // ------------------------------------------------------- (e) usage/finish

    [Fact]
    public async Task E_anthropic_exposes_usage_cache_tokens_and_length_finish_reason()
    {
        var falso = new Captura(_ => AnthropicOk("max_tokens", entrada: 10, salida: 5, cache: 7, creacion: 3));
        using var http = new HttpClient(falso);

        var r = await Anthropic(http).GetResponseAsync(Mensajes(), null, Ct);

        Assert.Equal(ChatFinishReason.Length, r.FinishReason);   // -> SeQuedoSinTokens
        Assert.Equal(5, r.Usage!.OutputTokenCount);
        Assert.Equal(7, r.Usage.CachedInputTokenCount);          // -> TokensDeCache
        // MEAI sums input + cache read + cache creation (10 + 7 + 3). ProveedorAnthropic
        // reports input + cache read only (17), so ProveedorChat must subtract the
        // creation count, exposed in AdditionalCounts, to keep parity (task 2.12).
        Assert.Equal(20, r.Usage.InputTokenCount);
        Assert.Equal(3, r.Usage.AdditionalCounts!["CacheCreationInputTokens"]);
    }

    [Fact]
    public async Task E_anthropic_end_turn_maps_to_stop()
    {
        var falso = new Captura(_ => AnthropicOk("end_turn"));
        using var http = new HttpClient(falso);

        var r = await Anthropic(http).GetResponseAsync(Mensajes(), null, Ct);

        Assert.Equal(ChatFinishReason.Stop, r.FinishReason);
    }

    [Fact]
    public async Task E_openai_compatible_exposes_usage_cache_tokens_and_length_finish_reason()
    {
        var falso = new Captura(_ => OpenAiOk("length", entrada: 10, salida: 5, cache: 7));
        using var http = new HttpClient(falso);

        var r = await OpenAi(http).GetResponseAsync(Mensajes(), null, Ct);

        Assert.Equal(ChatFinishReason.Length, r.FinishReason);
        Assert.Equal(5, r.Usage!.OutputTokenCount);
        Assert.Equal(7, r.Usage.CachedInputTokenCount);
        Assert.Equal(10, r.Usage.InputTokenCount);                // prompt_tokens already includes the cached 7
    }

    [Fact]
    public async Task E_openai_compatible_without_cache_details_leaves_cached_count_empty()
    {
        // Mistral and Gemini may omit prompt_tokens_details: the count must read as absent, not fail.
        var falso = new Captura(_ => Json(HttpStatusCode.OK, """
            {"id":"c","object":"chat.completion","created":1,"model":"m",
             "choices":[{"index":0,"message":{"role":"assistant","content":"x"},"finish_reason":"stop"}],
             "usage":{"prompt_tokens":4,"completion_tokens":2,"total_tokens":6}}
            """));
        using var http = new HttpClient(falso);

        var r = await OpenAi(http).GetResponseAsync(Mensajes(), null, Ct);

        Assert.Equal(ChatFinishReason.Stop, r.FinishReason);
        Assert.Equal(4, r.Usage!.InputTokenCount);
        Assert.True(r.Usage.CachedInputTokenCount is null or 0);
    }

    // -------------------------------------------------------------- (f) errors

    [Theory]
    [InlineData(401)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task F_anthropic_failures_surface_as_AnthropicApiException_with_status(int estado)
    {
        var falso = new Captura(_ => Json((HttpStatusCode)estado,
            """{"type":"error","error":{"type":"x","message":"boom"}}"""));
        using var http = new HttpClient(falso);

        var e = await Assert.ThrowsAnyAsync<Anthropic.Exceptions.AnthropicApiException>(
            () => Anthropic(http).GetResponseAsync(Mensajes(), null, Ct));

        Assert.Equal(estado, (int)e.StatusCode);
        Assert.Single(falso.Pedidos);                              // SDK retry off (MaxRetries = 0)
    }

    [Theory]
    [InlineData(401)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task F_openai_compatible_failures_surface_as_ClientResultException_with_status(int estado)
    {
        var falso = new Captura(_ => Json((HttpStatusCode)estado,
            """{"error":{"message":"boom"}}"""));
        using var http = new HttpClient(falso);

        var e = await Assert.ThrowsAsync<ClientResultException>(
            () => OpenAi(http).GetResponseAsync(Mensajes(), null, Ct));

        Assert.Equal(estado, e.Status);
        Assert.Single(falso.Pedidos);                              // SDK retry off (ClientRetryPolicy(0))
    }

    [Fact]
    public async Task F_neither_client_throws_HttpRequestException_so_ProveedorChat_must_translate()
    {
        var falso = new Captura(_ => Json(HttpStatusCode.InternalServerError, "{}"));
        using var http = new HttpClient(falso);

        var a = await Record.ExceptionAsync(() => Anthropic(http).GetResponseAsync(Mensajes(), null, Ct));
        var o = await Record.ExceptionAsync(() => OpenAi(http).GetResponseAsync(Mensajes(), null, Ct));

        Assert.IsNotType<HttpRequestException>(a);
        Assert.IsNotType<HttpRequestException>(o);
    }
}
