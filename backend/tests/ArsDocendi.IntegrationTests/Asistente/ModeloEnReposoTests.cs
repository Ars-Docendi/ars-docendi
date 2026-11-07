using System.Net;
using System.Text;
using System.Text.Json;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Asistente;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Detection of the idle model with <c>GET /api/ps</c> and the preload with <c>POST /api/generate</c>
/// against a fake Ollama wire (asistente-modelo-en-reposo, D1-D3, D7): no GPU, no network.
/// </summary>
public sealed class ModeloEnReposoTests
{
    private const string Modelo = "qwen3.5:9b";
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    // ------------------------------------------------------------ detection

    [Fact]
    public async Task Sin_el_modelo_en_api_ps_esta_en_reposo()
    {
        var cable = Cable.ConPs(Ps("otro-modelo:7b"));

        var lectura = await Carga(cable).ConsultarAsync(Modelo, Ct);

        Assert.Equal("en_reposo", lectura.Codigo);
        Assert.Equal(["GET /api/ps"], cable.Solicitudes);
    }

    [Fact]
    public async Task Con_el_modelo_listado_y_el_mismo_contexto_esta_listo()
    {
        var cable = Cable.ConPs(Ps(Modelo, contexto: 16384));

        var lectura = await Carga(cable, contexto: 16384).ConsultarAsync(Modelo, Ct);

        Assert.Equal("listo", lectura.Codigo);
    }

    [Fact]
    public async Task Con_otro_contexto_el_primer_turno_lo_recargaria_asi_que_esta_en_reposo()
    {
        var cable = Cable.ConPs(Ps(Modelo, contexto: 4096));

        var lectura = await Carga(cable, contexto: 16384).ConsultarAsync(Modelo, Ct);

        Assert.Equal("en_reposo", lectura.Codigo);
    }

    [Fact]
    public async Task Sin_context_length_en_la_respuesta_compara_solo_por_nombre()
    {
        var cable = Cable.ConPs(Ps(Modelo));

        var lectura = await Carga(cable, contexto: 16384).ConsultarAsync(Modelo, Ct);

        Assert.Equal("listo", lectura.Codigo);
    }

    [Fact]
    public async Task Un_nombre_sin_etiqueta_es_latest()
    {
        var cable = Cable.ConPs(Ps("llama3:latest"));

        var lectura = await Carga(cable).ConsultarAsync("llama3", Ct);

        Assert.Equal("listo", lectura.Codigo);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task Si_api_ps_no_responde_bien_no_se_sabe_y_el_dto_no_filtra_nada(HttpStatusCode codigo)
    {
        var cable = new Cable(_ => new HttpResponseMessage(codigo)
        {
            Content = new StringContent("secreto https://ia.example.net/api/ps"),
        });

        var lectura = await Carga(cable).ConsultarAsync(Modelo, Ct);

        Assert.Equal("desconocido", lectura.Codigo);
        var json = JsonSerializer.Serialize(Modules.Asistente.Api.DisponibilidadDelModeloDto.De(lectura));
        Assert.DoesNotContain("ia.example.net", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secreto", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Si_el_servicio_no_contesta_en_tres_segundos_no_se_sabe()
    {
        var reloj = new RelojFijo(new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero));
        var cable = new Cable(_ => throw new InvalidOperationException()) { Colgar = true };
        var carga = Carga(cable, reloj: reloj);

        var consulta = carga.ConsultarAsync(Modelo, Ct);
        await cable.LlegoUnPedido.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        reloj.Avanzar(TimeSpan.FromSeconds(3));

        Assert.Equal("desconocido", (await consulta).Codigo);
    }

    [Fact]
    public async Task Una_respuesta_que_no_es_json_tambien_es_desconocido()
    {
        var cable = new Cable(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>") });

        Assert.Equal("desconocido", (await Carga(cable).ConsultarAsync(Modelo, Ct)).Codigo);
    }

    [Fact]
    public async Task Sin_servicio_de_inferencia_configurado_no_sale_ningun_pedido()
    {
        var cable = Cable.ConPs(Ps(Modelo));

        var lectura = await Carga(cable, url: string.Empty).ConsultarAsync(Modelo, Ct);

        Assert.Equal("desconocido", lectura.Codigo);
        Assert.Empty(cable.Solicitudes);
    }

    [Fact]
    public async Task Las_credenciales_viajan_en_el_pedido_de_estado()
    {
        var cable = Cable.ConPs(Ps(Modelo));

        await Carga(cable).ConsultarAsync(Modelo, Ct);

        Assert.Equal("Basic", cable.Pedidos[0].Authorization?.Scheme);
    }

    // ------------------------------------------------- single-flight and cache

    [Fact]
    public async Task Veinte_consultas_con_la_cache_vencida_hacen_un_solo_api_ps()
    {
        var cable = Cable.ConPs(Ps(Modelo));
        cable.RetrasarPs = true;
        var carga = Carga(cable);

        var consultas = Enumerable.Range(0, 20).Select(_ => carga.ConsultarAsync(Modelo, Ct)).ToArray();
        await cable.LlegoUnPedido.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        cable.Liberar();
        var lecturas = await Task.WhenAll(consultas);

        Assert.All(lecturas, l => Assert.Equal("listo", l.Codigo));
        Assert.Single(cable.Solicitudes);
    }

    [Fact]
    public async Task Dentro_de_la_vida_de_la_cache_no_sale_ningun_pedido_y_pasada_sale_otro()
    {
        var reloj = new RelojFijo(new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero));
        var cable = Cable.ConPs(Ps(Modelo));
        var carga = Carga(cable, reloj: reloj);

        await carga.ConsultarAsync(Modelo, Ct);
        reloj.Avanzar(TimeSpan.FromSeconds(29));
        await carga.ConsultarAsync(Modelo, Ct);
        Assert.Single(cable.Solicitudes);

        reloj.Avanzar(TimeSpan.FromSeconds(2));
        await carga.ConsultarAsync(Modelo, Ct);
        Assert.Equal(2, cable.Solicitudes.Count);
    }

    [Fact]
    public async Task Una_llamada_exitosa_al_modelo_lo_marca_listo_sin_api_ps()
    {
        var cable = Cable.ConPs(Ps("otro"));
        var carga = Carga(cable);

        carga.MarcarListo(Modelo);
        var lectura = await carga.ConsultarAsync(Modelo, Ct);

        Assert.Equal("listo", lectura.Codigo);
        Assert.Empty(cable.Solicitudes);
    }

    // --------------------------------------------------------------- activation

    [Fact]
    public async Task Diez_activaciones_a_la_vez_hacen_una_sola_carga_y_todas_responden_activando()
    {
        var cable = Cable.ConPs(Ps("otro"), generate: Generate());
        cable.RetrasarGenerate = true;
        var carga = Carga(cable, contexto: 16384);

        var respuestas = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => carga.ActivarAsync(Modelo, Ct)));
        await cable.LlegoGenerate.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);

        Assert.All(respuestas, r => Assert.Equal("activando", r.Codigo));
        Assert.Single(cable.Solicitudes, s => s == "POST /api/generate");
        cable.LiberarGenerate();
    }

    [Fact]
    public async Task La_precarga_nombra_el_modelo_con_el_mismo_num_ctx_y_sin_prompt_ni_keep_alive()
    {
        var cable = Cable.ConPs(Ps("otro"), generate: Generate());
        var carga = Carga(cable, contexto: 16384);

        await carga.ActivarAsync(Modelo, Ct);
        await cable.LlegoGenerate.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);

        var cuerpo = JsonDocument.Parse(cable.CuerpoDeGenerate).RootElement;
        Assert.Equal(Modelo, cuerpo.GetProperty("model").GetString());
        Assert.Equal(16384, cuerpo.GetProperty("options").GetProperty("num_ctx").GetInt32());
        Assert.False(cuerpo.GetProperty("stream").GetBoolean());
        Assert.False(cuerpo.TryGetProperty("prompt", out _));
        Assert.False(cuerpo.TryGetProperty("keep_alive", out _));
    }

    [Fact]
    public async Task Sin_contexto_configurado_la_precarga_no_manda_num_ctx()
    {
        var cable = Cable.ConPs(Ps("otro"), generate: Generate());

        await Carga(cable, contexto: 0).ActivarAsync(Modelo, Ct);
        await cable.LlegoGenerate.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);

        Assert.False(JsonDocument.Parse(cable.CuerpoDeGenerate).RootElement.TryGetProperty("options", out _));
    }

    [Fact]
    public async Task Cuando_ya_esta_listo_o_no_se_sabe_no_se_precarga()
    {
        var listo = Cable.ConPs(Ps(Modelo));
        Assert.Equal("listo", (await Carga(listo).ActivarAsync(Modelo, Ct)).Codigo);
        Assert.DoesNotContain("POST /api/generate", listo.Solicitudes);

        var ciego = new Cable(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        Assert.Equal("desconocido", (await Carga(ciego).ActivarAsync(Modelo, Ct)).Codigo);
        Assert.DoesNotContain("POST /api/generate", ciego.Solicitudes);
    }

    [Fact]
    public async Task Al_terminar_la_carga_queda_listo_sin_marca_de_fallo()
    {
        var cable = Cable.ConPs(Ps("otro"), generate: Generate());
        var carga = Carga(cable);

        await carga.ActivarAsync(Modelo, Ct);
        await EsperarQueTermineAsync(carga);

        var lectura = await carga.ConsultarAsync(Modelo, Ct);
        Assert.Equal("listo", lectura.Codigo);
        Assert.False(lectura.UltimaActivacionFallida);
    }

    [Fact]
    public async Task Dentro_del_enfriamiento_un_nuevo_pedido_no_arranca_una_carga()
    {
        var reloj = new RelojFijo(new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero));
        var cable = Cable.ConPs(Ps("otro"), generate: Generate());
        var carga = Carga(cable, reloj: reloj, vida: 5);

        await carga.ActivarAsync(Modelo, Ct);
        await EsperarQueTermineAsync(carga);

        // La lectura (5 s) venció, el enfriamiento (30 s) no.
        reloj.Avanzar(TimeSpan.FromSeconds(10));
        var segunda = await carga.ActivarAsync(Modelo, Ct);

        Assert.Equal("en_reposo", segunda.Codigo);
        Assert.Single(cable.Solicitudes, s => s == "POST /api/generate");

        reloj.Avanzar(TimeSpan.FromSeconds(30));
        Assert.Equal("activando", (await carga.ActivarAsync(Modelo, Ct)).Codigo);
        await cable.LlegoGenerate.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Una_carga_rechazada_deja_en_reposo_con_la_marca_de_fallo_y_un_log_sin_datos(HttpStatusCode codigo)
    {
        var capturas = new RegistroDeCapturas();
        var cable = Cable.ConPs(Ps("otro"), generate: () => new HttpResponseMessage(codigo)
        {
            Content = new StringContent("detalle interno https://ia.example.net"),
        });
        var carga = Carga(cable, log: capturas.Logger<CargaDelModelo>());

        await carga.ActivarAsync(Modelo, Ct);
        await EsperarQueTermineAsync(carga);
        var lectura = await carga.ConsultarAsync(Modelo, Ct);

        Assert.Equal("en_reposo", lectura.Codigo);
        Assert.True(lectura.UltimaActivacionFallida);

        var registrado = capturas.Todo();
        Assert.Contains(Modelo, registrado, StringComparison.Ordinal);
        Assert.Contains("rechazado", registrado, StringComparison.Ordinal);
        Assert.DoesNotContain("ia.example.net", registrado, StringComparison.Ordinal);
        Assert.DoesNotContain("detalle interno", registrado, StringComparison.Ordinal);
        Assert.DoesNotContain("Basic", registrado, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Una_carga_que_pasa_del_tiempo_maximo_se_da_por_fallida()
    {
        var reloj = new RelojFijo(new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero));
        var capturas = new RegistroDeCapturas();
        var cable = Cable.ConPs(Ps("otro"), generate: Generate());
        cable.RetrasarGenerate = true;
        var carga = Carga(cable, reloj: reloj, log: capturas.Logger<CargaDelModelo>());

        await carga.ActivarAsync(Modelo, Ct);
        await cable.LlegoGenerate.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        reloj.Avanzar(TimeSpan.FromSeconds(120));
        await EsperarQueTermineAsync(carga);

        var lectura = await carga.ConsultarAsync(Modelo, Ct);
        Assert.Equal("en_reposo", lectura.Codigo);
        Assert.True(lectura.UltimaActivacionFallida);
        Assert.Contains("tiempo_agotado", capturas.Todo(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mientras_se_carga_las_consultas_dicen_activando_sin_ir_a_ollama()
    {
        var cable = Cable.ConPs(Ps("otro"), generate: Generate());
        cable.RetrasarGenerate = true;
        var carga = Carga(cable);

        await carga.ActivarAsync(Modelo, Ct);
        await cable.LlegoGenerate.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal("activando", (await carga.ConsultarAsync(Modelo, Ct)).Codigo);
        }

        Assert.Single(cable.Solicitudes, s => s == "GET /api/ps");
        cable.LiberarGenerate();
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Waits for the detached load to finish by polling the state, which is what a client does.</summary>
    private static async Task EsperarQueTermineAsync(CargaDelModelo carga)
    {
        for (var i = 0; i < 400; i++)
        {
            if ((await carga.ConsultarAsync(Modelo, Ct)).Codigo != "activando")
            {
                return;
            }

            await Task.Delay(25, Ct);
        }

        Assert.Fail("La carga no terminó.");
    }

    private static string Ps(string modelo, int? contexto = null) =>
        new System.Text.Json.Nodes.JsonObject
        {
            ["models"] = new System.Text.Json.Nodes.JsonArray(
                new System.Text.Json.Nodes.JsonObject
                {
                    ["name"] = modelo,
                    ["model"] = modelo,
                    ["size_vram"] = 123,
                    ["context_length"] = contexto,
                }),
        }.ToJsonString();

    private static Func<HttpResponseMessage> Generate() => () => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent("""{"model":"x","response":"","done":true,"load_duration":2500000000}"""),
    };

    private static CargaDelModelo Carga(
        Cable cable,
        int contexto = 0,
        TimeProvider? reloj = null,
        string url = "https://ia.example.net",
        int vida = 30,
        Microsoft.Extensions.Logging.ILogger<CargaDelModelo>? log = null) =>
        new CargaDelModelo(
            new FabricaFalsa(cable),
            Options.Create(new OpcionesAsistente
            {
                ContextoDelModelo = contexto,
                TiempoDeVidaDisponibilidadSegundos = vida,
            }),
            Options.Create(new OpcionesInferencia { Url = url, Usuario = "usuario", Contrasena = "clave" }),
            reloj ?? TimeProvider.System,
            log ?? NullLogger<CargaDelModelo>.Instance);

    private sealed class FabricaFalsa(HttpMessageHandler manejador) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(manejador, disposeHandler: false)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    /// <summary>A fake Ollama: records what arrives and answers <c>/api/ps</c> and <c>/api/generate</c>.</summary>
    private sealed class Cable(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        private readonly List<string> _solicitudes = [];
        private readonly List<HttpRequestMessage> _pedidos = [];
        private readonly Lock _candado = new();
        private readonly TaskCompletionSource _liberar = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _liberarGenerate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Func<HttpResponseMessage>? Generate { get; set; }

        public bool Colgar { get; init; }

        public bool RetrasarPs { get; set; }

        public bool RetrasarGenerate { get; set; }

        public string PsRespuesta { get; set; } = string.Empty;

        public string CuerpoDeGenerate { get; private set; } = string.Empty;

        public TaskCompletionSource LlegoUnPedido { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource LlegoGenerate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<string> Solicitudes
        {
            get
            {
                lock (_candado)
                {
                    return [.. _solicitudes];
                }
            }
        }

        public IReadOnlyList<(System.Net.Http.Headers.AuthenticationHeaderValue? Authorization, string Ruta)> Pedidos
        {
            get
            {
                lock (_candado)
                {
                    return [.. _pedidos.Select(p => (p.Headers.Authorization, p.RequestUri!.AbsolutePath))];
                }
            }
        }

        public static Cable ConPs(string ps, Func<HttpResponseMessage>? generate = null) =>
            new(_ => new HttpResponseMessage(HttpStatusCode.OK)) { PsRespuesta = ps, Generate = generate };

        public void Liberar() => _liberar.TrySetResult();

        public void LiberarGenerate() => _liberarGenerate.TrySetResult();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken ct)
        {
            var ruta = $"{pedido.Method} {pedido.RequestUri!.AbsolutePath}";
            string cuerpo = pedido.Content is null ? string.Empty : await pedido.Content.ReadAsStringAsync(ct);

            lock (_candado)
            {
                _solicitudes.Add(ruta);
                _pedidos.Add(pedido);
            }

            if (ruta == "POST /api/generate")
            {
                CuerpoDeGenerate = cuerpo;
                LlegoGenerate.TrySetResult();
                if (RetrasarGenerate)
                {
                    await _liberarGenerate.Task.WaitAsync(ct);
                }

                return Generate?.Invoke() ?? new HttpResponseMessage(HttpStatusCode.OK);
            }

            LlegoUnPedido.TrySetResult();
            if (Colgar)
            {
                await Task.Delay(Timeout.Infinite, ct);
            }

            if (RetrasarPs)
            {
                await _liberar.Task.WaitAsync(ct);
            }

            return PsRespuesta.Length == 0
                ? responder(pedido)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(PsRespuesta, Encoding.UTF8, "application/json"),
                };
        }
    }
}
