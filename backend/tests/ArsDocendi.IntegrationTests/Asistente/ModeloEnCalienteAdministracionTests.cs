using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modules.Asistente;
using Modules.Asistente.Api;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// The model administration endpoints over real HTTP and a real database
/// (asistente-modelo-en-caliente, slice 3: tasks 3.1-3.12 and 3.14). Every provider
/// answer comes from a stubbed transport: no network, no real keys.
/// </summary>
public sealed class ModeloEnCalienteAdministracionTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_modelo_administracion")
{
    private const string Ruta = "/api/asistente/administracion";
    private const string Inferencia = "https://ia.example.net";
    private const string ServidorPropio = "http://arsdocendi-llm:8000/v1";
    private const string ClaveA = "sk-live-ABCDEFGH12345678WXYZ";
    private const string ClaveB = "sk-live-ZYXWVUTS87654321QRST";
    private const string ContrasenaDelServidor = "contrasena-del-servicio-compartido";

    // sys_admin already has asistente.administrar; secretaria does not.
    private static readonly Guid Sistemas = Guid.Parse("a0000000-0000-4000-8000-000000000007");
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------- doubles

    /// <summary>The one fake wire for every company: records every request, answers by host.</summary>
    private sealed class Cable : HttpMessageHandler
    {
        private readonly List<(Uri Uri, string Autorizacion, string Cuerpo)> _pedidos = [];
        private readonly Lock _candado = new();

        /// <summary>Overrides the answer; null falls back to the success of the host.</summary>
        public Func<Uri, HttpResponseMessage?>? Respuesta { get; set; }

        /// <summary>Throws a transport failure for every call.</summary>
        public bool CaerEnElTransporte { get; set; }

        public IReadOnlyList<(Uri Uri, string Autorizacion, string Cuerpo)> Pedidos
        {
            get
            {
                lock (_candado)
                {
                    return [.. _pedidos];
                }
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken ct)
        {
            var cuerpo = pedido.Content is null ? string.Empty : await pedido.Content.ReadAsStringAsync(ct);
            var autorizacion = pedido.Headers.Authorization?.ToString()
                ?? (pedido.Headers.TryGetValues("x-api-key", out var valores) ? string.Join(',', valores) : string.Empty);

            lock (_candado)
            {
                _pedidos.Add((pedido.RequestUri!, autorizacion, cuerpo));
            }

            if (CaerEnElTransporte)
            {
                throw new HttpRequestException("connection refused");
            }

            return Respuesta?.Invoke(pedido.RequestUri!) ?? Exito(pedido.RequestUri!.Host);
        }

        private static HttpResponseMessage Exito(string host) => host switch
        {
            "api.openai.com" or "api.mistral.ai" => Json("""
                {"id":"c","object":"chat.completion","created":1,"model":"m",
                 "choices":[{"index":0,"message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}],
                 "usage":{"prompt_tokens":10,"completion_tokens":1,"total_tokens":11}}
                """),
            "api.anthropic.com" => Json("""
                {"id":"m","type":"message","role":"assistant","model":"claude-x",
                 "content":[{"type":"text","text":"ok"}],"stop_reason":"end_turn","stop_sequence":null,
                 "usage":{"input_tokens":10,"output_tokens":1}}
                """),
            "ia.example.net" => Json("""
                {"model":"qwen3.5:9b","message":{"role":"assistant","content":"ok"},
                 "done":true,"done_reason":"stop","prompt_eval_count":10,"eval_count":1}
                """),
            "arsdocendi-llm" => Json("""
                {"id":"c","object":"chat.completion","model":"qwen3-8b",
                 "choices":[{"index":0,"message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}],
                 "usage":{"prompt_tokens":10,"completion_tokens":1}}
                """),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };

        public static HttpResponseMessage Json(string cuerpo, HttpStatusCode estado = HttpStatusCode.OK) =>
            new(estado) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };
    }

    /// <summary>A provider rejection whose body echoes the key, as real providers sometimes do.</summary>
    private static HttpResponseMessage Rechazo(HttpStatusCode estado, string tipo = "error", string mensaje = "rejected") =>
        Cable.Json(
            "{\"error\":{\"message\":\"" + mensaje + " (key sk-live-ECHOED-BY-PROVIDER)\",\"type\":\"" + tipo
            + "\",\"code\":\"" + tipo + "\"}}",
            estado);

    // --------------------------------------------------------------- composition

    /// <summary>Collects every formatted log line and exception text of the host.</summary>
    private sealed class Registro : ILoggerProvider
    {
        private readonly List<string> _lineas = [];
        private readonly Lock _candado = new();

        public IReadOnlyList<string> Lineas
        {
            get
            {
                lock (_candado)
                {
                    return [.. _lineas];
                }
            }
        }

        public ILogger CreateLogger(string categoria) => new Anotador(this);

        public void Dispose()
        {
        }

        private sealed class Anotador(Registro dueno) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel nivel) => true;

            public void Log<TState>(
                LogLevel nivel, EventId id, TState estado, Exception? excepcion, Func<TState, Exception?, string> formato)
            {
                lock (dueno._candado)
                {
                    dueno._lineas.Add(formato(estado, excepcion) + " " + excepcion);
                }
            }
        }
    }

    private WebApplicationFactory<Program> CrearHost(
        Cable cable, bool conServicioCompartido = false, bool conServidorPropio = false,
        bool conClaveMaestra = true, int compuerta = 0, Registro? registro = null)
    {
        string A(string nombre) => $"{OpcionesAsistente.Seccion}:{nombre}";

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting($"ConnectionStrings:{CadenaDuena.Clave}", Cadena);
            builder.UseSetting($"{AutenticacionDesarrolloOptions.Seccion}:Enabled", "true");
            builder.UseSetting(A(nameof(OpcionesAsistente.RolSoloLectura)), RolSoloLectura);
            builder.UseSetting(A(nameof(OpcionesAsistente.RolSoloLecturaPii)), RolSoloLecturaPii);
            builder.UseSetting(A(nameof(OpcionesAsistente.PasswordSoloLectura)), PostgresFixture.PasswordDeRol);
            builder.UseSetting(A(nameof(OpcionesAsistente.PasswordSoloLecturaPii)), PostgresFixture.PasswordDeRol);
            builder.UseSetting(A(nameof(OpcionesAsistente.MaximoDeIntentosDeTransporte)), "1");
            builder.UseSetting(A(nameof(OpcionesAsistente.EsperaBaseMs)), "1");
            builder.UseSetting(A(nameof(OpcionesAsistente.EsperaMaximaMs)), "2");
            builder.UseSetting(A(nameof(OpcionesAsistente.MaximoDeLlamadasConcurrentes)), compuerta.ToString());
            builder.UseSetting(
                A(nameof(OpcionesAsistente.ClaveMaestra)),
                conClaveMaestra ? CifradoDeCredencialesTests.ClaveMaestra(7) : string.Empty);

            // Explicit blanks: the Development host also reads the repository's .env, and
            // "which local servers are configured" must come from this test alone.
            builder.UseSetting(A(nameof(OpcionesAsistente.Proveedor)), "simulado");
            builder.UseSetting($"{OpcionesInferencia.Seccion}:Url", string.Empty);
            builder.UseSetting(A(nameof(OpcionesAsistente.UrlDelProveedorLocal)), string.Empty);

            if (conServicioCompartido)
            {
                builder.UseSetting($"{OpcionesInferencia.Seccion}:Url", Inferencia);
                builder.UseSetting($"{OpcionesInferencia.Seccion}:Usuario", "preview");
                builder.UseSetting($"{OpcionesInferencia.Seccion}:Contrasena", ContrasenaDelServidor);
                builder.UseSetting(A(nameof(OpcionesAsistente.ContextoDelModelo)), "16384");
            }

            if (conServidorPropio)
            {
                builder.UseSetting(A(nameof(OpcionesAsistente.UrlDelProveedorLocal)), ServidorPropio);
            }

            builder.ConfigureTestServices(servicios =>
            {
                servicios.AddHttpClient(ModuleExtensions.ClienteDelProveedor)
                    .ConfigurePrimaryHttpMessageHandler(() => cable);

                if (registro is not null)
                {
                    // The host logs through Serilog, which ignores extra providers: replace the
                    // factory so every ILogger<T> of the module reaches the collector.
                    servicios.AddSingleton<ILoggerFactory>(new LoggerFactory([registro]));
                    servicios.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
                }
            });
        });
    }

    private static HttpClient Cliente(WebApplicationFactory<Program> host, Guid usuario, string rol)
    {
        var cliente = host.CreateClient();
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, usuario.ToString());
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, rol);
        return cliente;
    }

    private static HttpClient Admin(WebApplicationFactory<Program> host) => Cliente(host, Sistemas, "sys_admin");

    private static async Task<JsonElement> CuerpoAsync(HttpResponseMessage respuesta) =>
        JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(Ct)).RootElement;

    private static Task<HttpResponseMessage> Probar(HttpClient c, string empresa, string modelo, string? clave = null) =>
        c.PostAsJsonAsync($"{Ruta}/modelo/prueba", new PedidoDeModeloDto(empresa, modelo, clave), Ct);

    private static Task<HttpResponseMessage> Aplicar(HttpClient c, string empresa, string modelo, string? clave = null) =>
        c.PutAsJsonAsync($"{Ruta}/modelo", new PedidoDeModeloDto(empresa, modelo, clave), Ct);

    private async Task<(string Empresa, string Modelo)?> SeleccionGuardadaAsync()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand("SELECT empresa, modelo FROM asistente.modelo_vigente", conexion);
        await using var lector = await comando.ExecuteReaderAsync(Ct);
        return await lector.ReadAsync(Ct) ? (lector.GetString(0), lector.GetString(1)) : null;
    }

    private async Task<List<(string Accion, string? Antes, string Despues)>> AuditoriaAsync()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT accion, antes, despues FROM asistente.auditoria_administracion ORDER BY ocurrido_en", conexion);
        await using var lector = await comando.ExecuteReaderAsync(Ct);

        var filas = new List<(string, string?, string)>();
        while (await lector.ReadAsync(Ct))
        {
            filas.Add((lector.GetString(0), lector.IsDBNull(1) ? null : lector.GetString(1), lector.GetString(2)));
        }

        return filas;
    }

    private async Task<string?> PistaGuardadaAsync(string empresa)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT pista FROM asistente.credencial_de_proveedor WHERE empresa = @e", conexion);
        comando.Parameters.AddWithValue("e", empresa);
        return await comando.ExecuteScalarAsync(Ct) as string;
    }

    private static async Task<string?> ClaveDescifradaAsync(WebApplicationFactory<Program> host, string empresa)
    {
        await using var alcance = host.Services.CreateAsyncScope();
        return (await alcance.ServiceProvider.GetRequiredService<RepositorioDeCredenciales>()
            .AbrirAsync(empresa, Ct))?.Clave;
    }

    private async Task AssertNadaCambioAsync()
    {
        Assert.Null(await SeleccionGuardadaAsync());
        Assert.Equal(0L, await EscalarAsync<long>("SELECT count(*) FROM asistente.credencial_de_proveedor"));
        Assert.Empty(await AuditoriaAsync());
    }

    // ------------------------------------------------------------ 3.8 permission

    public static TheoryData<string, string> TodasLasRutas() => new()
    {
        { "GET", "/modelo" },
        { "POST", "/modelo/prueba" },
        { "PUT", "/modelo" },
        { "DELETE", "/modelo" },
        { "POST", "/credenciales/openai/reemplazo" },
    };

    [Theory]
    [MemberData(nameof(TodasLasRutas))]
    public async Task Every_route_answers_403_without_the_administration_permission_and_changes_nothing(
        string metodo, string ruta)
    {
        await SembrarAsync(Ct);
        var cable = new Cable();
        using var host = CrearHost(cable);
        using var cliente = Cliente(host, Secretaria, "secretaria");

        using var pedido = new HttpRequestMessage(new HttpMethod(metodo), Ruta + ruta);
        if (metodo is "POST" or "PUT")
        {
            pedido.Content = JsonContent.Create(ruta.Contains("reemplazo", StringComparison.Ordinal)
                ? (object)new PedidoDeReemplazoDeClaveDto(ClaveA)
                : new PedidoDeModeloDto("openai", "gpt-5", ClaveA));
        }

        using var respuesta = await cliente.SendAsync(pedido, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Empty(cable.Pedidos);
        await AssertNadaCambioAsync();
    }

    [Fact]
    public async Task No_administration_route_collides_with_another()
    {
        await SembrarAsync(Ct);
        using var host = CrearHost(new Cable());
        using var cliente = Admin(host);
        _ = await cliente.GetAsync($"{Ruta}/modelo", Ct);

        var rutas = host.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText!.StartsWith("api/asistente/administracion", StringComparison.Ordinal))
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["*"])
                .Select(m => $"{m} {e.RoutePattern.RawText}"))
            .ToList();

        Assert.Equal(rutas.Count, rutas.Distinct().Count());

        foreach (var propia in new[]
        {
            "GET api/asistente/administracion/modelo",
            "POST api/asistente/administracion/modelo/prueba",
            "PUT api/asistente/administracion/modelo",
            "DELETE api/asistente/administracion/modelo",
            "POST api/asistente/administracion/credenciales/{empresa}/reemplazo",
        })
        {
            Assert.Contains(propia, rutas);
        }

        // The pre-existing surface is untouched.
        foreach (var previa in new[] { "uso", "presupuestos", "servidor-local", "tope-organizacional", "mantenimiento" })
        {
            Assert.Contains(rutas, r => r.Contains($"administracion/{previa}", StringComparison.Ordinal));
        }
    }

    // ----------------------------------------------------------------- 3.1 read

    [Fact]
    public async Task The_screen_state_groups_the_catalog_by_company_with_key_status_and_never_a_key_or_url()
    {
        await SembrarAsync(Ct);
        var cable = new Cable();
        using var host = CrearHost(cable, conServicioCompartido: true);
        using var cliente = Admin(host);

        Assert.Equal(HttpStatusCode.OK, (await Aplicar(cliente, "openai", "gpt-5", ClaveA)).StatusCode);

        using var respuesta = await cliente.GetAsync($"{Ruta}/modelo", Ct);
        var texto = await respuesta.Content.ReadAsStringAsync(Ct);
        var cuerpo = JsonDocument.Parse(texto).RootElement;

        Assert.Equal("pantalla", cuerpo.GetProperty("enUso").GetProperty("origen").GetString());
        Assert.Equal("openai", cuerpo.GetProperty("enUso").GetProperty("empresa").GetString());
        Assert.Equal(["ollama"], cuerpo.GetProperty("servidoresLocales").EnumerateArray().Select(e => e.GetString()));

        var grupos = cuerpo.GetProperty("catalogo").EnumerateArray().ToDictionary(g => g.GetProperty("empresa").GetString()!);
        Assert.Equal("guardada", grupos["openai"].GetProperty("credencial").GetProperty("estado").GetString());
        Assert.Equal("WXYZ", grupos["openai"].GetProperty("credencial").GetProperty("pista").GetString());
        Assert.Equal("faltante", grupos["anthropic"].GetProperty("credencial").GetProperty("estado").GetString());
        Assert.Contains("gpt-5-mini", grupos["openai"].GetProperty("modelos").EnumerateArray().Select(m => m.GetString()));
        Assert.Equal("local", grupos["ollama"].GetProperty("tipo").GetString());

        Assert.DoesNotContain(ClaveA, texto, StringComparison.Ordinal);
        Assert.DoesNotContain("ia.example.net", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("http", texto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task With_no_stored_selection_the_origin_is_the_server_configuration()
    {
        await SembrarAsync(Ct);
        using var host = CrearHost(new Cable());
        using var cliente = Admin(host);

        var cuerpo = await CuerpoAsync(await cliente.GetAsync($"{Ruta}/modelo", Ct));

        Assert.Equal("servidor", cuerpo.GetProperty("enUso").GetProperty("origen").GetString());
        Assert.Empty(cuerpo.GetProperty("servidoresLocales").EnumerateArray());
        Assert.True(cuerpo.GetProperty("cifradoDisponible").GetBoolean());
    }

    // -------------------------------------------------------------- 3.2 / 3.12 test

    [Fact]
    public async Task A_successful_test_returns_a_latency_with_one_call_and_persists_nothing()
    {
        await SembrarAsync(Ct);
        var cable = new Cable();
        using var host = CrearHost(cable);
        using var cliente = Admin(host);

        using var respuesta = await Probar(cliente, "openai", "gpt-5", ClaveA);
        var cuerpo = await CuerpoAsync(respuesta);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(cuerpo.GetProperty("exito").GetBoolean());
        Assert.True(cuerpo.GetProperty("latenciaMs").GetInt32() >= 0);

        var pedido = Assert.Single(cable.Pedidos);
        Assert.Equal("api.openai.com", pedido.Uri.Host);
        Assert.Contains(ClaveA, pedido.Autorizacion, StringComparison.Ordinal);
        await AssertNadaCambioAsync();
    }

    [Theory]
    [InlineData("openai", "gpt-5", 401, "invalid_api_key", "clave-invalida")]
    [InlineData("openai", "gpt-5", 403, "forbidden", "clave-invalida")]
    [InlineData("openai", "gpt-5", 402, "payment_required", "sin-credito")]
    [InlineData("openai", "gpt-5", 429, "insufficient_quota", "sin-credito")]
    [InlineData("openai", "gpt-5", 404, "model_not_found", "modelo-inexistente")]
    [InlineData("openai", "gpt-5", 429, "rate_limit_exceeded", "saturado")]
    [InlineData("openai", "gpt-5", 500, "server_error", "inalcanzable")]
    [InlineData("anthropic", "claude-sonnet-5", 401, "authentication_error", "clave-invalida")]
    [InlineData("anthropic", "claude-sonnet-5", 400, "credit balance is too low", "sin-credito")]
    [InlineData("anthropic", "claude-sonnet-5", 404, "not_found_error", "modelo-inexistente")]
    [InlineData("anthropic", "claude-sonnet-5", 529, "overloaded_error", "saturado")]
    public async Task Each_cloud_failure_maps_to_its_code_and_no_provider_text_reaches_the_response(
        string empresa, string modelo, int estado, string tipo, string codigoEsperado)
    {
        await SembrarAsync(Ct);
        var cable = new Cable { Respuesta = _ => Rechazo((HttpStatusCode)estado, tipo.Replace(' ', '_'), tipo) };
        using var host = CrearHost(cable);
        using var cliente = Admin(host);

        using var respuesta = await Probar(cliente, empresa, modelo, ClaveA);
        var texto = await respuesta.Content.ReadAsStringAsync(Ct);
        var cuerpo = JsonDocument.Parse(texto).RootElement;

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.False(cuerpo.GetProperty("exito").GetBoolean());
        Assert.Equal(codigoEsperado, cuerpo.GetProperty("codigo").GetString());
        Assert.DoesNotContain(ClaveA, texto, StringComparison.Ordinal);
        Assert.DoesNotContain("ECHOED-BY-PROVIDER", texto, StringComparison.Ordinal);
        await AssertNadaCambioAsync();
    }

    [Fact]
    public async Task A_transport_failure_is_unreachable()
    {
        await SembrarAsync(Ct);
        using var host = CrearHost(new Cable { CaerEnElTransporte = true });
        using var cliente = Admin(host);

        var cuerpo = await CuerpoAsync(await Probar(cliente, "mistral", "mistral-large-latest", ClaveA));

        Assert.Equal("inalcanzable", cuerpo.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task A_cloud_company_with_no_key_at_all_is_an_invalid_key_without_a_call()
    {
        await SembrarAsync(Ct);
        var cable = new Cable();
        using var host = CrearHost(cable);
        using var cliente = Admin(host);

        var cuerpo = await CuerpoAsync(await Probar(cliente, "openai", "gpt-5"));

        Assert.Equal("clave-invalida", cuerpo.GetProperty("codigo").GetString());
        Assert.Empty(cable.Pedidos);
    }

    [Theory]
    [InlineData(401, "clave-invalida")]
    [InlineData(403, "clave-invalida")]
    [InlineData(404, "modelo-inexistente")]
    [InlineData(429, "saturado")]
    [InlineData(503, "saturado")]
    public async Task Each_shared_service_failure_maps_to_its_code_without_exposing_the_server_credential(
        int estado, string codigoEsperado)
    {
        await SembrarAsync(Ct);
        var cable = new Cable { Respuesta = _ => Cable.Json("{\"error\":\"nope\"}", (HttpStatusCode)estado) };
        using var host = CrearHost(cable, conServicioCompartido: true);
        using var cliente = Admin(host);

        using var respuesta = await Probar(cliente, "ollama", "qwen3.5:9b");
        var texto = await respuesta.Content.ReadAsStringAsync(Ct);

        Assert.Equal(codigoEsperado, JsonDocument.Parse(texto).RootElement.GetProperty("codigo").GetString());
        Assert.DoesNotContain(ContrasenaDelServidor, texto, StringComparison.Ordinal);
        Assert.DoesNotContain("Basic", texto, StringComparison.Ordinal);

        // The server-side credential did travel, on the request to the service.
        Assert.StartsWith("Basic ", Assert.Single(cable.Pedidos).Autorizacion, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_busy_gate_is_reported_as_saturated_without_waiting_or_calling_the_service()
    {
        await SembrarAsync(Ct);
        var cable = new Cable();
        using var host = CrearHost(cable, conServicioCompartido: true, compuerta: 1);
        using var cliente = Admin(host);

        var compuerta = host.Services.GetRequiredService<CompuertaDelModelo>();
        Assert.True(await compuerta.EntrarAsync(false, TimeSpan.Zero, Ct));
        try
        {
            var cuerpo = await CuerpoAsync(await Probar(cliente, "ollama", "qwen3.5:9b"));

            Assert.Equal("saturado", cuerpo.GetProperty("codigo").GetString());
            Assert.Empty(cable.Pedidos);
        }
        finally
        {
            compuerta.Salir();
        }
    }

    // ------------------------------------------------------------ 3.3 / 3.7 / 3.10 PUT

    [Fact]
    public async Task Applying_re_tests_saves_the_key_sets_the_selection_clears_the_cache_and_audits_hints_only()
    {
        await SembrarAsync(Ct);
        var cable = new Cable();
        using var host = CrearHost(cable);
        using var cliente = Admin(host);

        var cache = host.Services.GetRequiredService<CacheDeConsultasGeneradas>();
        cache.Guardar(
            "who teaches databases", false, new DateOnly(2027, 1, 1),
            GeneracionDeSql.NoContestable("x"), TimeSpan.FromHours(1));
        Assert.Equal(1, cache.Cantidad);

        using var respuesta = await Aplicar(cliente, "openai", "gpt-5", ClaveA);
        var texto = await respuesta.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("WXYZ", JsonDocument.Parse(texto).RootElement.GetProperty("pista").GetString());
        Assert.DoesNotContain(ClaveA, texto, StringComparison.Ordinal);

        Assert.Equal(("openai", "gpt-5"), await SeleccionGuardadaAsync());
        Assert.Equal(ClaveA, await ClaveDescifradaAsync(host, "openai"));
        Assert.Equal(0, cache.Cantidad);

        var filas = await AuditoriaAsync();
        Assert.Equal(["credencial.crear", "modelo.seleccionar"], filas.Select(f => f.Accion).Order());
        Assert.All(filas, f =>
        {
            Assert.DoesNotContain(ClaveA, f.Antes ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain(ClaveA, f.Despues, StringComparison.Ordinal);
            Assert.DoesNotContain("ABCDEFGH", f.Despues, StringComparison.Ordinal);
        });

        var seleccion = Assert.Single(filas, f => f.Accion == "modelo.seleccionar");
        Assert.Equal("{\"origen\":\"servidor\"}", seleccion.Antes);
        Assert.Equal("{\"empresa\":\"openai\",\"modelo\":\"gpt-5\",\"pista\":\"WXYZ\"}", seleccion.Despues);
    }

    [Fact]
    public async Task Applying_again_with_the_stored_key_needs_no_new_key_and_records_the_previous_selection()
    {
        await SembrarAsync(Ct);
        var cable = new Cable();
        using var host = CrearHost(cable);
        using var cliente = Admin(host);
        Assert.Equal(HttpStatusCode.OK, (await Aplicar(cliente, "openai", "gpt-5", ClaveA)).StatusCode);

        using var respuesta = await Aplicar(cliente, "openai", "gpt-5-mini");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(("openai", "gpt-5-mini"), await SeleccionGuardadaAsync());
        Assert.Contains(ClaveA, cable.Pedidos[^1].Autorizacion, StringComparison.Ordinal);

        var ultima = (await AuditoriaAsync()).Last(f => f.Accion == "modelo.seleccionar");
        Assert.Equal("{\"empresa\":\"openai\",\"modelo\":\"gpt-5\"}", ultima.Antes);
    }

    [Fact]
    public async Task A_failing_re_test_does_not_change_the_selection_or_the_keys_and_returns_the_code()
    {
        await SembrarAsync(Ct);
        var cable = new Cable();
        using var host = CrearHost(cable);
        using var cliente = Admin(host);
        Assert.Equal(HttpStatusCode.OK, (await Aplicar(cliente, "openai", "gpt-5", ClaveA)).StatusCode);
        var auditadas = (await AuditoriaAsync()).Count;

        cable.Respuesta = _ => Rechazo(HttpStatusCode.Unauthorized, "invalid_api_key");
        using var respuesta = await Aplicar(cliente, "anthropic", "claude-sonnet-5", ClaveB);
        var texto = await respuesta.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        Assert.Equal("clave-invalida", JsonDocument.Parse(texto).RootElement.GetProperty("codigo").GetString());
        Assert.DoesNotContain(ClaveB, texto, StringComparison.Ordinal);

        Assert.Equal(("openai", "gpt-5"), await SeleccionGuardadaAsync());
        Assert.Null(await PistaGuardadaAsync("anthropic"));
        Assert.Equal(ClaveA, await ClaveDescifradaAsync(host, "openai"));
        Assert.Equal(auditadas, (await AuditoriaAsync()).Count);
    }

    [Fact]
    public async Task A_model_outside_the_catalog_is_rejected_and_the_selection_is_unchanged()
    {
        await SembrarAsync(Ct);
        var cable = new Cable();
        using var host = CrearHost(cable);
        using var cliente = Admin(host);

        using var respuesta = await Aplicar(cliente, "openai", "gpt-9-ultra", ClaveA);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("modelo-fuera-del-catalogo", (await CuerpoAsync(respuesta)).GetProperty("codigo").GetString());
        Assert.Empty(cable.Pedidos);
        await AssertNadaCambioAsync();
    }

    // ----------------------------------------------------------------- 3.4 DELETE

    [Fact]
    public async Task Reverting_returns_to_server_configuration_clears_the_cache_and_audits_once()
    {
        await SembrarAsync(Ct);
        using var host = CrearHost(new Cable());
        using var cliente = Admin(host);
        Assert.Equal(HttpStatusCode.OK, (await Aplicar(cliente, "openai", "gpt-5", ClaveA)).StatusCode);

        var cache = host.Services.GetRequiredService<CacheDeConsultasGeneradas>();
        cache.Guardar("q", false, new DateOnly(2027, 1, 1), GeneracionDeSql.NoContestable("x"), TimeSpan.FromHours(1));

        using var primera = await cliente.DeleteAsync($"{Ruta}/modelo", Ct);
        using var segunda = await cliente.DeleteAsync($"{Ruta}/modelo", Ct);

        Assert.Equal(HttpStatusCode.NoContent, primera.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, segunda.StatusCode);
        Assert.Null(await SeleccionGuardadaAsync());
        Assert.Equal(0, cache.Cantidad);

        var filas = await AuditoriaAsync();
        var revertida = Assert.Single(filas, f => f.Accion == "modelo.restablecer");
        Assert.Equal("{\"empresa\":\"openai\",\"modelo\":\"gpt-5\"}", revertida.Antes);

        // The stored key survives a revert: it belongs to the company, not to the selection.
        Assert.Equal(ClaveA, await ClaveDescifradaAsync(host, "openai"));
    }

    // ---------------------------------------------------------- 3.5 / 3.9 replacement

    [Fact]
    public async Task A_failed_replacement_keeps_the_old_key_decryptable_and_in_use()
    {
        await SembrarAsync(Ct);
        var cable = new Cable();
        using var host = CrearHost(cable);
        using var cliente = Admin(host);
        Assert.Equal(HttpStatusCode.OK, (await Aplicar(cliente, "openai", "gpt-5", ClaveA)).StatusCode);
        var auditadas = (await AuditoriaAsync()).Count;

        cable.Respuesta = uri => Rechazo(HttpStatusCode.Unauthorized, "invalid_api_key");
        using var respuesta = await cliente.PostAsJsonAsync(
            $"{Ruta}/credenciales/openai/reemplazo", new PedidoDeReemplazoDeClaveDto(ClaveB), Ct);
        var texto = await respuesta.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        Assert.Equal("clave-invalida", JsonDocument.Parse(texto).RootElement.GetProperty("codigo").GetString());
        Assert.DoesNotContain(ClaveB, texto, StringComparison.Ordinal);

        Assert.Equal("WXYZ", await PistaGuardadaAsync("openai"));
        Assert.Equal(ClaveA, await ClaveDescifradaAsync(host, "openai"));
        Assert.Equal(auditadas, (await AuditoriaAsync()).Count);

        // And a later test with no new key still goes out with the OLD one.
        cable.Respuesta = null;
        Assert.True((await CuerpoAsync(await Probar(cliente, "openai", "gpt-5"))).GetProperty("exito").GetBoolean());
        Assert.Contains(ClaveA, cable.Pedidos[^1].Autorizacion, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_successful_replacement_updates_the_hint_audits_hints_only_and_leaves_the_selection_alone()
    {
        await SembrarAsync(Ct);
        var cable = new Cable();
        using var host = CrearHost(cable);
        using var cliente = Admin(host);
        Assert.Equal(HttpStatusCode.OK, (await Aplicar(cliente, "openai", "gpt-5", ClaveA)).StatusCode);

        // Replacing the key of a company that is NOT selected is independent too.
        using var respuesta = await cliente.PostAsJsonAsync(
            $"{Ruta}/credenciales/openai/reemplazo", new PedidoDeReemplazoDeClaveDto(ClaveB), Ct);
        var texto = await respuesta.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("QRST", JsonDocument.Parse(texto).RootElement.GetProperty("pista").GetString());
        Assert.DoesNotContain(ClaveB, texto, StringComparison.Ordinal);

        Assert.Equal("QRST", await PistaGuardadaAsync("openai"));
        Assert.Equal(ClaveB, await ClaveDescifradaAsync(host, "openai"));
        Assert.Equal(("openai", "gpt-5"), await SeleccionGuardadaAsync());
        Assert.Contains(ClaveB, cable.Pedidos[^1].Autorizacion, StringComparison.Ordinal);

        var fila = Assert.Single((await AuditoriaAsync()), f => f.Accion == "credencial.reemplazar");
        Assert.Equal("{\"empresa\":\"openai\",\"pista\":\"WXYZ\"}", fila.Antes);
        Assert.Equal("{\"empresa\":\"openai\",\"pista\":\"QRST\"}", fila.Despues);
    }

    [Theory]
    [InlineData("ollama")]
    [InlineData("local")]
    [InlineData("https://evil.example.net")]
    public async Task Only_cloud_companies_have_a_replaceable_key(string empresa)
    {
        await SembrarAsync(Ct);
        var cable = new Cable();
        using var host = CrearHost(cable, conServicioCompartido: true, conServidorPropio: true);
        using var cliente = Admin(host);

        using var respuesta = await cliente.PostAsJsonAsync(
            $"{Ruta}/credenciales/{Uri.EscapeDataString(empresa)}/reemplazo", new PedidoDeReemplazoDeClaveDto(ClaveA), Ct);

        Assert.True(respuesta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound);
        Assert.Empty(cable.Pedidos);
        await AssertNadaCambioAsync();
    }

    // ------------------------------------------------------------ 3.6 / 3.11 local

    [Theory]
    [InlineData("ollama", "qwen3.5:9b")]
    [InlineData("local", "qwen3-8b")]
    public async Task An_unconfigured_local_server_is_rejected_without_any_outbound_call(string servidor, string modelo)
    {
        await SembrarAsync(Ct);
        var cable = new Cable();
        using var host = CrearHost(cable);
        using var cliente = Admin(host);

        using var prueba = await Probar(cliente, servidor, modelo);
        using var aplicar = await Aplicar(cliente, servidor, modelo);

        foreach (var respuesta in new[] { prueba, aplicar })
        {
            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
            Assert.Equal("servidor-local-no-configurado", (await CuerpoAsync(respuesta)).GetProperty("codigo").GetString());
        }

        Assert.Empty(cable.Pedidos);
        await AssertNadaCambioAsync();
    }

    [Theory]
    [InlineData("https://evil.example.net")]
    [InlineData("http://169.254.169.254/latest")]
    [InlineData("evil.example.net:8000")]
    public async Task A_free_form_address_in_place_of_a_server_identifier_is_rejected(string direccion)
    {
        await SembrarAsync(Ct);
        var cable = new Cable();
        using var host = CrearHost(cable, conServicioCompartido: true, conServidorPropio: true);
        using var cliente = Admin(host);

        using var respuesta = await Aplicar(cliente, direccion, "qwen3.5:9b");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Empty(cable.Pedidos);
        await AssertNadaCambioAsync();
    }

    [Fact]
    public async Task A_request_that_carries_a_url_or_a_credential_field_for_a_local_server_is_rejected()
    {
        await SembrarAsync(Ct);
        var cable = new Cable();
        using var host = CrearHost(cable, conServicioCompartido: true);
        using var cliente = Admin(host);

        using var conUrl = await cliente.PutAsync(
            $"{Ruta}/modelo",
            new StringContent(
                """{"empresa":"ollama","modelo":"qwen3.5:9b","url":"https://evil.example.net"}""",
                Encoding.UTF8, "application/json"),
            Ct);
        using var conClave = await Aplicar(cliente, "ollama", "qwen3.5:9b", ClaveA);

        Assert.Equal(HttpStatusCode.BadRequest, conUrl.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, conClave.StatusCode);
        Assert.Equal("credencial-local-no-admitida", (await CuerpoAsync(conClave)).GetProperty("codigo").GetString());
        Assert.Empty(cable.Pedidos);
        await AssertNadaCambioAsync();
    }

    [Theory]
    [InlineData("ollama", "qwen3.5:9b", "ia.example.net")]
    [InlineData("local", "qwen3-8b", "arsdocendi-llm")]
    public async Task A_configured_local_server_can_be_selected_with_no_key_and_audits_no_hint(
        string servidor, string modelo, string host)
    {
        await SembrarAsync(Ct);
        var cable = new Cable();
        using var anfitrion = CrearHost(cable, conServicioCompartido: true, conServidorPropio: true);
        using var cliente = Admin(anfitrion);

        using var respuesta = await Aplicar(cliente, servidor, modelo);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal((servidor, modelo), await SeleccionGuardadaAsync());
        Assert.Equal(host, Assert.Single(cable.Pedidos).Uri.Host);
        Assert.Equal(0L, await EscalarAsync<long>("SELECT count(*) FROM asistente.credencial_de_proveedor"));

        var fila = Assert.Single(await AuditoriaAsync());
        Assert.Equal("modelo.seleccionar", fila.Accion);
        Assert.DoesNotContain("pista\":\"", fila.Despues, StringComparison.Ordinal);
        Assert.DoesNotContain(ContrasenaDelServidor, fila.Despues, StringComparison.Ordinal);
    }

    // ----------------------------------------------------------------------- 3.14

    [Fact]
    public async Task Saving_a_key_with_no_master_key_answers_503_without_a_call_and_leaves_the_local_servers_usable()
    {
        await SembrarAsync(Ct);
        var cable = new Cable();
        using var host = CrearHost(cable, conServicioCompartido: true, conClaveMaestra: false);
        using var cliente = Admin(host);

        using var guardar = await Aplicar(cliente, "openai", "gpt-5", ClaveA);
        using var reemplazar = await cliente.PostAsJsonAsync(
            $"{Ruta}/credenciales/openai/reemplazo", new PedidoDeReemplazoDeClaveDto(ClaveA), Ct);

        foreach (var respuesta in new[] { guardar, reemplazar })
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
            var texto = await respuesta.Content.ReadAsStringAsync(Ct);
            Assert.Equal("clave-maestra-no-disponible", JsonDocument.Parse(texto).RootElement.GetProperty("codigo").GetString());
            Assert.DoesNotContain(ClaveA, texto, StringComparison.Ordinal);
        }

        Assert.Empty(cable.Pedidos);
        await AssertNadaCambioAsync();

        var estado = await CuerpoAsync(await cliente.GetAsync($"{Ruta}/modelo", Ct));
        Assert.False(estado.GetProperty("cifradoDisponible").GetBoolean());

        // A test that persists nothing still works with a candidate key, and the shared service stays selectable.
        Assert.True((await CuerpoAsync(await Probar(cliente, "openai", "gpt-5", ClaveA))).GetProperty("exito").GetBoolean());
        using var local = await Aplicar(cliente, "ollama", "qwen3.5:9b");
        Assert.Equal(HttpStatusCode.OK, local.StatusCode);
        Assert.Equal(("ollama", "qwen3.5:9b"), await SeleccionGuardadaAsync());
    }

    [Fact]
    public async Task A_key_shorter_than_the_minimum_is_rejected_before_any_call()
    {
        await SembrarAsync(Ct);
        var cable = new Cable();
        using var host = CrearHost(cable);
        using var cliente = Admin(host);

        using var respuesta = await Aplicar(cliente, "openai", "gpt-5", "short");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("clave-demasiado-corta", (await CuerpoAsync(respuesta)).GetProperty("codigo").GetString());
        Assert.Empty(cable.Pedidos);
        await AssertNadaCambioAsync();
    }

    [Fact]
    public async Task No_log_line_carries_a_key_a_provider_echo_or_a_server_credential()
    {
        await SembrarAsync(Ct);
        var registro = new Registro();
        var cable = new Cable { Respuesta = _ => Rechazo(HttpStatusCode.Unauthorized, "invalid_api_key") };
        using var host = CrearHost(cable, conServicioCompartido: true, registro: registro);
        using var cliente = Admin(host);

        // A rejected cloud key (the provider echoes it), a rejected server credential, and a
        // success that stores a key: the three paths that touch secrets.
        _ = await Probar(cliente, "openai", "gpt-5", ClaveA);
        _ = await Aplicar(cliente, "openai", "gpt-5", ClaveA);
        _ = await Probar(cliente, "ollama", "qwen3.5:9b");
        cable.Respuesta = null;
        _ = await Aplicar(cliente, "openai", "gpt-5", ClaveB);
        _ = await cliente.PostAsJsonAsync(
            $"{Ruta}/credenciales/openai/reemplazo", new PedidoDeReemplazoDeClaveDto(ClaveA), Ct);

        Assert.NotEmpty(registro.Lineas);
        foreach (var linea in registro.Lineas)
        {
            Assert.DoesNotContain(ClaveA, linea, StringComparison.Ordinal);
            Assert.DoesNotContain(ClaveB, linea, StringComparison.Ordinal);
            Assert.DoesNotContain("ECHOED-BY-PROVIDER", linea, StringComparison.Ordinal);
            Assert.DoesNotContain(ContrasenaDelServidor, linea, StringComparison.Ordinal);
        }
    }
}
