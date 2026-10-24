using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Microsoft.Extensions.AI;
using Modules.Asistente.Application;
using OpenAI;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// The one file that builds <see cref="IChatClient"/> instances for the cloud
/// companies, and therefore the only one (besides <c>ProveedorAnthropic.cs</c>)
/// that names the Anthropic and OpenAI SDKs (asistente-modelo-en-caliente, D2/D10;
/// <c>ArquitecturaAsistenteTests</c> enforces it).
/// </summary>
/// <remarks>
/// Every client is built over the module's named <see cref="HttpClient"/>, so
/// <c>GrabadorDeCassettes</c> and <c>ReintentoDeTransporte</c> still wrap each call,
/// and with the SDK's own retries zeroed so only <c>ReintentoDeTransporte</c>
/// retries. Mistral and Gemini are the OpenAI client over another base URL.
/// </remarks>
internal static class ClientesDeChat
{
    public const string EmpresaAnthropic = "anthropic";
    public const string EmpresaOpenAi = "openai";
    public const string EmpresaGoogle = "google";
    public const string EmpresaMistral = "mistral";

    /// <summary>The companies served here, in catalog order.</summary>
    public static readonly IReadOnlyList<string> Empresas = [EmpresaAnthropic, EmpresaGoogle, EmpresaMistral, EmpresaOpenAi];

    // The trailing slash matters: the SDK appends "chat/completions" to it.
    private static readonly Dictionary<string, Uri> BasesCompatiblesConOpenAi = new(StringComparer.Ordinal)
    {
        [EmpresaOpenAi] = new("https://api.openai.com/v1/"),
        [EmpresaMistral] = new("https://api.mistral.ai/v1/"),
        [EmpresaGoogle] = new("https://generativelanguage.googleapis.com/v1beta/openai/"),
    };

    /// <summary>A built client with what is needed to drive and dispose it.</summary>
    public sealed record ClienteDeChat(IChatClient Cliente, IConstructorDeChat Constructor, IDisposable? Dueno);

    /// <exception cref="InvalidOperationException">The company is not one of <see cref="Empresas"/>.</exception>
    public static ClienteDeChat Crear(string empresa, string modelo, string clave, HttpClient transporte)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelo);
        ArgumentException.ThrowIfNullOrWhiteSpace(clave);
        ArgumentNullException.ThrowIfNull(transporte);

        if (empresa == EmpresaAnthropic)
        {
            var sdk = new AnthropicClient
            {
                ApiKey = clave,
                HttpClient = transporte,
                // Zero: the SDK would multiply the requests of a turn by its own
                // retries, silently. One place retries (ReintentoDeTransporte).
                MaxRetries = 0,
            };

            // The ceiling and the model travel in the raw request (see the builder),
            // so the default passed here is never the one sent.
            return new ClienteDeChat(sdk.AsIChatClient(modelo, 1024), new ConstructorDeAnthropic(modelo), sdk);
        }

        if (BasesCompatiblesConOpenAi.TryGetValue(empresa, out var @base))
        {
            var sdk = new OpenAIClient(
                new ApiKeyCredential(clave),
                new OpenAIClientOptions
                {
                    Endpoint = @base,
                    Transport = new HttpClientPipelineTransport(transporte),
                    RetryPolicy = new ClientRetryPolicy(0),
                });

            return new ClienteDeChat(
                sdk.GetChatClient(modelo).AsIChatClient(), new ConstructorCompatibleConOpenAi(empresa), null);
        }

        throw new InvalidOperationException($"La empresa '{empresa}' no tiene un cliente de chat.");
    }

    /// <summary>
    /// Anthropic: <c>cache_control</c> on the stable prefix and <c>output_config.effort</c>
    /// go through the raw <see cref="MessageCreateParams"/>, exactly as
    /// <c>ProveedorAnthropic</c> builds them.
    /// </summary>
    /// <remarks>
    /// The user turn goes in <c>Messages</c> of the raw request and the
    /// <c>ChatMessage</c> list stays EMPTY: Microsoft.Extensions.AI appends the list
    /// to <c>Messages</c>, so filling both would send the question twice, and an
    /// empty <c>Messages</c> with the turn in the list would change its JSON shape
    /// from a plain string to a content block.
    ///
    /// It must NEVER use <c>ChatOptions.Reasoning</c>: that selects extended thinking
    /// (<c>thinking.budget_tokens</c>, inflated <c>max_tokens</c>), a different
    /// feature from <c>output_config.effort</c>.
    /// </remarks>
    private sealed class ConstructorDeAnthropic(string modelo) : IConstructorDeChat
    {
        public (IList<ChatMessage> Mensajes, ChatOptions Opciones) Armar(SolicitudAlModelo solicitud) => (
            [],
            new ChatOptions
            {
                // Also set here: the adapter reads the ceiling from the options.
                ModelId = modelo,
                MaxOutputTokens = solicitud.MaximoDeTokens,
                RawRepresentationFactory = _ => new MessageCreateParams
                {
                    Model = modelo,
                    MaxTokens = solicitud.MaximoDeTokens,
                    System = new List<TextBlockParam>
                    {
                        new()
                        {
                            Text = solicitud.PrefijoEstable,
                            CacheControl = new CacheControlEphemeral(),
                        },
                    },
                    // The user turn travels HERE and the ChatMessage list is empty, so
                    // the content keeps the plain-string shape ProveedorAnthropic sends
                    // (the cassette key reads `messages`).
                    Messages = [new() { Role = Role.User, Content = solicitud.Mensaje }],
                    // Minimo sends no field at all: models that do not deliberate
                    // reject output_config with a 400.
                    OutputConfig = solicitud.Esfuerzo == EsfuerzoDelModelo.Minimo
                        ? null
                        : new OutputConfig { Effort = EsfuerzoDe(solicitud.Esfuerzo) },
                },
            });

        public bool EsFallaDelProveedor(Exception excepcion, out HttpStatusCode? estado)
        {
            estado = (excepcion as AnthropicApiException)?.StatusCode;
            return excepcion is AnthropicException or HttpRequestException;
        }

        private static Effort EsfuerzoDe(EsfuerzoDelModelo esfuerzo) => esfuerzo switch
        {
            EsfuerzoDelModelo.Bajo => Effort.Low,
            EsfuerzoDelModelo.Medio => Effort.Medium,
            EsfuerzoDelModelo.Alto => Effort.High,
            EsfuerzoDelModelo.Maximo => Effort.Max,
            _ => throw new InvalidOperationException(
                $"Esfuerzo del puerto no mapeado en el adaptador: {esfuerzo}."),
        };
    }

    /// <summary>
    /// OpenAI, Mistral and Gemini through their OpenAI-compatible endpoints: the
    /// stable prefix is the system message (caching is automatic on the prefix) and
    /// the effort is the portable <c>reasoning_effort</c>.
    /// </summary>
    /// <remarks>
    /// The temperature does not travel, as with Anthropic: current reasoning models
    /// reject any value but their default. Mistral gets no effort: the spike proved
    /// the wire shape only, and a 4xx on an unsupported field would take the whole
    /// company down for the sake of a knob.
    /// </remarks>
    private sealed class ConstructorCompatibleConOpenAi(string empresa) : IConstructorDeChat
    {
        public (IList<ChatMessage> Mensajes, ChatOptions Opciones) Armar(SolicitudAlModelo solicitud) => (
            [
                new ChatMessage(ChatRole.System, solicitud.PrefijoEstable),
                new ChatMessage(ChatRole.User, solicitud.Mensaje),
            ],
            new ChatOptions
            {
                MaxOutputTokens = solicitud.MaximoDeTokens,
                Reasoning = EsfuerzoDe(solicitud.Esfuerzo),
            });

        public bool EsFallaDelProveedor(Exception excepcion, out HttpStatusCode? estado)
        {
            estado = excepcion is ClientResultException { Status: > 0 } resultado
                ? (HttpStatusCode)resultado.Status
                : null;
            return excepcion is ClientResultException or HttpRequestException;
        }

        private ReasoningOptions? EsfuerzoDe(EsfuerzoDelModelo esfuerzo)
        {
            if (empresa == EmpresaMistral || esfuerzo == EsfuerzoDelModelo.Minimo)
            {
                return null;
            }

            return new ReasoningOptions
            {
                Effort = esfuerzo switch
                {
                    EsfuerzoDelModelo.Bajo => ReasoningEffort.Low,
                    EsfuerzoDelModelo.Medio => ReasoningEffort.Medium,
                    // Maximo has no portable name above High that every endpoint accepts.
                    EsfuerzoDelModelo.Alto or EsfuerzoDelModelo.Maximo => ReasoningEffort.High,
                    _ => throw new InvalidOperationException(
                        $"Esfuerzo del puerto no mapeado en el adaptador: {esfuerzo}."),
                },
            };
        }
    }
}
