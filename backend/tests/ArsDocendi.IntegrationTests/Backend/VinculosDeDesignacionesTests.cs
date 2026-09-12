using System.Security.Claims;
using ArsDocendi.Host.Asistente;
using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Modules.Asistente.Application;
using Modules.Designaciones.Contracts.Queries;

namespace ArsDocendi.IntegrationTests.Backend;

/// <summary>
/// El adaptador que compone el puerto de vínculos del asistente con designaciones.
/// </summary>
/// <remarks>
/// VIVE EN EL HOST Y NO EN EL MÓDULO, y ése es el punto: el asistente no referencia
/// a designaciones. La arista <c>Modules.Asistente → Modules.&lt;X&gt;.Contracts</c>
/// es ARS-46 y necesita la aprobación del equipo; el manifiesto de aristas lo
/// verifica por su cuenta, y estos tests verifican la otra mitad — que la
/// composición efectivamente ocurra y que reproduzca las dos autorizaciones del
/// endpoint de detalle.
/// </remarks>
public sealed class VinculosDeDesignacionesTests
{
    /// <summary>Una cadena que no resuelve: acá no se toca la base.</summary>
    private const string CadenaInalcanzable =
        "Host=127.0.0.1;Port=1;Database=nada;Username=nadie;Password=nada;Timeout=1";

    [Fact]
    public void El_contenedor_del_Host_resuelve_el_adaptador_y_no_la_implementacion_vacia()
    {
        // El módulo del asistente registra `SinVinculos` porque no conoce a nadie, y
        // el Host lo reemplaza. Es un orden de registración, o sea algo que se
        // rompe moviendo una línea y sin que nada se queje: por eso hay un test.
        using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:ArsDocendi", CadenaInalcanzable);
        });

        using var alcance = host.Services.CreateScope();
        var resolutor = alcance.ServiceProvider.GetRequiredService<IResolutorDeVinculos>();

        Assert.IsType<VinculosDeDesignaciones>(resolutor);
    }

    [Fact]
    public async Task Con_el_permiso_devuelve_el_destino_que_ubico_designaciones()
    {
        var consultas = new ConsultasFalsas([new PedidoUbicableDto("2026-9005", Identificador)]);
        var adaptador = Adaptador(consultas, conPermiso: true);

        var destinos = await adaptador.ResolverAsync(
            ["2026-9005"], TestContext.Current.CancellationToken);

        var destino = Assert.Single(destinos);
        Assert.Equal("2026-9005", destino.Key);
        Assert.Equal(VinculosDeDesignaciones.TipoDelPedido, destino.Value.Tipo);
        Assert.Equal(Identificador.ToString(), destino.Value.Id);
    }

    [Fact]
    public async Task Sin_el_permiso_no_se_le_pregunta_nada_a_designaciones()
    {
        // ES LA MITAD QUE EL ÁMBITO NO CUBRE. El permiso lo evalúa el atributo del
        // controller, no el servicio del módulo, así que un adaptador que sólo
        // delegara dejaría pasar el caso en que la matriz cambió después de emitido
        // el token: la RLS del asistente la lee en vivo y el endpoint lee el claim.
        //
        // Las consultas revientan si alguien las llama: sin eso, «devolvió vacío»
        // también lo cumpliría una implementación que preguntó igual.
        var adaptador = Adaptador(new ConsultasQueRevientan(), conPermiso: false);

        var destinos = await adaptador.ResolverAsync(
            ["2026-9005"], TestContext.Current.CancellationToken);

        Assert.Empty(destinos);
    }

    [Fact]
    public async Task Sin_contexto_de_pedido_no_resuelve_nada()
    {
        // Un turno fuera de un request HTTP no tiene principal contra el que
        // autorizar. Falla cerrado: sin vínculos, no con los de nadie.
        var adaptador = new VinculosDeDesignaciones(
            new ConsultasQueRevientan(), Autorizacion(), new ContextoFalso(null));

        Assert.Empty(await adaptador.ResolverAsync(
            ["2026-9005"], TestContext.Current.CancellationToken));
    }

    // ------------------------------------------------------------------ apoyo

    private static readonly Guid Identificador =
        Guid.Parse("d5000000-0000-4000-8000-000000000005");

    private static VinculosDeDesignaciones Adaptador(
        IDesignacionesQueries consultas, bool conPermiso)
    {
        var identidad = conPermiso
            ? new ClaimsIdentity([new Claim(Permisos.Claim, Permisos.DesignacionesVer)], "test")
            : new ClaimsIdentity([], "test");

        return new VinculosDeDesignaciones(
            consultas,
            Autorizacion(),
            new ContextoFalso(new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identidad),
            }));
    }

    /// <summary>La MISMA política que registra el Host para ese permiso.</summary>
    private static IAuthorizationService Autorizacion()
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddAuthorization(opciones =>
            opciones.AddPolicy(
                Permisos.DesignacionesVer,
                politica => politica.RequireClaim(Permisos.Claim, Permisos.DesignacionesVer)));

        return servicios.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    private sealed class ContextoFalso(HttpContext? contexto) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = contexto;
    }

    private sealed class ConsultasFalsas(IReadOnlyList<PedidoUbicableDto> ubicables)
        : IDesignacionesQueries
    {
        public Task<IReadOnlyList<PedidoUbicableDto>> UbicarPedidosAsync(
            IReadOnlyCollection<string> textos, CancellationToken ct) =>
            Task.FromResult(ubicables);
    }

    private sealed class ConsultasQueRevientan : IDesignacionesQueries
    {
        public Task<IReadOnlyList<PedidoUbicableDto>> UbicarPedidosAsync(
            IReadOnlyCollection<string> textos, CancellationToken ct) =>
            throw new InvalidOperationException(
                "No se tiene que consultar designaciones sin haber pasado la política.");
    }
}
