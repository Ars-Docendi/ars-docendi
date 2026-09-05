using System.Net;
using ArsDocendi.Shared;
using ArsDocendi.Shared.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Asistente;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Sin configurar, el mecanismo de cassettes no existe.
/// </summary>
/// <remarks>
/// El apagado no es un <c>if</c> adentro del handler: es que el handler <b>no se
/// registra</b>. Con el directorio vacío el pipeline del cliente del proveedor
/// queda exactamente como estaba antes de que este mecanismo existiera, así que
/// producción no paga nada y no hay nada que se pueda misconfigurar — la única
/// forma de encenderlo es escribir una ruta.
/// </remarks>
public sealed class ApagadoPorDefaultTests
{
    private const string CadenaDelDueno =
        "Host=arsdocendi-postgres;Port=5432;Database=arsdocendi_pr_123;Username=app_pr_123;Password=x";

    /// <summary>
    /// Puerto 1: no hay servicio escuchando ahí, y con <c>Timeout=1</c> un intento
    /// de conexión falla rápido en vez de colgar el test.
    /// </summary>
    private const string CadenaInalcanzable =
        "Host=127.0.0.1;Port=1;Database=no_existe;Username=nadie;Password=nada;Timeout=1";

    [Fact]
    public void Sin_configuracion_el_pipeline_del_proveedor_es_el_de_antes_del_cambio()
    {
        using var raiz = Componer(directorio: null).BuildServiceProvider();

        // El reintento solo, como el día antes de este change. Un handler de más
        // acá sería trabajo, memoria y una superficie de fallo que ningún ambiente
        // pidió.
        Assert.Equal(
            [typeof(ReintentoDeTransporte)],
            OrdenDelGrabadorTests.HandlersDelCliente(raiz));
    }

    [Fact]
    public void Un_directorio_en_blanco_tampoco_enciende_nada()
    {
        // «   » es lo que deja una variable de ambiente exportada vacía por error.
        using var raiz = Componer(directorio: "   ").BuildServiceProvider();

        Assert.Equal(
            [typeof(ReintentoDeTransporte)],
            OrdenDelGrabadorTests.HandlersDelCliente(raiz));
    }

    [Fact]
    public async Task El_ping_responde_con_el_mecanismo_apagado()
    {
        var ct = TestContext.Current.CancellationToken;

        using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:ArsDocendi", CadenaInalcanzable);
        });
        using var cliente = host.CreateClient();

        // El invariante #3 no se negocia por una opción nueva: el ping tiene que
        // distinguir «el módulo está cargado» de «la base responde», y una
        // registración de más en el arranque es justamente lo que ya lo rompió una
        // vez.
        using var respuesta = await cliente.GetAsync("/api/asistente/ping", ct);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    private static ServiceCollection Componer(string? directorio)
    {
        var valores = new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{CadenaDuena.Clave}"] = CadenaDelDueno,
        };

        if (directorio is not null)
        {
            valores[
                $"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.DirectorioDeCassettes)}"] =
                directorio;
        }

        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddArsDocendiShared(configuracion);
        servicios.AddAsistenteModule(configuracion);
        return servicios;
    }
}
