using System.Net;
using System.Net.Http.Json;
using System.Text;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Modules.Asistente;
using Modules.Asistente.Api;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// The cause and the model of a turn that goes through the REAL adapters, behind a
/// fake wire (asistente-metricas-de-calidad, tasks 2.6 and 2.9): the shared service's
/// 429 and 503, and a runtime model selection.
/// </summary>
public sealed class CausasDelTurnoConAdaptadoresTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_causas_adaptadores")
{
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static readonly Guid Admin = Guid.Parse("a0000000-0000-4000-8000-0000000000a1");
    private const string Inferencia = "https://ia.example.net";
    private const string ModeloDelEntorno = "qwen3.5:9b";

    /// <summary>A fake wire that answers every request with one status, or a canned success.</summary>
    private sealed class Cable(HttpStatusCode estado) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken ct) =>
            Task.FromResult(estado == HttpStatusCode.OK
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    // Valid for the OpenAI-compatible adapter, and not a generation object:
                    // the turn ends as an unintelligible generation.
                    Content = new StringContent(
                        """
                        {"id":"c","object":"chat.completion","created":1,"model":"m",
                         "choices":[{"index":0,"message":{"role":"assistant","content":"SELECT 1"},"finish_reason":"stop"}],
                         "usage":{"prompt_tokens":10,"completion_tokens":5,"total_tokens":15}}
                        """,
                        Encoding.UTF8,
                        "application/json"),
                }
                : new HttpResponseMessage(estado) { Content = new StringContent("{}") });
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task The_shared_service_429_and_503_are_recorded_as_saturated_with_the_breaker_closed(
        HttpStatusCode estado)
    {
        await SembrarAsync();
        using var host = CrearHost(new Cable(estado), ajustes: ConElServicioCompartido);
        using var cliente = Autenticado(host);

        await PreguntarAsync(cliente);

        var fila = await LeerFilaAsync();
        Assert.Equal("proveedor_saturado", fila.Causa);
        Assert.Equal("ServicioDegradado", fila.Estado);
        Assert.Equal($"ollama/{ModeloDelEntorno}", fila.Modelo);

        using var alcance = host.Services.CreateScope();
        var breakers = alcance.ServiceProvider.GetRequiredService<RegistroDeBreakers>();
        Assert.Equal(EstadoDelBreaker.Cerrado, breakers.Para($"ollama/{ModeloDelEntorno}").Estado);
    }

    [Fact]
    public async Task A_runtime_selection_is_recorded_as_the_model_of_the_turn_and_matches_the_operational_provider()
    {
        await SembrarAsync();
        using var host = CrearHost(new Cable(HttpStatusCode.OK), ajustes: ConElServicioCompartido);

        await SeleccionarAsync(host, "openai", "gpt-5", "sk-live-ABCDEFGH12345678WXYZ");
        using var cliente = Autenticado(host);

        await PreguntarAsync(cliente);

        var fila = await LeerFilaAsync();
        Assert.Equal("openai/gpt-5", fila.Modelo);
        Assert.Equal("respuesta_ininteligible", fila.Causa);
        // The operational provider is unchanged: the same string, written by the same adapter name.
        Assert.Equal(
            "openai/gpt-5",
            await EscalarAsync<string>("SELECT proveedor FROM asistente.registro_operativo"));
    }

    [Fact]
    public async Task Without_a_selection_the_model_is_the_one_the_environment_describes()
    {
        await SembrarAsync();
        using var host = CrearHost(new Cable(HttpStatusCode.OK), ajustes: ConElServicioCompartido);
        using var cliente = Autenticado(host);

        await PreguntarAsync(cliente);

        Assert.Equal($"ollama/{ModeloDelEntorno}", (await LeerFilaAsync()).Modelo);
    }

    // ----------------------------------------------------------------- apoyo

    private static void ConElServicioCompartido(Dictionary<string, string> v)
    {
        v[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.Proveedor)}"] = "ollama";
        v[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.Modelo)}"] = ModeloDelEntorno;
        v[$"{OpcionesInferencia.Seccion}:Url"] = Inferencia;
        v[$"{OpcionesInferencia.Seccion}:Usuario"] = "preview";
        v[$"{OpcionesInferencia.Seccion}:Contrasena"] = "clave";
        v[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.ContextoDelModelo)}"] = "16384";
        v[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.ClaveMaestra)}"] =
            CifradoDeCredencialesTests.ClaveMaestra(7);
        v[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.MaximoDeIntentosDeTransporte)}"] = "1";
        v[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.EsperaBaseMs)}"] = "1";
        v[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.EsperaMaximaMs)}"] = "2";
    }

    private WebApplicationFactory<Program> CrearHost(Cable cable, Action<Dictionary<string, string>> ajustes)
    {
        var valores = new Dictionary<string, string>
        {
            [$"ConnectionStrings:{CadenaDuena.Clave}"] = Cadena,
            [$"{AutenticacionDesarrolloOptions.Seccion}:Enabled"] = "true",
            [$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.RolSoloLectura)}"] = RolSoloLectura,
            [$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.RolSoloLecturaPii)}"] = RolSoloLecturaPii,
            [$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.PasswordSoloLectura)}"] =
                PostgresFixture.PasswordDeRol,
            [$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.PasswordSoloLecturaPii)}"] =
                PostgresFixture.PasswordDeRol,
        };
        ajustes(valores);

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");

            foreach (var (clave, valor) in valores)
            {
                builder.UseSetting(clave, valor);
            }

            builder.ConfigureTestServices(servicios =>
                servicios.AddHttpClient(ModuleExtensions.ClienteDelProveedor)
                    .ConfigurePrimaryHttpMessageHandler(() => cable));
        });
    }

    private static HttpClient Autenticado(WebApplicationFactory<Program> host)
    {
        var cliente = host.CreateClient();
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, Secretaria.ToString());
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, "secretaria");

        return cliente;
    }

    private static async Task SeleccionarAsync(
        WebApplicationFactory<Program> host, string empresa, string modelo, string clave)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var alcance = host.Services.CreateAsyncScope();

        await alcance.ServiceProvider.GetRequiredService<RepositorioDeCredenciales>()
            .GuardarAsync(empresa, clave, Admin, ct);
        Assert.True(await alcance.ServiceProvider.GetRequiredService<RepositorioDelModelo>()
            .AplicarSeleccionAsync(empresa, modelo, Admin, ct));
    }

    private static async Task PreguntarAsync(HttpClient cliente)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/asistente/consultas")
        {
            Content = JsonContent.Create(new ConsultaDelAsistente("¿cuántos docentes hay?", null)),
        };
        pedido.Headers.TryAddWithoutValidation(
            AsistenteController.CabeceraDeIdempotencia, Guid.NewGuid().ToString());

        using var respuesta = await cliente.SendAsync(pedido, TestContext.Current.CancellationToken);
        Assert.True(
            respuesta.IsSuccessStatusCode,
            await respuesta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private async Task<(string? Causa, string Estado, string? Modelo)> LeerFilaAsync()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT causa, estado, modelo FROM asistente.registro_analitico", conexion);
        await using var lector = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        Assert.True(await lector.ReadAsync(TestContext.Current.CancellationToken), "No se registró el turno.");

        return (
            lector.IsDBNull(0) ? null : lector.GetString(0),
            lector.GetString(1),
            lector.IsDBNull(2) ? null : lector.GetString(2));
    }
}
