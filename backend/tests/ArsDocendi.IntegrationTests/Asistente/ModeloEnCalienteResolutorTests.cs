using System.Net;
using System.Text;
using System.Text.Json;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// The per-turn resolver of the runtime model selection against a real database:
/// selection read per turn, environment fallback, redaction, masking, keyed
/// breakers and the concurrency gate (asistente-modelo-en-caliente, tasks 2.5-2.10,
/// 2.13-2.15, 2.17-2.21). Every provider answer comes from a fake transport.
/// </summary>
public sealed class ModeloEnCalienteResolutorTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_resolutor")
{
    private const string Inferencia = "https://ia.example.net";
    private const string ServidorPropio = "http://arsdocendi-llm:8000/v1";
    private const string ClaveDeLaNube = "sk-live-ABCDEFGH12345678WXYZ";
    private const string ModeloDelEntorno = "modelo-del-entorno";
    private static readonly Guid Admin = Guid.Parse("a0000000-0000-4000-8000-0000000000a1");
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private static readonly SolicitudAlModelo Solicitud = new()
    {
        PrefijoEstable = "Schema.",
        Mensaje = "Which teachers teach Databases?",
        Temperatura = 0.0m,
        Esfuerzo = EsfuerzoDelModelo.Medio,
        MaximoDeTokens = 256,
    };

    // ---------------------------------------------------------------- doubles

    /// <summary>One fake wire for every company: answers by host and records what arrives.</summary>
    private sealed class Cable : HttpMessageHandler
    {
        private readonly List<(Uri Uri, string Cuerpo)> _pedidos = [];
        private readonly Lock _candado = new();
        private int _enVuelo;
        private int _maximoEnVuelo;

        /// <summary>When set, every call waits for it before answering (concurrency probes).</summary>
        public TaskCompletionSource? Retener { get; set; }

        /// <summary>Overrides the answer for requests whose body matches.</summary>
        public Func<Uri, string, HttpResponseMessage?>? Respuesta { get; set; }

        public IReadOnlyList<(Uri Uri, string Cuerpo)> Pedidos
        {
            get
            {
                lock (_candado)
                {
                    return [.. _pedidos];
                }
            }
        }

        public int EnVuelo => Volatile.Read(ref _enVuelo);

        public int MaximoEnVuelo => Volatile.Read(ref _maximoEnVuelo);

        public IEnumerable<string> Modelos(string host) => Pedidos
            .Where(p => p.Uri.Host == host)
            .Select(p => JsonDocument.Parse(p.Cuerpo).RootElement.GetProperty("model").GetString()!);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken ct)
        {
            var cuerpo = pedido.Content is null ? string.Empty : await pedido.Content.ReadAsStringAsync(ct);
            lock (_candado)
            {
                _pedidos.Add((pedido.RequestUri!, cuerpo));
            }

            var ahora = Interlocked.Increment(ref _enVuelo);
            int anterior;
            do
            {
                anterior = Volatile.Read(ref _maximoEnVuelo);
            }
            while (ahora > anterior && Interlocked.CompareExchange(ref _maximoEnVuelo, ahora, anterior) != anterior);

            try
            {
                if (Retener is { } espera)
                {
                    await espera.Task.WaitAsync(ct);
                }

                return Respuesta?.Invoke(pedido.RequestUri!, cuerpo) ?? Exito(pedido.RequestUri!.Host);
            }
            finally
            {
                Interlocked.Decrement(ref _enVuelo);
            }
        }

        private static HttpResponseMessage Exito(string host) => host switch
        {
            "api.openai.com" or "api.mistral.ai" => Json("""
                {"id":"c","object":"chat.completion","created":1,"model":"m",
                 "choices":[{"index":0,"message":{"role":"assistant","content":"SELECT 1"},"finish_reason":"stop"}],
                 "usage":{"prompt_tokens":10,"completion_tokens":5,"total_tokens":15}}
                """),
            "api.anthropic.com" => Json("""
                {"id":"m","type":"message","role":"assistant","model":"claude-x",
                 "content":[{"type":"text","text":"SELECT 1"}],"stop_reason":"end_turn","stop_sequence":null,
                 "usage":{"input_tokens":10,"output_tokens":5}}
                """),
            "ia.example.net" => Json("""
                {"model":"qwen3.5:9b","message":{"role":"assistant","content":"SELECT 1"},
                 "done":true,"done_reason":"stop","prompt_eval_count":10,"eval_count":5}
                """),
            "arsdocendi-llm" => Json("""
                {"id":"c","object":"chat.completion","model":"qwen3-8b",
                 "choices":[{"index":0,"message":{"role":"assistant","content":"SELECT 1"},"finish_reason":"stop"}],
                 "usage":{"prompt_tokens":10,"completion_tokens":5}}
                """),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };

        public static HttpResponseMessage Json(string cuerpo, HttpStatusCode estado = HttpStatusCode.OK) =>
            new(estado) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };
    }

    private sealed class AmbienteDePrueba : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "pruebas";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    // ------------------------------------------------------------ composition

    private ServiceProvider Componer(Cable cable, Action<Dictionary<string, string?>>? ajustar = null)
    {
        static string A(string nombre) => $"{OpcionesAsistente.Seccion}:{nombre}";

        var valores = new Dictionary<string, string?>
        {
            ["ConnectionStrings:ArsDocendi"] = Cadena,
            [A(nameof(OpcionesAsistente.Proveedor))] = "simulado",
            [A(nameof(OpcionesAsistente.Modelo))] = ModeloDelEntorno,
            [A(nameof(OpcionesAsistente.ClaveMaestra))] = CifradoDeCredencialesTests.ClaveMaestra(7),
            [A(nameof(OpcionesAsistente.MaximoDeIntentosDeTransporte))] = "1",
            [A(nameof(OpcionesAsistente.EsperaBaseMs))] = "1",
            [A(nameof(OpcionesAsistente.EsperaMaximaMs))] = "2",
        };
        ajustar?.Invoke(valores);

        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddSingleton<IHostEnvironment>(new AmbienteDePrueba());
        servicios.AddArsDocendiShared(configuracion);
        servicios.AddAsistenteModule(configuracion);
        servicios.AddHttpClient(ModuleExtensions.ClienteDelProveedor)
            .ConfigurePrimaryHttpMessageHandler(() => cable);

        return servicios.BuildServiceProvider();
    }

    private static void ConElServicioCompartido(Dictionary<string, string?> v)
    {
        v[$"{OpcionesInferencia.Seccion}:Url"] = Inferencia;
        v[$"{OpcionesInferencia.Seccion}:Usuario"] = "preview";
        v[$"{OpcionesInferencia.Seccion}:Contrasena"] = "clave";
        v[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.ContextoDelModelo)}"] = "16384";
    }

    private static async Task SeleccionarAsync(
        IServiceProvider raiz, string empresa, string modelo, string? clave = null)
    {
        await using var alcance = raiz.CreateAsyncScope();

        if (clave is not null)
        {
            await alcance.ServiceProvider.GetRequiredService<RepositorioDeCredenciales>()
                .GuardarAsync(empresa, clave, Admin, Ct);
        }

        Assert.True(await alcance.ServiceProvider.GetRequiredService<RepositorioDelModelo>()
            .AplicarSeleccionAsync(empresa, modelo, Admin, Ct));
    }

    private static async Task RevertirAsync(IServiceProvider raiz)
    {
        await using var alcance = raiz.CreateAsyncScope();
        await alcance.ServiceProvider.GetRequiredService<RepositorioDelModelo>().RestablecerAsync(Ct);
    }

    private static IProveedorDeModelo Generacion(IServiceScope turno) =>
        turno.ServiceProvider.GetRequiredService<IProveedorDeModelo>();

    private static IProveedorDeModelo Redaccion(IServiceScope turno) =>
        turno.ServiceProvider.GetRequiredKeyedService<IProveedorDeModelo>(ModuleExtensions.ProveedorDeRedaccion);

    // ------------------------------------- 2.13 / 2.14 the selection is per turn

    [Fact]
    public async Task Switching_the_stored_selection_changes_the_adapter_of_the_next_turn_without_restarting_the_host()
    {
        var cable = new Cable();
        await using var raiz = Componer(cable);

        using (var turno = raiz.CreateScope())
        {
            await Generacion(turno).CompletarAsync(Solicitud, Ct);
            Assert.Equal("simulado", Generacion(turno).Nombre);
        }

        await SeleccionarAsync(raiz, "openai", "gpt-5", ClaveDeLaNube);

        using (var turno = raiz.CreateScope())
        {
            var respuesta = await Generacion(turno).CompletarAsync(Solicitud, Ct);

            Assert.Equal("openai/gpt-5", Generacion(turno).Nombre);
            Assert.False(respuesta.EsSimulada);
            Assert.Equal("api.openai.com", Assert.Single(cable.Pedidos).Uri.Host);
        }

        await SeleccionarAsync(raiz, "anthropic", "claude-haiku-4-5", ClaveDeLaNube);

        using (var turno = raiz.CreateScope())
        {
            await Generacion(turno).CompletarAsync(Solicitud, Ct);

            Assert.Equal("anthropic/claude-haiku-4-5", Generacion(turno).Nombre);
            Assert.Equal("api.anthropic.com", cable.Pedidos[^1].Uri.Host);
        }

        // Reverting returns to server configuration, again from the next turn on.
        await RevertirAsync(raiz);

        using (var turno = raiz.CreateScope())
        {
            await Generacion(turno).CompletarAsync(Solicitud, Ct);
            Assert.Equal("simulado", Generacion(turno).Nombre);
            Assert.Equal(2, cable.Pedidos.Count);
        }
    }

    [Fact]
    public async Task A_turn_that_began_before_the_switch_finishes_on_the_old_model()
    {
        var cable = new Cable();
        await using var raiz = Componer(cable);
        await SeleccionarAsync(raiz, "openai", "gpt-5", ClaveDeLaNube);

        using var enCurso = raiz.CreateScope();
        await Generacion(enCurso).CompletarAsync(Solicitud, Ct);

        await SeleccionarAsync(raiz, "mistral", "mistral-large-latest", ClaveDeLaNube);

        // Same scope = same turn: its second call (the redaction after the
        // generation) is still the model the turn began with.
        await Redaccion(enCurso).CompletarAsync(Solicitud, Ct);
        Assert.Equal(["api.openai.com", "api.openai.com"], cable.Pedidos.Select(p => p.Uri.Host));
        Assert.Equal("openai/gpt-5", Redaccion(enCurso).Nombre);

        using var siguiente = raiz.CreateScope();
        await Generacion(siguiente).CompletarAsync(Solicitud, Ct);
        Assert.Equal("api.mistral.ai", cable.Pedidos[^1].Uri.Host);
    }

    // --------------------------------------------- 2.15 environment unchanged

    [Theory]
    [InlineData("simulado", "simulado")]
    [InlineData("anthropic", "api.anthropic.com")]
    [InlineData("local", "arsdocendi-llm")]
    [InlineData("ollama", "ia.example.net")]
    public async Task With_no_stored_selection_the_environment_configuration_is_used_unchanged(
        string proveedor, string destino)
    {
        var cable = new Cable();
        await using var raiz = Componer(cable, v =>
        {
            v[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.Proveedor)}"] = proveedor;
            v[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.ClaveDelProveedor)}"] = "sk-ant-del-entorno";
            v[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.UrlDelProveedorLocal)}"] = ServidorPropio;
            ConElServicioCompartido(v);
        });

        using var turno = raiz.CreateScope();
        await Generacion(turno).CompletarAsync(Solicitud, Ct);

        if (proveedor == "simulado")
        {
            Assert.Empty(cable.Pedidos);
        }
        else
        {
            Assert.Equal(destino, Assert.Single(cable.Pedidos).Uri.Host);
        }

        // The name the turn records is the one the environment adapter writes itself.
        var delEntorno = raiz.GetRequiredService<ProveedorBase>().Valor.Nombre;
        Assert.Equal(delEntorno, Generacion(turno).Nombre);
        Assert.Equal(
            delEntorno,
            turno.ServiceProvider.GetRequiredService<ResolutorDelProveedor>().IdentidadDelEntorno().NombreDeGeneracion);
    }

    // ------------------------------------------ 2.16 incomplete configuration

    [Theory]
    [InlineData("anthropic")]
    [InlineData("local")]
    [InlineData("ollama")]
    [InlineData("no-existe")]
    public async Task With_incomplete_configuration_the_host_starts_and_the_ping_answers(string proveedor)
    {
        var ct = Ct;
        using var host = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.UseSetting("AutenticacionMicrosoft:Habilitada", "true");
                builder.UseSetting("AutenticacionMicrosoft:ClientId", "cliente-pruebas");
                builder.UseSetting("AutenticacionMicrosoft:ClientSecret", "secreto-pruebas");
                // A port where nobody listens: any read at startup would fail.
                builder.UseSetting(
                    "ConnectionStrings:ArsDocendi",
                    "Host=127.0.0.1;Port=1;Database=no_existe;Username=nadie;Password=nada;Timeout=1");
                builder.UseSetting($"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.Proveedor)}", proveedor);
                builder.UseSetting($"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.ClaveMaestra)}", string.Empty);
            });
        using var cliente = host.CreateClient();

        using var respuesta = await cliente.GetAsync("/api/asistente/ping", ct);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
        Assert.Contains("\"ok\"", cuerpo, StringComparison.Ordinal);
    }

    // ------------------------------------------------- 2.17 keyed breakers

    [Fact]
    public async Task An_open_breaker_for_one_name_does_not_affect_another_and_availability_follows_the_resolved_name()
    {
        var cable = new Cable
        {
            // Only gpt-5 fails; gpt-5-mini answers.
            Respuesta = (_, cuerpo) => cuerpo.Contains("\"gpt-5-mini\"", StringComparison.Ordinal)
                ? null
                : Cable.Json("{}", HttpStatusCode.InternalServerError),
        };
        await using var raiz = Componer(cable, v =>
        {
            v[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.FallosParaAbrirElBreaker)}"] = "1";
            v[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.EsperaDelBreakerSegundos)}"] = "600";
        });

        async Task<MotivoSinModelo> DisponibilidadAsync(IServiceScope turno) =>
            await new DisponibilidadDelModeloReal(
                new AccesoAlAsistenteFalso(),
                new CuotaDeActorFalsa(0, TimeProvider.System),
                new PresupuestoOrganizacionalFalso(),
                new DisponibilidadDelModuloFalsa(),
                turno.ServiceProvider.GetRequiredService<IBreakerDelTurno>())
                .ConsultarAsync(Admin, Ct);

        await SeleccionarAsync(raiz, "openai", "gpt-5", ClaveDeLaNube);

        using (var turno = raiz.CreateScope())
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => Generacion(turno).CompletarAsync(Solicitud, Ct));
            Assert.Equal(MotivoSinModelo.ProveedorCaido, await DisponibilidadAsync(turno));
        }

        // Another name: not inherited.
        await SeleccionarAsync(raiz, "openai", "gpt-5-mini");
        using (var turno = raiz.CreateScope())
        {
            Assert.Equal(MotivoSinModelo.Ninguno, await DisponibilidadAsync(turno));
            await Generacion(turno).CompletarAsync(Solicitud, Ct);
        }

        // Back to the first: its breaker is still open, it was not reset by the detour.
        await SeleccionarAsync(raiz, "openai", "gpt-5");
        using (var turno = raiz.CreateScope())
        {
            Assert.Equal(MotivoSinModelo.ProveedorCaido, await DisponibilidadAsync(turno));
            await Assert.ThrowsAsync<ProveedorNoDisponible>(() => Generacion(turno).CompletarAsync(Solicitud, Ct));
        }

        var registro = raiz.GetRequiredService<RegistroDeBreakers>();
        Assert.Equal(EstadoDelBreaker.Abierto, registro.Para("openai/gpt-5").Estado);
        Assert.Equal(EstadoDelBreaker.Cerrado, registro.Para("openai/gpt-5-mini").Estado);
    }

    // --------------------------------------- 2.18 one model, both roles

    [Fact]
    public async Task The_selected_model_performs_both_generation_and_redaction_and_the_environment_redaction_model_is_ignored()
    {
        var cable = new Cable();
        await using var raiz = Componer(cable, v =>
        {
            v[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.Proveedor)}"] = "anthropic";
            v[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.ClaveDelProveedor)}"] = "sk-ant-del-entorno";
            v[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.ModeloDeRedaccion)}"] = "claude-del-entorno-chico";
        });
        await SeleccionarAsync(raiz, "openai", "gpt-5", ClaveDeLaNube);

        using var turno = raiz.CreateScope();
        await Generacion(turno).CompletarAsync(Solicitud, Ct);
        await Redaccion(turno).CompletarAsync(Solicitud, Ct);

        Assert.Equal(["gpt-5", "gpt-5"], cable.Modelos("api.openai.com"));
        Assert.Empty(cable.Modelos("api.anthropic.com"));
        Assert.Equal("openai/gpt-5", Generacion(turno).Nombre);
        Assert.Equal("openai/gpt-5", Redaccion(turno).Nombre);
    }

    [Fact]
    public async Task Without_a_selection_the_environment_redaction_model_still_serves_redaction()
    {
        var cable = new Cable();
        await using var raiz = Componer(cable, v =>
        {
            v[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.Proveedor)}"] = "anthropic";
            v[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.ClaveDelProveedor)}"] = "sk-ant-del-entorno";
            v[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.ModeloDeRedaccion)}"] = "claude-del-entorno-chico";
        });

        using var turno = raiz.CreateScope();
        await Generacion(turno).CompletarAsync(Solicitud, Ct);
        await Redaccion(turno).CompletarAsync(Solicitud, Ct);

        Assert.Equal([ModeloDelEntorno, "claude-del-entorno-chico"], cable.Modelos("api.anthropic.com"));
        Assert.Equal("anthropic/claude-del-entorno-chico", Redaccion(turno).Nombre);
    }

    // ------------------------------------------------------- 2.19 masking

    [Fact]
    public async Task Selecting_a_cloud_company_masks_again_and_selecting_own_hardware_unmasks()
    {
        var cable = new Cable();
        await using var raiz = Componer(cable, v =>
        {
            v[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.Proveedor)}"] = "ollama";
            v[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.RedaccionSinEnmascarar)}"] = "true";
            ConElServicioCompartido(v);
        });

        async Task<bool> SinEnmascararAsync()
        {
            using var turno = raiz.CreateScope();
            return await turno.ServiceProvider.GetRequiredService<IEnmascaradoDelTurno>()
                .RedaccionSinEnmascararAsync(Ct);
        }

        Assert.True(await SinEnmascararAsync());                          // environment: own hardware, option on

        await SeleccionarAsync(raiz, "openai", "gpt-5", ClaveDeLaNube);
        Assert.False(await SinEnmascararAsync());                         // a cloud company always masks

        await SeleccionarAsync(raiz, "ollama", "qwen3.5:9b");
        Assert.True(await SinEnmascararAsync());                          // own hardware again

        await SeleccionarAsync(raiz, "local", "qwen3-8b");
        Assert.True(await SinEnmascararAsync());

        await RevertirAsync(raiz);
        Assert.True(await SinEnmascararAsync());
    }

    [Fact]
    public async Task With_the_option_off_nothing_unmasks_whatever_the_adapter_is()
    {
        var cable = new Cable();
        await using var raiz = Componer(cable, ConElServicioCompartido);

        async Task<bool> SinEnmascararAsync()
        {
            using var turno = raiz.CreateScope();
            return await turno.ServiceProvider.GetRequiredService<IEnmascaradoDelTurno>()
                .RedaccionSinEnmascararAsync(Ct);
        }

        // Option off: masked whatever the adapter is.
        await SeleccionarAsync(raiz, "ollama", "qwen3.5:9b");
        Assert.False(await SinEnmascararAsync());
    }

    // ------------------------------------------------------ 2.20 the gate

    [Theory]
    [InlineData("ollama", "qwen3.5:9b", true)]
    [InlineData("local", "qwen3-8b", true)]
    [InlineData("openai", "gpt-5", false)]
    [InlineData("anthropic", "claude-sonnet-5", false)]
    public async Task The_concurrency_gate_wraps_own_hardware_selections_and_not_cloud_ones(
        string empresa, string modelo, bool conCompuerta)
    {
        var cable = new Cable { Retener = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        await using var raiz = Componer(cable, v =>
        {
            v[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.MaximoDeLlamadasConcurrentes)}"] = "1";
            v[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.UrlDelProveedorLocal)}"] = ServidorPropio;
            ConElServicioCompartido(v);
        });
        await SeleccionarAsync(raiz, empresa, modelo, clave: conCompuerta ? null : ClaveDeLaNube);

        using var primero = raiz.CreateScope();
        using var segundo = raiz.CreateScope();
        var llamada1 = Generacion(primero).CompletarAsync(Solicitud, Ct);
        var llamada2 = Generacion(segundo).CompletarAsync(Solicitud, Ct);

        var compuerta = raiz.GetRequiredService<CompuertaDelModelo>();

        if (conCompuerta)
        {
            // One call is on the wire and the other waits in the gate's queue.
            await EsperarAsync(() => compuerta.EnEspera == 1 && cable.EnVuelo == 1);
            Assert.Equal(1, cable.EnVuelo);
        }
        else
        {
            // Both reach the wire at once: nothing serializes them.
            await EsperarAsync(() => cable.EnVuelo == 2);
            Assert.Equal(0, compuerta.EnEspera);
            Assert.Equal(0, compuerta.EnCurso);
        }

        cable.Retener.SetResult();
        await Task.WhenAll(llamada1, llamada2);

        Assert.Equal(conCompuerta ? 1 : 2, cable.MaximoEnVuelo);
    }

    // ------------------------------------------ 2.21 saturation is not a failure

    [Fact]
    public async Task A_saturated_shared_service_does_not_open_the_breaker_but_a_cloud_rate_limit_does()
    {
        var cable = new Cable
        {
            Respuesta = (uri, _) => uri.Host == "ia.example.net"
                ? Cable.Json("{}", HttpStatusCode.ServiceUnavailable)
                : Cable.Json("{}", HttpStatusCode.TooManyRequests),
        };
        await using var raiz = Componer(cable, v =>
        {
            v[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.FallosParaAbrirElBreaker)}"] = "1";
            ConElServicioCompartido(v);
        });
        var registro = raiz.GetRequiredService<RegistroDeBreakers>();

        await SeleccionarAsync(raiz, "ollama", "qwen3.5:9b");
        for (var i = 0; i < 3; i++)
        {
            using var turno = raiz.CreateScope();
            await Assert.ThrowsAsync<ProveedorSaturado>(() => Generacion(turno).CompletarAsync(Solicitud, Ct));
        }

        Assert.Equal(EstadoDelBreaker.Cerrado, registro.Para("ollama/qwen3.5:9b").Estado);

        await SeleccionarAsync(raiz, "openai", "gpt-5", ClaveDeLaNube);
        using (var turno = raiz.CreateScope())
        {
            var error = await Assert.ThrowsAsync<HttpRequestException>(
                () => Generacion(turno).CompletarAsync(Solicitud, Ct));
            Assert.Equal(HttpStatusCode.TooManyRequests, error.StatusCode);
        }

        Assert.Equal(EstadoDelBreaker.Abierto, registro.Para("openai/gpt-5").Estado);
    }

    // --------------------------------------- selection that cannot be built

    [Fact]
    public async Task A_cloud_selection_without_a_stored_key_degrades_as_a_provider_failure_and_the_breaker_records_it()
    {
        var cable = new Cable();
        await using var raiz = Componer(cable, v =>
            v[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.FallosParaAbrirElBreaker)}"] = "1");
        await SeleccionarAsync(raiz, "openai", "gpt-5");                 // no key saved

        using var turno = raiz.CreateScope();
        await Assert.ThrowsAsync<HttpRequestException>(() => Generacion(turno).CompletarAsync(Solicitud, Ct));

        Assert.Empty(cable.Pedidos);
        Assert.Equal(EstadoDelBreaker.Abierto, raiz.GetRequiredService<RegistroDeBreakers>().Para("openai/gpt-5").Estado);
    }

    [Fact]
    public async Task A_cloud_selection_with_no_master_key_degrades_without_leaking_anything()
    {
        var cable = new Cable();
        await using var raiz = Componer(cable);
        await SeleccionarAsync(raiz, "openai", "gpt-5", ClaveDeLaNube);

        // Same database, but this host has no master key: the key cannot be opened.
        await using var sinClave = Componer(cable, v =>
            v[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.ClaveMaestra)}"] = string.Empty);
        using var turno = sinClave.CreateScope();

        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => Generacion(turno).CompletarAsync(Solicitud, Ct));

        Assert.DoesNotContain(ClaveDeLaNube, error.ToString(), StringComparison.Ordinal);
        Assert.Empty(cable.Pedidos);
    }

    [Fact]
    public async Task A_turn_that_never_calls_the_model_never_builds_an_adapter_so_incomplete_configuration_does_not_stop_it()
    {
        var cable = new Cable();
        await using var raiz = Componer(cable, v =>
            v[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.Proveedor)}"] = "anthropic");   // no key

        using var turno = raiz.CreateScope();

        // Availability, masking and the name all work: only a real call needs the adapter.
        Assert.Equal(EstadoDelBreaker.Cerrado,
            await turno.ServiceProvider.GetRequiredService<IBreakerDelTurno>().EstadoAsync(Ct));
        Assert.False(await turno.ServiceProvider.GetRequiredService<IEnmascaradoDelTurno>()
            .RedaccionSinEnmascararAsync(Ct));
        Assert.Equal($"anthropic/{ModeloDelEntorno}", Generacion(turno).Nombre);
    }

    // -------------------------------------------------------- 2.10 the cache

    [Fact]
    public void Clearing_the_generated_query_cache_drops_every_entry()
    {
        var cache = new CacheDeConsultasGeneradas(TimeProvider.System);
        var generacion = new GeneracionDeSql(true, "SELECT 1", "reasoning", "category");
        cache.Guardar("¿cuántos docentes hay?", false, new DateOnly(2027, 1, 1), generacion, TimeSpan.FromMinutes(5));
        cache.Guardar("¿y materias?", false, new DateOnly(2027, 1, 1), generacion, TimeSpan.FromMinutes(5));
        Assert.Equal(2, cache.Cantidad);

        cache.Vaciar();

        Assert.Equal(0, cache.Cantidad);
        Assert.Null(cache.Buscar("¿cuántos docentes hay?", false, new DateOnly(2027, 1, 1)));
    }

    // --------------------------------------------------------------- support

    private static async Task EsperarAsync(Func<bool> condicion)
    {
        var limite = DateTime.UtcNow.AddSeconds(10);
        while (!condicion())
        {
            if (DateTime.UtcNow > limite)
            {
                Assert.Fail("The condition was not reached in 10 seconds.");
            }

            await Task.Delay(10, Ct);
        }
    }
}
