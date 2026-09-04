using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Designaciones.Api;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure;
using Modules.Designaciones.Repositories;
using Modules.Designaciones.Services;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Designaciones;

[Collection(ColeccionPostgres.Nombre)]
public sealed class PedidosApiTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "pedidos_api")
{
    private static readonly Guid Periodo = Guid.Parse("d4000000-0000-4000-8000-000000000001");
    private static readonly Guid Materia = Guid.Parse("70000000-0000-4000-8000-000000000101");
    private static readonly Guid Jefe = Guid.Parse("a0000000-0000-4000-8000-000000000002");

    [Fact]
    public async Task Controller_crea_obtiene_edita_envia_reenvia_y_elimina()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var controller = new PedidosController(CrearServicio(Jefe, identityDb, db));

        var respuesta = await controller.Crear(Datos(
            Guid.Parse("d0000000-0000-4000-8000-000000000002")), ct);
        var creado = Assert.IsType<PedidoDto>(Assert.IsType<CreatedAtActionResult>(respuesta.Result).Value);
        Assert.Matches(@"^\d{4}-\d{4}$", creado.Numero);
        Assert.Equal("crear", Assert.Single(creado.Historial).Accion);
        Assert.Contains("enviar", creado.AccionesPermitidas);

        var editado = await controller.Editar(creado.Id, Datos(creado.Persona.Id) with
        {
            Horas = 18,
            Justificacion = "Actualización de carga",
            Version = creado.Version,
        }, ct);
        Assert.Equal(18, editado.Horas);
        Assert.Equal("editar", editado.Historial.Last().Accion);

        var enviado = await controller.Enviar(creado.Id, Guid.NewGuid().ToString(), ct);
        Assert.Equal(EstadosPedido.EnRevisionCoordinador, enviado.Estado);
        Assert.Equal("enviar", enviado.Historial.Last().Accion);
        Assert.NotNull(enviado.Snapshot);

        var devueltoId = Guid.Parse("d5000000-0000-4000-8000-000000000005");
        var reenviado = await controller.Reenviar(devueltoId, Guid.NewGuid().ToString(), ct);
        Assert.Equal(EstadosPedido.EnRevisionCoordinador, reenviado.Estado);
        Assert.Equal("reenviar", reenviado.Historial.Last().Accion);

        var borrador = Assert.IsType<PedidoDto>(Assert.IsType<CreatedAtActionResult>((await controller.Crear(
            Datos(Guid.Parse("d0000000-0000-4000-8000-000000000003")), ct)).Result).Value);
        Assert.IsType<NoContentResult>(await controller.Eliminar(borrador.Id, ct));
        Assert.Null(await db.Pedidos.FindAsync([borrador.Id], ct));
    }

    [Fact]
    public async Task Actor_sin_jefatura_no_puede_crear_y_un_error_no_deja_historial_parcial()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var docente = CrearServicio(
            Guid.Parse("a0000000-0000-4000-8000-000000000001"), identityDb, db);
        var datos = Datos(Guid.Parse("d0000000-0000-4000-8000-000000000004"));

        await Assert.ThrowsAsync<ErrorDominioPedido>(() => docente.CrearAsync(datos, ct));

        Assert.False(await db.Pedidos.AnyAsync(p => p.PersonaId == datos.PersonaId, ct));
        Assert.False(await db.PedidoHistorial.AnyAsync(h => h.ActorId ==
            Guid.Parse("a0000000-0000-4000-8000-000000000001"), ct));
    }

    [Fact]
    public async Task Edicion_invalida_conserva_pedido_y_adjuntos_confirmados()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var servicio = CrearServicio(Jefe, identityDb, db);
        var creado = await servicio.CrearAsync(Datos(
            Guid.Parse("d0000000-0000-4000-8000-000000000002")) with
        {
            Adjuntos = [new GuardarAdjuntoPedidoDto("cv", "original.pdf")],
        }, ct);

        await Assert.ThrowsAsync<ErrorDominioPedido>(() => servicio.EditarAsync(creado.Id, Datos(creado.Persona.Id) with
        {
            Horas = 99,
            Version = creado.Version,
            Adjuntos = [new GuardarAdjuntoPedidoDto("inventado", "invalido.pdf")],
        }, ct));

        db.ChangeTracker.Clear();
        var posterior = await servicio.ObtenerAsync(creado.Id, ct);
        Assert.Equal(10, posterior.Horas);
        Assert.Equal("original.pdf", Assert.Single(posterior.Adjuntos).Nombre);
        Assert.DoesNotContain(posterior.Historial, h => h.Accion == "editar");
    }

    [Fact]
    public async Task Envio_concurrente_con_la_misma_clave_se_ejecuta_una_sola_vez()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        Guid pedidoId;
        Guid otroPedidoId;
        await using (var identityInicial = PostgresFixture.CrearIdentity(Cadena))
        await using (var dbInicial = PostgresFixture.CrearDesignaciones(Cadena))
        {
            var servicio = CrearServicio(Jefe, identityInicial, dbInicial);
            pedidoId = (await servicio.CrearAsync(
                Datos(Guid.Parse("d0000000-0000-4000-8000-000000000002")), ct)).Id;
            otroPedidoId = (await servicio.CrearAsync(
                Datos(Guid.Parse("d0000000-0000-4000-8000-000000000003")), ct)).Id;
        }

        await using var identityUno = PostgresFixture.CrearIdentity(Cadena);
        await using var identityDos = PostgresFixture.CrearIdentity(Cadena);
        await using var dbUno = PostgresFixture.CrearDesignaciones(Cadena);
        await using var dbDos = PostgresFixture.CrearDesignaciones(Cadena);
        var uno = CrearServicio(Jefe, identityUno, dbUno);
        var dos = CrearServicio(Jefe, identityDos, dbDos);
        var clave = Guid.NewGuid();

        var respuestas = await Task.WhenAll(
            uno.AplicarAccionIdempotenteAsync(
                pedidoId, new AccionPedido.Enviar(), clave, "enviar", string.Empty, ct),
            dos.AplicarAccionIdempotenteAsync(
                pedidoId, new AccionPedido.Enviar(), clave, "enviar", string.Empty, ct));

        Assert.All(respuestas, r => Assert.Equal(EstadosPedido.EnRevisionCoordinador, r.Estado));
        await using var verificacion = PostgresFixture.CrearDesignaciones(Cadena);
        Assert.Equal(1, await verificacion.PedidoHistorial.CountAsync(
            h => h.PedidoId == pedidoId && h.Accion == "enviar", ct));
        Assert.Equal(1, await verificacion.ComandosIdempotentes.CountAsync(
            c => c.PedidoId == pedidoId && c.Clave == clave, ct));

        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() =>
            uno.AplicarAccionIdempotenteAsync(
                otroPedidoId, new AccionPedido.Enviar(), clave, "enviar", string.Empty, ct));
        Assert.Equal("idempotency-key-reused", error.Codigo);
    }

    private static IServicioPedidosApi CrearServicio(
        Guid usuarioId,
        IdentityDbContext identityDb,
        DesignacionesDbContext db)
    {
        var currentUser = new UsuarioActualFalso(usuarioId);
        var identity = new ConsultasIdentity(identityDb);
        var resolutor = new ResolutorActor(currentUser, identity);
        var pedidos = new RepositorioPedidos(db);
        var designaciones = new RepositorioDesignaciones(db);
        var core = new ServicioPedidos(
            pedidos,
            designaciones,
            new MaterializadorDesignaciones(designaciones),
            resolutor,
            identity,
            new UnidadDeTrabajo(db),
            NullLogger<ServicioPedidos>.Instance);
        return new ServicioPedidosApi(
            core, pedidos, resolutor, identity,
            new RepositorioIdempotencia(db), new UnidadDeTrabajo(db));
    }

    private static GuardarPedidoDto Datos(Guid personaId) => new(
        Periodo,
        personaId,
        Materia,
        Novedades.SinNovedad,
        null,
        null,
        10,
        0,
        0,
        null,
        null,
        null,
        []);

    private async Task EjecutarSeedAsync(CancellationToken ct)
    {
        var sql = await File.ReadAllTextAsync(
            Path.Combine(BuscarRaizRepositorio(), "infra", "scripts", "seed-data", "sintetico.sql"), ct);
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = 60 };
        await comando.ExecuteNonQueryAsync(ct);
    }

    private static string BuscarRaizRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null)
        {
            if (File.Exists(Path.Combine(directorio.FullName, "CLAUDE.md"))) return directorio.FullName;
            directorio = directorio.Parent;
        }
        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
    }

    private sealed class UsuarioActualFalso(Guid id) : ICurrentUser
    {
        public string UserId => id.ToString();
        public string? Email => "test@unlam.edu.ar";
        public IReadOnlyList<string> Roles => [];
        public bool IsAuthenticated => true;
    }
}
