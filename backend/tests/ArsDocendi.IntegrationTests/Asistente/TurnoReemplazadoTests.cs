using System.Net.Http.Json;
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
/// Replacing a turn by editing and resending it is recorded as implicit
/// dissatisfaction (asistente-metricas-de-calidad, design D4, task 2.8).
/// </summary>
/// <remarks>
/// Goes through the real host and the real writer: the flag is set by an UPDATE from the
/// registration step, and what matters is which row it lands on.
/// </remarks>
public sealed class TurnoReemplazadoTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_turno_reemplazado")
{
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");

    private const string ContarDocentes =
        "SELECT count(*) AS cantidad FROM designaciones.designaciones";

    [Fact]
    public async Task Replacing_an_answered_turn_flags_that_turn_only_and_keeps_its_vote()
    {
        await SembrarAsync();
        using var host = CrearHost(new ProveedorGuionado(
            ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.",
            ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes."));
        using var cliente = Autenticado(host);

        var primero = await PreguntarAsync(cliente, "¿cuántos docentes hay?", "clave-1");
        var tokenViejo = primero.ClaveDeRetroalimentacion!.Value;
        await VotarAsync(cliente, tokenViejo, voto: false);

        var segundo = await PreguntarAsync(
            cliente, "¿cuántos adjuntos hay?", "clave-2", hilo: primero.Hilo, reemplaza: "clave-1");

        Assert.True(await ReemplazadoAsync(tokenViejo));
        Assert.False(await ReemplazadoAsync(segundo.ClaveDeRetroalimentacion!.Value));
        Assert.Equal(1L, await EscalarAsync<long>("SELECT count(*) FROM asistente.registro_analitico WHERE reemplazado"));

        // The vote on the replaced turn survives, untouched.
        Assert.Equal(1L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.retroalimentacion_turno WHERE analitico_id = @t AND NOT voto",
            ("t", tokenViejo)));
    }

    [Fact]
    public async Task A_replacement_that_fails_flags_nothing()
    {
        await SembrarAsync();
        var llamadas = 0;
        using var host = CrearHost(new ProveedorGuionado(
            ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.",
            ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.")
        {
            // The third call is the generation of the replacement.
            Antes = () =>
            {
                if (++llamadas == 3)
                {
                    throw new InvalidOperationException("se cayó el reemplazo");
                }
            },
        });
        using var cliente = Autenticado(host);

        var primero = await PreguntarAsync(cliente, "¿cuántos docentes hay?", "clave-1");

        try
        {
            using var respuesta = await EnviarAsync(
                cliente, "¿cuántos adjuntos hay?", "clave-2", primero.Hilo, "clave-1");
            Assert.False(respuesta.IsSuccessStatusCode);
        }
        catch (InvalidOperationException)
        {
            // The test server may surface the exception instead of a 500: same outcome.
        }

        Assert.Equal(0L, await EscalarAsync<long>("SELECT count(*) FROM asistente.registro_analitico WHERE reemplazado"));
        Assert.False(await ReemplazadoAsync(primero.ClaveDeRetroalimentacion!.Value));
    }

    [Fact]
    public async Task Replacing_a_turn_that_was_not_answered_flags_nothing_because_its_id_is_not_kept()
    {
        await SembrarAsync();
        using var host = CrearHost(new ProveedorGuionado(
            ProveedorGuionado.NoContestable(),
            ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes."));
        using var cliente = Autenticado(host);

        var primero = await PreguntarAsync(cliente, "¿cuál es la temperatura del aula 302?", "clave-1");
        Assert.Null(primero.ClaveDeRetroalimentacion);

        await PreguntarAsync(
            cliente, "¿cuántos docentes hay?", "clave-2", hilo: primero.Hilo, reemplaza: "clave-1");

        Assert.Equal(2L, await EscalarAsync<long>("SELECT count(*) FROM asistente.registro_analitico"));
        Assert.Equal(0L, await EscalarAsync<long>("SELECT count(*) FROM asistente.registro_analitico WHERE reemplazado"));
    }

    [Fact]
    public async Task A_new_turn_is_never_flagged()
    {
        await SembrarAsync();
        using var host = CrearHost(new ProveedorGuionado(
            ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes."));
        using var cliente = Autenticado(host);

        await PreguntarAsync(cliente, "¿cuántos docentes hay?", "clave-1");

        Assert.Equal(0L, await EscalarAsync<long>("SELECT count(*) FROM asistente.registro_analitico WHERE reemplazado"));
    }

    // ----------------------------------------------------------------- apoyo

    private Task<bool> ReemplazadoAsync(Guid analiticoId) =>
        EscalarAsync<bool>(
            "SELECT reemplazado FROM asistente.registro_analitico WHERE id = @id", ("id", analiticoId));

    private WebApplicationFactory<Program> CrearHost(ProveedorGuionado guionado) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting($"ConnectionStrings:{CadenaDuena.Clave}", Cadena);
            builder.UseSetting($"{AutenticacionDesarrolloOptions.Seccion}:Enabled", "true");
            builder.UseSetting(
                $"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.RolSoloLectura)}", RolSoloLectura);
            builder.UseSetting(
                $"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.RolSoloLecturaPii)}", RolSoloLecturaPii);
            builder.UseSetting(
                $"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.PasswordSoloLectura)}",
                PostgresFixture.PasswordDeRol);
            builder.UseSetting(
                $"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.PasswordSoloLecturaPii)}",
                PostgresFixture.PasswordDeRol);

            builder.ConfigureTestServices(servicios =>
                servicios.AddSingleton(new ProveedorBase(guionado)));
        });

    private static HttpClient Autenticado(WebApplicationFactory<Program> host)
    {
        var cliente = host.CreateClient();
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, Secretaria.ToString());
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, "secretaria");

        return cliente;
    }

    private static async Task<HttpResponseMessage> EnviarAsync(
        HttpClient cliente, string mensaje, string clave, Guid? hilo, string? reemplaza)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/asistente/consultas")
        {
            Content = JsonContent.Create(new ConsultaDelAsistente(mensaje, hilo, reemplaza)),
        };
        pedido.Headers.TryAddWithoutValidation(AsistenteController.CabeceraDeIdempotencia, clave);

        return await cliente.SendAsync(pedido, TestContext.Current.CancellationToken);
    }

    private static async Task<RespuestaDelAsistente> PreguntarAsync(
        HttpClient cliente, string mensaje, string clave, Guid? hilo = null, string? reemplaza = null)
    {
        using var respuesta = await EnviarAsync(cliente, mensaje, clave, hilo, reemplaza);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(respuesta.IsSuccessStatusCode, cuerpo);

        return (await respuesta.Content.ReadFromJsonAsync<RespuestaDelAsistente>(
            TestContext.Current.CancellationToken))!;
    }

    private static async Task VotarAsync(HttpClient cliente, Guid token, bool voto)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/asistente/retroalimentacion")
        {
            Content = JsonContent.Create(new PedidoDeRetroalimentacion(token, voto, null, null)),
        };

        using var respuesta = await cliente.SendAsync(pedido, TestContext.Current.CancellationToken);
        Assert.True(respuesta.IsSuccessStatusCode);
    }
}
