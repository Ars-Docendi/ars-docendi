using System.Net;
using System.Net.Http.Json;
using System.Text;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Asistente;
using Modules.Asistente.Api;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// The idle-model endpoints over real HTTP, authorization and the module's own identity resolution
/// (asistente-modelo-en-reposo, D3/D5/D8), with a fake Ollama wire.
/// </summary>
public sealed class ModeloEnReposoHttpTests(PostgresFixture postgres)
    : AsistenteHttpBase(postgres, "asistente_modelo_en_reposo", sembrada: true)
{
    private const string Disponibilidad = "/api/asistente/modelo/disponibilidad";
    private const string Activacion = "/api/asistente/modelo/activacion";

    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static readonly Guid Docente = Guid.Parse("a0000000-0000-4000-8000-000000000001");
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sin_asistente_consultar_ambos_endpoints_dan_403_y_no_sale_ningun_pedido()
    {
        var cable = new CableDeOllama();
        using var host = Host(cable, "ollama");
        using var cliente = host.CreateClient();
        Autenticar(cliente, Docente, "docente");

        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync(Disponibilidad, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PostAsync(Activacion, null, Ct)).StatusCode);
        Assert.Equal(0, cable.Pedidos);
    }

    [Fact]
    public async Task Un_ambiente_ollama_en_reposo_se_informa_y_activar_responde_202_sin_gastar_cupo()
    {
        var cable = new CableDeOllama();
        using var host = Host(cable, "ollama");
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");
        var antes = await EscalarAsync<long>("SELECT count(*) FROM asistente.registro_operativo");

        var estado = await LeerAsync<DisponibilidadDelModeloDto>(await cliente.GetAsync(Disponibilidad, Ct));
        Assert.Equal("en_reposo", estado.Estado);
        Assert.False(estado.UltimaActivacionFallida);

        var respuesta = await cliente.PostAsync(Activacion, null, Ct);
        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);
        Assert.Equal("activando", (await respuesta.Content.ReadFromJsonAsync<DisponibilidadDelModeloDto>(Ct))!.Estado);
        await cable.LlegoGenerate.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);

        // Outside the turn chain: no quota or consumption row is written.
        Assert.Equal(antes, await EscalarAsync<long>("SELECT count(*) FROM asistente.registro_operativo"));
    }

    [Theory]
    [InlineData("simulado")]
    [InlineData("local")]
    [InlineData("anthropic")]
    public async Task Otros_proveedores_son_no_aplica_y_no_salen_pedidos(string proveedor)
    {
        var cable = new CableDeOllama();
        using var host = Host(cable, proveedor);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var estado = await LeerAsync<DisponibilidadDelModeloDto>(await cliente.GetAsync(Disponibilidad, Ct));
        var activacion = await cliente.PostAsync(Activacion, null, Ct);

        Assert.Equal("no_aplica", estado.Estado);
        Assert.Equal(HttpStatusCode.Accepted, activacion.StatusCode);
        Assert.Equal("no_aplica", (await activacion.Content.ReadFromJsonAsync<DisponibilidadDelModeloDto>(Ct))!.Estado);
        Assert.Equal(0, cable.Pedidos);
    }

    [Fact]
    public async Task Una_seleccion_guardada_de_ollama_sobre_un_ambiente_en_la_nube_se_respeta()
    {
        var cable = new CableDeOllama();
        using var host = Host(cable, "anthropic", seleccion: new SeleccionDeModelo(
            "ollama", "qwen3.5:9b", Secretaria, DateTimeOffset.UtcNow));
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var estado = await LeerAsync<DisponibilidadDelModeloDto>(await cliente.GetAsync(Disponibilidad, Ct));

        Assert.Equal("en_reposo", estado.Estado);
        Assert.Equal(1, cable.Pedidos);
    }

    // ------------------------------------------------------------------ apoyo

    private WebApplicationFactory<Program> Host(
        CableDeOllama cable, string proveedor, SeleccionDeModelo? seleccion = null) =>
        CrearHostAsistente(configurar: builder =>
        {
            builder.UseSetting($"{OpcionesAsistente.Seccion}:Proveedor", proveedor);
            builder.UseSetting($"{OpcionesAsistente.Seccion}:Modelo", "qwen3.5:9b");
            builder.UseSetting($"{OpcionesAsistente.Seccion}:UrlDelProveedorLocal", "http://llm:8000/v1");
            builder.UseSetting($"{OpcionesAsistente.Seccion}:ClaveDelProveedor", "clave-de-prueba-123456");
            builder.ConfigureTestServices(servicios =>
            {
                servicios.AddSingleton(new CargaDelModelo(
                    new FabricaDeOllama(cable),
                    Options.Create(new OpcionesAsistente()),
                    Options.Create(new OpcionesInferencia
                    {
                        Url = "https://ia.example.net",
                        Usuario = "usuario",
                        Contrasena = "clave",
                    }),
                    TimeProvider.System,
                    NullLogger<CargaDelModelo>.Instance));

                if (seleccion is not null)
                {
                    servicios.AddScoped<ILectorDeLaSeleccion>(_ => new SeleccionFija(seleccion));
                }
            });
        });

    private sealed class SeleccionFija(SeleccionDeModelo seleccion) : ILectorDeLaSeleccion
    {
        public Task<SeleccionDeModelo?> LeerAsync(CancellationToken ct) => Task.FromResult<SeleccionDeModelo?>(seleccion);
    }

    private sealed class FabricaDeOllama(HttpMessageHandler manejador) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(manejador, disposeHandler: false);
    }

    private sealed class CableDeOllama : HttpMessageHandler
    {
        private int _pedidos;

        public int Pedidos => Volatile.Read(ref _pedidos);

        public TaskCompletionSource LlegoGenerate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken ct)
        {
            Interlocked.Increment(ref _pedidos);
            if (pedido.RequestUri!.AbsolutePath == "/api/generate")
            {
                LlegoGenerate.TrySetResult();
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"models":[]}""", Encoding.UTF8, "application/json"),
            });
        }
    }
}
