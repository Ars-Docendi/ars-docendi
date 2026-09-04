using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica el smoke test del módulo del asistente (invariante #3).
/// </summary>
/// <remarks>
/// Estos tests NO usan la colección de PostgreSQL a propósito. El Host se compone
/// con una cadena de conexión que apunta a un puerto donde no escucha nadie: si
/// el camino de ejecución del ping tocara la base, la respuesta sería un error y
/// no un 200. Es la forma de comprobar «no toca la base» sin poder demostrar una
/// negativa por inspección.
/// </remarks>
public sealed class PingAsistenteTests
{
    /// <summary>
    /// Puerto 1: no hay servicio escuchando ahí. `Timeout=1` hace que un intento
    /// de conexión falle rápido en vez de colgar el test.
    /// </summary>
    private const string CadenaInalcanzable =
        "Host=127.0.0.1;Port=1;Database=no_existe;Username=nadie;Password=nada;Timeout=1";

    [Fact]
    public async Task Ping_responde_200_sin_credenciales_y_sin_base()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = CrearHostSinBase();
        using var cliente = host.CreateClient();

        using var respuesta = await cliente.GetAsync("/api/asistente/ping", ct);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Ping_devuelve_el_modulo_y_su_estado()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = CrearHostSinBase();
        using var cliente = host.CreateClient();

        var cuerpo = await cliente.GetFromJsonAsync<RespuestaPing>("/api/asistente/ping", ct);

        Assert.NotNull(cuerpo);
        Assert.Equal("asistente", cuerpo.Module);
        Assert.Equal("ok", cuerpo.Status);
    }

    [Fact]
    public async Task La_cadena_de_conexion_del_Host_realmente_no_conecta()
    {
        var ct = TestContext.Current.CancellationToken;

        // Contraprueba. Sin esto, los dos tests de arriba pasarían aunque la cadena
        // fuese alcanzable, y no demostrarían nada sobre el ping.
        await using var conexion = new NpgsqlConnection(CadenaInalcanzable);

        await Assert.ThrowsAnyAsync<Exception>(() => conexion.OpenAsync(ct));
    }

    private static WebApplicationFactory<Program> CrearHostSinBase(
        Action<IServiceCollection>? servicios = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:ArsDocendi", CadenaInalcanzable);
            if (servicios is not null)
            {
                builder.ConfigureTestServices(servicios);
            }
        });

    [Fact]
    public async Task Ping_responde_aunque_haya_una_politica_global_que_exija_autenticacion()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = CrearHostSinBase(servicios =>
            servicios.AddAuthorization(opciones =>
                opciones.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build()));
        using var cliente = host.CreateClient();

        // Contraprueba: con esa política global, un ping SIN [AllowAnonymous] deja
        // de responder 200. Hoy los pings de los otros módulos no llevan el
        // atributo, así que sirven de control — y el contraste deja anotado que el
        // invariante #3 no se cumple del todo fuera de este módulo.
        using var sinAtributo = await cliente.GetAsync("/api/designaciones/ping", ct);
        Assert.NotEqual(HttpStatusCode.OK, sinAtributo.StatusCode);

        using var respuesta = await cliente.GetAsync("/api/asistente/ping", ct);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    private sealed record RespuestaPing(string Module, string Status);
}
