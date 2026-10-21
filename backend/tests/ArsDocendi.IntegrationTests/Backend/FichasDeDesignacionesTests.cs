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
/// El adaptador que compone el puerto de fichas del asistente con designaciones.
/// </summary>
/// <remarks>
/// Misma lógica que <see cref="VinculosDeDesignacionesTests"/>: la composición tiene
/// que ocurrir, y el adaptador tiene que reproducir la política del endpoint de
/// detalle antes de preguntarle nada al módulo.
/// </remarks>
public sealed class FichasDeDesignacionesTests
{
    private const string CadenaInalcanzable =
        "Host=127.0.0.1;Port=1;Database=nada;Username=nadie;Password=nada;Timeout=1";

    private static readonly Guid Identificador =
        Guid.Parse("d5000000-0000-4000-8000-000000000005");

    private static readonly DateTimeOffset Cuando = new(2026, 9, 3, 14, 30, 0, TimeSpan.Zero);

    [Fact]
    public void El_contenedor_del_Host_resuelve_el_adaptador_y_no_la_implementacion_vacia()
    {
        // `SinFichas` es lo que registra el módulo; el Host lo reemplaza. Es un orden
        // de registro: se rompe moviendo una línea y sin que nada se queje.
        using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("AutenticacionMicrosoft:Habilitada", "true");
            builder.UseSetting("AutenticacionMicrosoft:ClientId", "cliente-pruebas");
            builder.UseSetting("AutenticacionMicrosoft:ClientSecret", "secreto-pruebas");
            builder.UseSetting("ConnectionStrings:ArsDocendi", CadenaInalcanzable);
        });

        using var alcance = host.Services.CreateScope();

        Assert.IsType<FichasDeDesignaciones>(
            alcance.ServiceProvider.GetRequiredService<IProveedorDeFichas>());
    }

    [Fact]
    public async Task Con_el_permiso_traduce_la_ficha_a_las_claves_documentadas()
    {
        var consultas = new Consultas(Ficha());
        var adaptador = Adaptador(consultas, conPermiso: true);

        var ficha = await adaptador.ObtenerAsync(
            VinculosDeDesignaciones.TipoDelPedido, Identificador.ToString(), TestContext.Current.CancellationToken);

        Assert.NotNull(ficha);
        Assert.Equal(VinculosDeDesignaciones.TipoDelPedido, ficha.Tipo);
        Assert.Equal(Identificador.ToString(), ficha.Id);
        Assert.Equal(Identificador, consultas.Pedido);
        Assert.Equal("2026-9005", ficha.Datos["numero"]);
        Assert.Equal("Cambio de cargo o dedicación", ficha.Datos["novedad"]);
        Assert.Equal("Ingeniería de Software", ficha.Datos["materia"]);
        Assert.Equal("en_revision_coordinador", ficha.Datos["estado"]);
        Assert.Equal("En Coordinación", ficha.Datos["estadoEtiqueta"]);
        Assert.Equal("2026 – 1.er cuatrimestre", ficha.Datos["periodo"]);
        Assert.Equal("Profesor Adjunto", ficha.Datos["cargo"]);
        Assert.Equal("Categoría 3", ficha.Datos["dedicacion"]);
        Assert.Equal("2026-09-03T14:30:00.0000000+00:00", ficha.Datos["ultimaActividad"]);
    }

    [Fact]
    public async Task Un_cargo_y_una_dedicacion_ausentes_no_dejan_claves_vacias()
    {
        var adaptador = Adaptador(new Consultas(Ficha(cargo: null, dedicacion: null)), conPermiso: true);

        var ficha = await adaptador.ObtenerAsync(
            VinculosDeDesignaciones.TipoDelPedido, Identificador.ToString(), TestContext.Current.CancellationToken);

        Assert.NotNull(ficha);
        Assert.DoesNotContain("cargo", ficha.Datos.Keys);
        Assert.DoesNotContain("dedicacion", ficha.Datos.Keys);
    }

    [Fact]
    public async Task Sin_el_permiso_no_se_le_pregunta_nada_a_designaciones()
    {
        // Las consultas revientan si alguien las llama: «devolvió null» también lo
        // cumpliría una implementación que preguntó igual.
        var adaptador = Adaptador(new ConsultasQueRevientan(), conPermiso: false);

        Assert.Null(await adaptador.ObtenerAsync(
            VinculosDeDesignaciones.TipoDelPedido, Identificador.ToString(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Un_tipo_ajeno_o_un_identificador_invalido_no_consultan_nada()
    {
        var adaptador = Adaptador(new ConsultasQueRevientan(), conPermiso: true);
        var ct = TestContext.Current.CancellationToken;

        Assert.Null(await adaptador.ObtenerAsync("perfil-docente", Identificador.ToString(), ct));
        Assert.Null(await adaptador.ObtenerAsync(VinculosDeDesignaciones.TipoDelPedido, "no-es-un-guid", ct));
    }

    [Fact]
    public async Task Si_designaciones_no_devuelve_ficha_no_hay_ficha()
    {
        var adaptador = Adaptador(new Consultas(null), conPermiso: true);

        Assert.Null(await adaptador.ObtenerAsync(
            VinculosDeDesignaciones.TipoDelPedido, Identificador.ToString(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Sin_contexto_de_pedido_no_hay_ficha()
    {
        var adaptador = new FichasDeDesignaciones(
            new ConsultasQueRevientan(), Autorizacion(), new ContextoFalso(null));

        Assert.Null(await adaptador.ObtenerAsync(
            VinculosDeDesignaciones.TipoDelPedido, Identificador.ToString(), TestContext.Current.CancellationToken));
    }

    // ------------------------------------------------------------------ apoyo

    private static FichaDePedidoDto Ficha(string? cargo = "Profesor Adjunto", string? dedicacion = "Categoría 3") =>
        new(
            Identificador, "2026-9005", "Cambio de cargo o dedicación", "Ingeniería de Software",
            "en_revision_coordinador", "En Coordinación", "2026 – 1.er cuatrimestre",
            cargo, dedicacion, Cuando);

    private static FichasDeDesignaciones Adaptador(IDesignacionesQueries consultas, bool conPermiso)
    {
        var identidad = conPermiso
            ? new ClaimsIdentity([new Claim(Permisos.Claim, Permisos.DesignacionesVer)], "test")
            : new ClaimsIdentity([], "test");

        return new FichasDeDesignaciones(
            consultas,
            Autorizacion(),
            new ContextoFalso(new DefaultHttpContext { User = new ClaimsPrincipal(identidad) }));
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

    private sealed class Consultas(FichaDePedidoDto? ficha) : IDesignacionesQueries
    {
        public Guid? Pedido { get; private set; }

        public Task<IReadOnlyList<PedidoUbicableDto>> UbicarPedidosAsync(
            IReadOnlyCollection<string> textos, CancellationToken ct) =>
            throw new InvalidOperationException("Este adaptador no ubica pedidos.");

        public Task<FichaDePedidoDto?> FichaDePedidoAsync(Guid id, CancellationToken ct)
        {
            Pedido = id;
            return Task.FromResult(ficha);
        }
    }

    private sealed class ConsultasQueRevientan : IDesignacionesQueries
    {
        public Task<IReadOnlyList<PedidoUbicableDto>> UbicarPedidosAsync(
            IReadOnlyCollection<string> textos, CancellationToken ct) =>
            throw new InvalidOperationException("No se tiene que consultar designaciones.");

        public Task<FichaDePedidoDto?> FichaDePedidoAsync(Guid id, CancellationToken ct) =>
            throw new InvalidOperationException(
                "No se tiene que consultar designaciones sin haber pasado la política.");
    }
}
