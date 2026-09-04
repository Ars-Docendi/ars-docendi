using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
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
public sealed class RevisionPedidosTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "revision_pedidos")
{
    private static readonly Guid Periodo = Guid.Parse("d4000000-0000-4000-8000-000000000001");
    private static readonly Guid Materia = Guid.Parse("70000000-0000-4000-8000-000000000101");
    private static readonly Guid Jefe = Guid.Parse("a0000000-0000-4000-8000-000000000002");
    private static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static readonly Guid Decanato = Guid.Parse("a0000000-0000-4000-8000-000000000005");

    [Fact]
    public async Task Listados_se_acotan_por_materia_carrera_y_alcance_global()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);

        var jefe = await CrearServicio(Jefe, identityDb, db).ListarAsync(Periodo, ct);
        var coordinador = await CrearServicio(Coordinador, identityDb, db).ListarAsync(Periodo, ct);
        var global = await CrearServicio(Secretaria, identityDb, db).ListarAsync(Periodo, ct);

        Assert.NotEmpty(jefe);
        Assert.All(jefe, p => Assert.Equal(Materia, p.Materia.Id));
        Assert.NotEmpty(coordinador);
        Assert.All(coordinador, p => Assert.Equal(
            Guid.Parse("c0000000-0000-4000-8000-000000000201"), p.Materia.CarreraId));
        Assert.Equal(8, global.Count);
        Assert.DoesNotContain(typeof(PedidosController).GetMethods().SelectMany(m => m.GetParameters()),
            p => p.ParameterType == typeof(ActorContexto));
    }

    [Fact]
    public async Task Cadena_de_aceptacion_usa_el_rol_de_cada_etapa_y_un_historial_atomico()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var pedido = await CrearServicio(Jefe, identityDb, db).CrearAsync(
            Datos(Guid.Parse("d0000000-0000-4000-8000-000000000002")), ct);
        pedido = await CrearServicio(Jefe, identityDb, db)
            .AplicarAccionAsync(pedido.Id, new AccionPedido.Enviar(), ct);

        pedido = await CrearServicio(Coordinador, identityDb, db)
            .AplicarAccionAsync(pedido.Id, new AccionPedido.Aceptar("Coordinación conforme"), ct);
        Assert.Equal(EstadosPedido.EnRevisionSecretaria, pedido.Estado);
        Assert.Equal(RolesCircuito.CoordinadorCarrera, pedido.Historial.Last().RolCodigo);
        pedido = await CrearServicio(Secretaria, identityDb, db)
            .AplicarAccionAsync(pedido.Id, new AccionPedido.Aceptar(), ct);
        Assert.Equal(EstadosPedido.EnRevisionDecanato, pedido.Estado);
        Assert.Equal(RolesCircuito.Secretaria, pedido.Historial.Last().RolCodigo);
        pedido = await CrearServicio(Decanato, identityDb, db)
            .AplicarAccionAsync(pedido.Id, new AccionPedido.Aceptar("Aprobado"), ct);

        Assert.Equal(EstadosPedido.EnLote, pedido.Estado);
        Assert.Equal(RolesCircuito.Decanato, pedido.Historial.Last().RolCodigo);
        db.ChangeTracker.Clear();
        var persistido = await db.Pedidos.Include(p => p.Historial).SingleAsync(p => p.Id == pedido.Id, ct);
        Assert.Equal(pedido.Estado, persistido.Estado);
        Assert.Equal(pedido.Historial.Count, persistido.Historial.Count);
    }

    [Fact]
    public async Task Aprobacion_final_materializa_una_designacion_trazable()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var persona = Guid.Parse("d0000000-0000-4000-8000-000000000003");
        var cargo = Guid.Parse("c3000000-0000-4000-8000-000000000003");
        var pedido = await CrearServicio(Jefe, identityDb, db).CrearAsync(Datos(persona) with
        {
            Novedad = Novedades.Alta,
            CargoSolicitadoId = cargo,
            DedicacionSolicitada = "Categoría 2",
            Horas = 16,
        }, ct);
        pedido = await CrearServicio(Jefe, identityDb, db)
            .AplicarAccionAsync(pedido.Id, new AccionPedido.Enviar(), ct);
        pedido = await CrearServicio(Coordinador, identityDb, db)
            .AplicarAccionAsync(pedido.Id, new AccionPedido.Aceptar(), ct);
        pedido = await CrearServicio(Secretaria, identityDb, db)
            .AplicarAccionAsync(pedido.Id, new AccionPedido.Aceptar(), ct);
        var servicioDecanato = CrearServicio(Decanato, identityDb, db);
        var clave = Guid.NewGuid();
        pedido = await servicioDecanato.AplicarAccionIdempotenteAsync(
            pedido.Id, new AccionPedido.Aceptar(), clave, "aceptar", string.Empty, ct);
        var repetido = await servicioDecanato.AplicarAccionIdempotenteAsync(
            pedido.Id, new AccionPedido.Aceptar(), clave, "aceptar", string.Empty, ct);

        db.ChangeTracker.Clear();
        var designacion = Assert.Single(await db.Designaciones.Where(d =>
            d.PersonaId == persona && d.MateriaId == Materia && d.VigenteHasta == null).ToListAsync(ct));
        Assert.Equal(pedido.Id, designacion.OrigenPedidoId);
        Assert.Equal(cargo, designacion.CargoId);
        Assert.Equal(16, designacion.Horas);
        Assert.Equal(pedido.Estado, repetido.Estado);
        Assert.Equal(1, await db.PedidoHistorial.CountAsync(
            h => h.PedidoId == pedido.Id && h.Accion == "aceptar" && h.RolId ==
                Guid.Parse("a1000000-0000-4000-8000-000000000005"), ct));
    }

    [Theory]
    [InlineData("rechazar")]
    [InlineData("devolver")]
    [InlineData("priorizar")]
    public void Comentarios_obligatorios_se_validan_sin_mutar_el_pedido(string accion)
    {
        var pedido = new Pedido
        {
            Id = Guid.NewGuid(),
            Numero = "TEST",
            PeriodoId = Periodo,
            PersonaId = Guid.NewGuid(),
            MateriaId = Materia,
            Novedad = Novedades.SinNovedad,
            Estado = accion == "priorizar" ? EstadosPedido.Borrador : EstadosPedido.EnRevisionCoordinador,
        };
        var actor = accion == "priorizar"
            ? Actor(RolesCircuito.JefeCatedra)
            : Actor(RolesCircuito.CoordinadorCarrera);
        AccionPedido comando = accion switch
        {
            "rechazar" => new AccionPedido.Rechazar(""),
            "devolver" => new AccionPedido.Devolver(""),
            _ => new AccionPedido.Priorizar(""),
        };

        Assert.Throws<ErrorDominioPedido>(() =>
            MaquinaEstadosPedido.AplicarAccion(pedido, Guid.Empty, comando, actor));
        Assert.False(pedido.Prioritario);
        Assert.Equal(accion == "priorizar" ? EstadosPedido.Borrador : EstadosPedido.EnRevisionCoordinador,
            pedido.Estado);
    }

    [Theory]
    [InlineData(EstadosPedido.EnRevisionCoordinador, RolesCircuito.Secretaria)]
    [InlineData(EstadosPedido.EnRevisionSecretaria, RolesCircuito.Decanato)]
    [InlineData(EstadosPedido.EnRevisionDecanato, RolesCircuito.CoordinadorCarrera)]
    public void Rol_incorrecto_no_puede_aceptar_la_etapa(string estado, string rol)
    {
        var pedido = new Pedido
        {
            Numero = "TEST",
            PeriodoId = Periodo,
            PersonaId = Guid.NewGuid(),
            MateriaId = Materia,
            Novedad = Novedades.SinNovedad,
            Estado = estado,
        };
        Assert.Throws<ErrorDominioPedido>(() => MaquinaEstadosPedido.AplicarAccion(
            pedido, Guid.NewGuid(), new AccionPedido.Aceptar(), Actor(rol)));
    }

    [Theory]
    [InlineData(EstadosPedido.EnRevisionCoordinador, RolesCircuito.CoordinadorCarrera, "aceptar")]
    [InlineData(EstadosPedido.EnRevisionCoordinador, RolesCircuito.CoordinadorCarrera, "rechazar")]
    [InlineData(EstadosPedido.EnRevisionCoordinador, RolesCircuito.CoordinadorCarrera, "devolver")]
    [InlineData(EstadosPedido.EnRevisionSecretaria, RolesCircuito.Secretaria, "aceptar")]
    [InlineData(EstadosPedido.EnRevisionSecretaria, RolesCircuito.Secretaria, "rechazar")]
    [InlineData(EstadosPedido.EnRevisionSecretaria, RolesCircuito.Secretaria, "devolver")]
    [InlineData(EstadosPedido.EnRevisionDecanato, RolesCircuito.Decanato, "aceptar")]
    [InlineData(EstadosPedido.EnRevisionDecanato, RolesCircuito.Decanato, "rechazar")]
    [InlineData(EstadosPedido.EnRevisionDecanato, RolesCircuito.Decanato, "devolver")]
    public void Cada_revisor_puede_actuar_en_su_etapa(string estado, string rol, string accion)
    {
        var pedido = PedidoEn(estado);
        AccionPedido comando = accion switch
        {
            "aceptar" => new AccionPedido.Aceptar(),
            "rechazar" => new AccionPedido.Rechazar("Justificación"),
            _ => new AccionPedido.Devolver("Corrección requerida"),
        };

        var transicion = MaquinaEstadosPedido.AplicarAccion(pedido, Guid.Empty, comando, Actor(rol));

        Assert.Equal(rol, transicion.CodigoRolActuante);
    }

    [Theory]
    [InlineData(RolesCircuito.JefeCatedra)]
    [InlineData(RolesCircuito.CoordinadorCarrera)]
    [InlineData(RolesCircuito.Secretaria)]
    [InlineData(RolesCircuito.Decanato)]
    [InlineData(RolesCircuito.Administrativo)]
    public void Cada_rol_del_circuito_puede_priorizar_y_despriorizar_en_su_ambito(string rol)
    {
        var pedido = PedidoEn(EstadosPedido.Borrador);
        var actor = Actor(rol);

        var prioridad = MaquinaEstadosPedido.AplicarAccion(
            pedido, Guid.Empty, new AccionPedido.Priorizar("Urgente"), actor);
        var sinPrioridad = MaquinaEstadosPedido.AplicarAccion(
            pedido, Guid.Empty, new AccionPedido.Despriorizar(), actor);

        Assert.True(prioridad.Prioritario);
        Assert.False(sinPrioridad.Prioritario);
        Assert.Equal(rol, prioridad.CodigoRolActuante);
    }

    [Theory]
    [InlineData(EstadosPedido.EnRevisionCoordinador)]
    [InlineData(EstadosPedido.EnRevisionSecretaria)]
    [InlineData(EstadosPedido.EnRevisionDecanato)]
    public void Administrativo_revisa_pero_no_acepta_en_cada_etapa(string estado)
    {
        var pedido = PedidoEn(estado);
        var actor = Actor(RolesCircuito.Administrativo);

        Assert.Throws<ErrorDominioPedido>(() => MaquinaEstadosPedido.AplicarAccion(
            pedido, Guid.Empty, new AccionPedido.Aceptar(), actor));
        Assert.Equal(EstadosPedido.Rechazado, MaquinaEstadosPedido.AplicarAccion(
            pedido, Guid.Empty, new AccionPedido.Rechazar("No corresponde"), actor).EstadoResultante);
        Assert.Equal(EstadosPedido.Devuelto, MaquinaEstadosPedido.AplicarAccion(
            pedido, Guid.Empty, new AccionPedido.Devolver("Corregir"), actor).EstadoResultante);
    }

    private static Pedido PedidoEn(string estado) => new()
    {
        Id = Guid.NewGuid(),
        Numero = "TEST",
        PeriodoId = Periodo,
        PersonaId = Guid.NewGuid(),
        MateriaId = Materia,
        Novedad = Novedades.SinNovedad,
        Estado = estado,
    };

    private static ActorContexto Actor(string rol) => new(
        Guid.NewGuid(),
        new HashSet<string> { rol },
        new HashSet<Guid> { Materia },
        new HashSet<Guid> { Guid.Empty });

    private static IServicioPedidosApi CrearServicio(
        Guid usuarioId,
        IdentityDbContext identityDb,
        DesignacionesDbContext db)
    {
        var identity = new ConsultasIdentity(identityDb);
        var resolutor = new ResolutorActor(new UsuarioActualFalso(usuarioId), identity);
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
        Periodo, personaId, Materia, Novedades.SinNovedad,
        null, null, 10, 0, 0, null, null, null, []);

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
