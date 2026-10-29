using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Xml.Linq;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Designaciones.Api;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure;
using Modules.Designaciones.Repositories;
using Modules.Designaciones.Services;
using Modules.Designaciones.Services.Integridad;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Designaciones;

/// <summary>Garantías de la cadena de sellos del circuito [BR-designaciones-019] y [BR-designaciones-020].</summary>
public sealed class IntegridadPedidosTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "integridad_pedidos")
{
    private static readonly Guid Periodo = Guid.Parse("d4000000-0000-4000-8000-000000000001");
    private static readonly Guid Materia = Guid.Parse("70000000-0000-4000-8000-000000000101");
    private static readonly Guid Carrera = Guid.Parse("c0000000-0000-4000-8000-000000000201");
    private static readonly Guid Jefe = Guid.Parse("a0000000-0000-4000-8000-000000000002");
    private static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");
    private static readonly Guid Decanato = Guid.Parse("a0000000-0000-4000-8000-000000000005");
    private static readonly Guid Sistemas = Guid.Parse("a0000000-0000-4000-8000-000000000007");
    private static readonly Guid PersonaLibre = Guid.Parse("d0000000-0000-4000-8000-000000000004");
    private static readonly Guid OtraPersonaLibre = Guid.Parse("d0000000-0000-4000-8000-000000000003");

    [Fact]
    public async Task Circuito_por_la_aplicacion_queda_sellado_y_un_borrador_sigue_siendo_eliminable()
    {
        var ct = TestContext.Current.CancellationToken;
        await PrepararAsync(ct);
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);

        var pedido = await CrearServicio(Jefe, identityDb, db).CrearAsync(Datos(PersonaLibre), ct);
        var antesDeEnviar = await ContarSellosAsync(db, ct);
        Assert.Empty(await db.PedidoSellos.Where(s => s.PedidoId == pedido.Id).ToListAsync(ct));
        await CrearServicio(Jefe, identityDb, db).AplicarAccionAsync(pedido.Id, new AccionPedido.Enviar(), ct);
        await CrearServicio(Coordinador, identityDb, db).AplicarAccionAsync(pedido.Id, new AccionPedido.Aceptar(), ct);

        var borrador = await CrearServicio(Jefe, identityDb, db).CrearAsync(Datos(OtraPersonaLibre), ct);
        await CrearServicio(Jefe, identityDb, db).EliminarAsync(borrador.Id, ct);

        Assert.Equal(antesDeEnviar + 2, await ContarSellosAsync(db, ct));
        var resultado = await SellosPrueba.Verificador(db).VerificarAsync(Periodo, ct);
        Assert.True(resultado.Integro, string.Join(", ", resultado.Hallazgos));
    }

    [Fact]
    public async Task Si_el_sello_no_puede_registrarse_la_transicion_no_se_confirma()
    {
        var ct = TestContext.Current.CancellationToken;
        await PrepararAsync(ct);
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var pedido = await CrearServicio(Jefe, identityDb, db).CrearAsync(Datos(PersonaLibre), ct);
        var sinClave = SellosPrueba.Claves(ambiente: "Production");

        await Assert.ThrowsAnyAsync<Exception>(() =>
            CrearServicio(Jefe, identityDb, db, sinClave).AplicarAccionAsync(pedido.Id, new AccionPedido.Enviar(), ct));

        await using var lectura = PostgresFixture.CrearDesignaciones(Cadena);
        var persistido = await lectura.Pedidos.Include(p => p.Historial).SingleAsync(p => p.Id == pedido.Id, ct);
        Assert.Equal(EstadosPedido.Borrador, persistido.Estado);
        Assert.DoesNotContain(persistido.Historial, h => h.Accion == AccionesHistorial.Enviar);
    }

    [Fact]
    public void Formato_canonico_del_sello_no_cambia_sin_version_nueva()
    {
        // Cambiar el formato invalida toda cadena ya sellada: este hash fija la versión v1.
        var periodo = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var pedido = new Pedido
        {
            Id = Guid.Parse("22222222-2222-4222-8222-222222222222"),
            Numero = "2026-0001",
            PeriodoId = periodo,
            PersonaId = Guid.Parse("33333333-3333-4333-8333-333333333333"),
            MateriaId = Guid.Parse("44444444-4444-4444-8444-444444444444"),
            CarreraId = Guid.Parse("55555555-5555-4555-8555-555555555555"),
            Novedad = Novedades.Alta,
            Estado = EstadosPedido.EnRevisionCoordinador,
            Horas = 10,
            Justificacion = "Designación «nueva»",
            Snapshot = new SnapshotPedido("Profesor", "Categoría 2", 8, "Álgebra", null, 1),
            Adjuntos = [new PedidoAdjunto { Id = Guid.Parse("66666666-6666-4666-8666-666666666666"), Tipo = TiposAdjunto.Cv, Nombre = "cv.pdf" }],
        };
        var evento = new PedidoHistorial
        {
            Id = Guid.Parse("77777777-7777-4777-8777-777777777777"),
            PedidoId = pedido.Id,
            Accion = AccionesHistorial.Enviar,
            RolId = Guid.Parse("a1000000-0000-4000-8000-000000000002"),
            ActorId = Jefe,
            Etapa = EstadosPedido.EnRevisionCoordinador,
            CreadoEn = new DateTimeOffset(2026, 10, 28, 12, 30, 45, 123, TimeSpan.Zero).AddMicroseconds(456),
        };

        var estado = CanonicoSellos.HashEstado(pedido);
        var hash = CanonicoSellos.Hash(CanonicoSellos.Genesis(periodo), periodo, 1, evento, estado);

        Assert.Equal("f2d9629738a8361e8f45abf9b03a10223efaf3fadcb937294d3915e9917315e1", Convert.ToHexStringLower(estado));
        Assert.Equal("05b141d2266baab528b24947e7e35a8497057fe475bd5041625ee8fd4eac43f3", Convert.ToHexStringLower(hash));
    }

    [Fact]
    public async Task Cambios_por_SQL_fuera_del_circuito_se_detectan()
    {
        var ct = TestContext.Current.CancellationToken;
        await PrepararAsync(ct);
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var pedido = await EnviarAsync(identityDb, db, PersonaLibre, ct);

        await EjecutarAsync("UPDATE designaciones.pedidos SET estado = 'en_lote' WHERE id = @id", ct,
            new NpgsqlParameter("id", pedido.Id));
        var eventoFalso = Guid.NewGuid();
        await EjecutarAsync("""
            INSERT INTO designaciones.pedido_historial (id, pedido_id, accion, rol_id, actor_id, etapa)
            VALUES (@evento, @pedido, 'aceptar', 'a1000000-0000-4000-8000-000000000005', @actor, 'en_lote')
            """, ct,
            new NpgsqlParameter("evento", eventoFalso), new NpgsqlParameter("pedido", pedido.Id),
            new NpgsqlParameter("actor", Decanato));

        var resultado = await SellosPrueba.Verificador(db).VerificarAsync(Periodo, ct);
        Assert.False(resultado.Integro);
        Assert.Contains(new HallazgoCadena(CategoriasIntegridad.PedidoEstadoDivergente, pedido.Id), resultado.Hallazgos);
        Assert.Contains(new HallazgoCadena(CategoriasIntegridad.HistorialSinSello, eventoFalso), resultado.Hallazgos);

        // Quien recalcule hashes sin la clave del host no produce firmas válidas.
        var otraClave = SellosPrueba.Claves(new IntegridadOptions
        {
            ClavesSellado = { ["1"] = Convert.ToBase64String(new byte[32]) },
        });
        var conOtraClave = await SellosPrueba.Verificador(db, otraClave).VerificarAsync(Periodo, ct);
        Assert.Contains(conOtraClave.Hallazgos, h => h.Categoria == CategoriasIntegridad.SelloFirmaInvalida);
    }

    [Fact]
    public async Task Base_rechaza_alterar_el_circuito_y_saltear_sus_protecciones_rompe_el_hash()
    {
        var ct = TestContext.Current.CancellationToken;
        await PrepararAsync(ct);
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var pedido = await EnviarAsync(identityDb, db, PersonaLibre, ct);
        var evento = await db.PedidoHistorial.AsNoTracking()
            .SingleAsync(h => h.PedidoId == pedido.Id && h.Accion == AccionesHistorial.Enviar, ct);
        var parametro = () => new NpgsqlParameter("id", pedido.Id);

        await AssertRechazoAsync("42501", "UPDATE designaciones.pedido_historial SET comentario = 'x' WHERE pedido_id = @id AND etapa <> 'borrador'", parametro());
        await AssertRechazoAsync("42501", "DELETE FROM designaciones.pedido_historial WHERE pedido_id = @id", parametro());
        await AssertRechazoAsync("42501", "DELETE FROM designaciones.pedido_sellos WHERE pedido_id = @id", parametro());
        await AssertRechazoAsync("42501", "UPDATE designaciones.pedido_sellos SET version_clave = 2 WHERE pedido_id = @id", parametro());
        await AssertRechazoAsync("42501", "DELETE FROM designaciones.pedidos WHERE id = @id", parametro());
        await AssertRechazoAsync("23514", """
            INSERT INTO designaciones.pedido_sellos
                (periodo_id, secuencia, historial_id, pedido_id, hash_anterior, hash_estado, hash, firma, version_clave)
            SELECT periodo_id, secuencia + 5, historial_id, pedido_id, hash, hash, hash, hash, 1
              FROM designaciones.pedido_sellos WHERE pedido_id = @id
            """, parametro());

        await EjecutarAsync("""
            SET session_replication_role = replica;
            UPDATE designaciones.pedido_historial SET comentario = 'Comentario reescrito' WHERE id = @evento;
            """, ct, new NpgsqlParameter("evento", evento.Id));

        var resultado = await SellosPrueba.Verificador(db).VerificarAsync(Periodo, ct);
        Assert.Contains(new HallazgoCadena(CategoriasIntegridad.SelloHashInvalido, evento.Id), resultado.Hallazgos);

        async Task AssertRechazoAsync(string codigo, string sql, NpgsqlParameter p)
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => EjecutarAsync(sql, ct, p));
            Assert.Equal(codigo, error.SqlState);
        }
    }

    [Fact]
    public async Task Transiciones_concurrentes_del_mismo_periodo_no_bifurcan_la_cadena()
    {
        var ct = TestContext.Current.CancellationToken;
        await PrepararAsync(ct);
        var pedidos = new List<Guid>();
        foreach (var persona in new[] { PersonaLibre, OtraPersonaLibre })
        {
            await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
            await using var db = PostgresFixture.CrearDesignaciones(Cadena);
            pedidos.Add((await CrearServicio(Jefe, identityDb, db).CrearAsync(Datos(persona), ct)).Id);
        }

        await Task.WhenAll(pedidos.Select(async id =>
        {
            await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
            await using var db = PostgresFixture.CrearDesignaciones(Cadena);
            await CrearServicio(Jefe, identityDb, db).AplicarAccionAsync(id, new AccionPedido.Enviar(), ct);
        }));

        await using var lectura = PostgresFixture.CrearDesignaciones(Cadena);
        var secuencias = await lectura.PedidoSellos.Where(s => s.PeriodoId == Periodo)
            .OrderBy(s => s.Secuencia).Select(s => s.Secuencia).ToListAsync(ct);
        Assert.Equal(Enumerable.Range(1, secuencias.Count).Select(i => (long)i), secuencias);
        Assert.True((await SellosPrueba.Verificador(lectura).VerificarAsync(Periodo, ct)).Integro);
    }

    [Fact]
    public async Task Lote_imprime_la_cabeza_anclada_y_se_bloquea_ante_una_ruptura()
    {
        var ct = TestContext.Current.CancellationToken;
        await PrepararAsync(ct);
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        // Un evento posterior al sellado retroactivo deja una cabeza todavía sin anclar.
        await EnviarAsync(identityDb, db, PersonaLibre, ct);
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Decanato, "decanato");
        var url = $"/api/designaciones/periodos/{Periodo}/lote.xlsx";

        using var primera = await cliente.GetAsync(url, ct);
        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);
        var integridad = await LeerCeldaIntegridadAsync(primera, ct);
        var cabeza = await db.PedidoSellos.Where(s => s.PeriodoId == Periodo)
            .OrderByDescending(s => s.Secuencia).FirstAsync(ct);
        Assert.Equal($"INTEGRIDAD · sello {cabeza.Secuencia} · {Convert.ToHexStringLower(cabeza.Hash)}", integridad);

        using var segunda = await cliente.GetAsync(url, ct);
        Assert.Equal(integridad, await LeerCeldaIntegridadAsync(segunda, ct));
        var anclas = await db.PeriodoAnclas.Where(a => a.PeriodoId == Periodo && a.Secuencia == cabeza.Secuencia)
            .ToListAsync(ct);
        Assert.Equal(MotivosAncla.ExportacionLote, Assert.Single(anclas).Motivo);

        // Un superusuario que borra un pedido sellado completo no puede reproducir la cabeza anclada.
        var pedido = cabeza.PedidoId;
        await EjecutarAsync("""
            SET session_replication_role = replica;
            DELETE FROM designaciones.pedido_sellos WHERE pedido_id = @pedido;
            DELETE FROM designaciones.pedido_historial WHERE pedido_id = @pedido;
            DELETE FROM designaciones.pedido_adjuntos WHERE pedido_id = @pedido;
            DELETE FROM designaciones.designaciones WHERE origen_pedido_id = @pedido;
            DELETE FROM designaciones.pedidos WHERE id = @pedido;
            """, ct, new NpgsqlParameter("periodo", Periodo), new NpgsqlParameter("pedido", pedido));

        var resultado = await SellosPrueba.Verificador(db).VerificarAsync(Periodo, ct);
        Assert.Contains(resultado.Hallazgos, h => h.Categoria == CategoriasIntegridad.AnclaNoReproducida);
        using var bloqueada = await cliente.GetAsync(url, ct);
        Assert.Equal(HttpStatusCode.Conflict, bloqueada.StatusCode);
        Assert.DoesNotContain(CategoriasIntegridad.AnclaNoReproducida, await bloqueada.Content.ReadAsStringAsync(ct));
    }

    [Fact]
    public async Task Consulta_de_integridad_exige_auditoria()
    {
        var ct = TestContext.Current.CancellationToken;
        await PrepararAsync(ct);
        using var host = CrearHost();
        var url = $"/api/designaciones/periodos/{Periodo}/integridad";

        using var decanato = host.CreateClient();
        Autenticar(decanato, Decanato, "decanato");
        Assert.Equal(HttpStatusCode.Forbidden, (await decanato.GetAsync(url, ct)).StatusCode);

        using var sistemas = host.CreateClient();
        Autenticar(sistemas, Sistemas, "sys_admin");
        var resultado = await sistemas.GetFromJsonAsync<IntegridadPeriodoDto>(url, ct);
        Assert.True(resultado!.Integro);
        Assert.Equal(64, resultado.Hash.Length);
    }

    [Fact]
    public async Task Rotar_la_clave_conserva_la_verificacion_y_el_sellado_retroactivo_es_idempotente()
    {
        var ct = TestContext.Current.CancellationToken;
        await PrepararAsync(ct);
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var v1 = Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray());
        var v2 = Convert.ToBase64String(Enumerable.Repeat((byte)2, 32).ToArray());
        var soloV1 = SellosPrueba.Claves(new IntegridadOptions { ClavesSellado = { ["1"] = v1 }, VersionVigente = 1 });
        var rotadas = SellosPrueba.Claves(new IntegridadOptions
        {
            ClavesSellado = { ["1"] = v1, ["2"] = v2 },
            VersionVigente = 2,
        });

        var pedido = await CrearServicio(Jefe, identityDb, db, soloV1).CrearAsync(Datos(PersonaLibre), ct);
        await CrearServicio(Jefe, identityDb, db, soloV1).AplicarAccionAsync(pedido.Id, new AccionPedido.Enviar(), ct);
        await CrearServicio(Coordinador, identityDb, db, rotadas).AplicarAccionAsync(pedido.Id, new AccionPedido.Aceptar(), ct);

        var versiones = await db.PedidoSellos.Where(s => s.PedidoId == pedido.Id)
            .OrderBy(s => s.Secuencia).Select(s => s.VersionClave).ToListAsync(ct);
        Assert.Equal([(short)1, (short)2], versiones);
        // Los sellos del seed se hicieron con la clave de desarrollo; sólo interesan los del pedido.
        var resultado = await SellosPrueba.Verificador(db, rotadas).VerificarAsync(Periodo, ct);
        var sellosPedido = await db.PedidoSellos.Where(s => s.PedidoId == pedido.Id).Select(s => s.HistorialId).ToListAsync(ct);
        Assert.DoesNotContain(resultado.Hallazgos, h => sellosPedido.Contains(h.Id));

        var sellosAntes = await ContarSellosAsync(db, ct);
        var anclasAntes = await db.PeriodoAnclas.CountAsync(ct);
        Assert.Equal(0, await SellosPrueba.Sellador(db).SellarHistorialExistenteAsync(ct));
        Assert.Equal(sellosAntes, await ContarSellosAsync(db, ct));
        Assert.Equal(anclasAntes, await db.PeriodoAnclas.CountAsync(ct));
    }

    /// <summary>Migración ya aplicada, seed sintético y sellado retroactivo: el orden de un despliegue.</summary>
    private async Task PrepararAsync(CancellationToken ct)
    {
        var sql = await File.ReadAllTextAsync(
            Path.Combine(BuscarRaizRepositorio(), "infra", "scripts", "seed-data", "sintetico.sql"), ct);
        await EjecutarAsync(sql, ct);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        Assert.True(await SellosPrueba.Sellador(db).SellarHistorialExistenteAsync(ct) > 0);
        Assert.True((await SellosPrueba.Verificador(db).VerificarAsync(Periodo, ct)).Integro);
    }

    private async Task<PedidoDto> EnviarAsync(IdentityDbContext identityDb, DesignacionesDbContext db, Guid persona, CancellationToken ct)
    {
        var pedido = await CrearServicio(Jefe, identityDb, db).CrearAsync(Datos(persona), ct);
        return await CrearServicio(Jefe, identityDb, db).AplicarAccionAsync(pedido.Id, new AccionPedido.Enviar(), ct);
    }

    private static Task<int> ContarSellosAsync(DesignacionesDbContext db, CancellationToken ct) =>
        db.PedidoSellos.CountAsync(ct);

    private static ServicioPedidosApi CrearServicio(
        Guid usuarioId, IdentityDbContext identityDb, DesignacionesDbContext db, ClavesSellado? claves = null)
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
            SellosPrueba.Sellador(db, claves),
            NullLogger<ServicioPedidos>.Instance);
        return new ServicioPedidosApi(
            core, pedidos, resolutor, identity,
            new RepositorioIdempotencia(db), new UnidadDeTrabajo(db));
    }

    private static GuardarPedidoDto Datos(Guid personaId) => new(
        Periodo, personaId, Materia, Carrera, Novedades.Alta,
        Guid.Parse("c3000000-0000-4000-8000-000000000001"),
        Guid.Parse("d6000000-0000-4000-8000-000000000001"),
        10, 0, 0, "Solicitud de alta", null, null,
        [
            new GuardarAdjuntoPedidoDto(TiposAdjunto.Cv, "cv.pdf"),
            new GuardarAdjuntoPedidoDto(TiposAdjunto.DniFrente, "dni-frente.pdf"),
            new GuardarAdjuntoPedidoDto(TiposAdjunto.DniDorso, "dni-dorso.pdf"),
        ]);

    private WebApplicationFactory<Program> CrearHost() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:ArsDocendi", Cadena);
            builder.UseSetting($"{AutenticacionDesarrolloOptions.Seccion}:Enabled", "true");
        });

    private static void Autenticar(HttpClient cliente, Guid usuario, string rol)
    {
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, usuario.ToString());
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, rol);
    }

    private static async Task<string> LeerCeldaIntegridadAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        using var zip = new ZipArchive(new MemoryStream(await respuesta.Content.ReadAsByteArrayAsync(ct)), ZipArchiveMode.Read);
        await using var stream = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var hoja = await XDocument.LoadAsync(stream, LoadOptions.None, ct);
        return hoja.Descendants().Single(e => e.Name.LocalName == "c" && e.Attribute("r")?.Value == "M1").Value;
    }

    private async Task EjecutarAsync(string sql, CancellationToken ct, params NpgsqlParameter[] parametros)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = 60 };
        comando.Parameters.AddRange(parametros);
        await comando.ExecuteNonQueryAsync(ct);
    }

    private static string BuscarRaizRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null)
        {
            if (File.Exists(Path.Combine(directorio.FullName, "AGENTS.md"))) return directorio.FullName;
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
