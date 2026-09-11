using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Identity.Administracion;
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

public sealed class PedidosApiTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "pedidos_api")
{
    private static readonly Guid Periodo = Guid.Parse("d4000000-0000-4000-8000-000000000001");
    private static readonly Guid Materia = Guid.Parse("70000000-0000-4000-8000-000000000101");
    private static readonly Guid Jefe = Guid.Parse("a0000000-0000-4000-8000-000000000002");
    private static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");

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
        Assert.Equal(enviado.Materia.Nombre, enviado.Snapshot.Materia);

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
    public async Task Alta_con_datos_nuevos_crea_persona_sin_cuenta_y_pedido()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var controller = new PedidosController(CrearServicio(Jefe, identityDb, db));
        var documento = "39999888";

        var respuesta = await controller.Crear(new GuardarPedidoDto(
            Periodo,
            null,
            Materia,
            Novedades.Alta,
            Guid.Parse("c3000000-0000-4000-8000-000000000001"),
            Guid.Parse("d6000000-0000-4000-8000-000000000001"),
            10,
            0,
            0,
            "Solicitud de alta",
            null,
            null,
            [
                new GuardarAdjuntoPedidoDto(TiposAdjunto.Cv, "cv.pdf"),
                new GuardarAdjuntoPedidoDto(TiposAdjunto.DniFrente, "dni-frente.pdf"),
                new GuardarAdjuntoPedidoDto(TiposAdjunto.DniDorso, "dni-dorso.pdf"),
            ],
            Persona: new GuardarPersonaPedidoDto(documento, "Ada", "Lovelace")), ct);

        var creado = Assert.IsType<PedidoDto>(Assert.IsType<CreatedAtActionResult>(respuesta.Result).Value);
        var persona = await identityDb.Personas.SingleAsync(p => p.Documento == documento, ct);

        Assert.Equal(persona.Id, creado.Persona.Id);
        Assert.Equal(persona.Id, await db.Pedidos.Where(p => p.Id == creado.Id)
            .Select(p => p.PersonaId).SingleAsync(ct));
        Assert.False(await identityDb.Usuarios.AnyAsync(u => u.PersonaId == persona.Id, ct));

        var usuario = await new VinculadorPrimerLogin(identityDb).VincularAsync(
            new DatosPrimerLogin(Guid.NewGuid(), "ada@unlam.edu.ar", "Ada Lovelace", documento), ct);
        Assert.Equal(persona.Id, usuario.PersonaId);
        Assert.Equal(1, await identityDb.Personas.CountAsync(p => p.Documento == documento, ct));
    }

    [Fact]
    public async Task Historial_ordenado_por_instante_y_desempate_estable()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand("""
            UPDATE designaciones.pedido_historial
               SET created_at = CASE id
                   WHEN 'd7000000-0000-4000-8000-000000000004' THEN TIMESTAMPTZ '2026-06-14 12:00:00+00'
                   WHEN 'd7000000-0000-4000-8000-000000000005' THEN TIMESTAMPTZ '2026-06-14 12:00:00+00'
                   ELSE created_at
               END
             WHERE pedido_id = 'd5000000-0000-4000-8000-000000000003'
            """, conexion);
        await comando.ExecuteNonQueryAsync(ct);

        var pedido = await CrearServicio(Secretaria, identityDb, db).ObtenerAsync(
            Guid.Parse("d5000000-0000-4000-8000-000000000003"), ct);

        Assert.Equal(["crear", "enviar", "aceptar", "priorizar"], pedido.Historial.Select(h => h.Accion));
        Assert.Equal(
            [
                Guid.Parse("d7000000-0000-4000-8000-000000000014"),
                Guid.Parse("d7000000-0000-4000-8000-000000000003"),
                Guid.Parse("d7000000-0000-4000-8000-000000000004"),
                Guid.Parse("d7000000-0000-4000-8000-000000000005"),
            ], pedido.Historial.Select(h => h.Id));
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
    public async Task Backend_exige_documentacion_justificacion_y_legajo_segun_novedad()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var servicio = CrearServicio(Jefe, identityDb, db);
        var personaConLegajo = Guid.Parse("d0000000-0000-4000-8000-000000000002");
        var personaSinLegajo = Guid.Parse("d0000000-0000-4000-8000-000000000012");

        await Assert.ThrowsAsync<ErrorDominioPedido>(() => servicio.CrearAsync(
            Datos(personaConLegajo) with { Novedad = Novedades.Alta, Adjuntos = [] }, ct));
        await Assert.ThrowsAsync<ErrorDominioPedido>(() => servicio.CrearAsync(
            Datos(personaConLegajo) with { Novedad = Novedades.Baja }, ct));
        await Assert.ThrowsAsync<ErrorDominioPedido>(() => servicio.CrearAsync(
            Datos(personaConLegajo) with
            {
                Novedad = Novedades.CambioDeCargoODedicacion,
                Justificacion = null,
            }, ct));
        await Assert.ThrowsAsync<ErrorDominioPedido>(() => servicio.CrearAsync(
            Datos(personaSinLegajo) with
            {
                Novedad = Novedades.Baja,
                TipoBaja = TiposBaja.Renuncia,
                Adjuntos = [new GuardarAdjuntoPedidoDto(TiposAdjunto.Justificativo, "baja.pdf")],
            }, ct));

        Assert.False(await db.Pedidos.AnyAsync(p =>
            p.PersonaId == personaConLegajo && p.Numero.StartsWith(DateTime.UtcNow.Year.ToString()), ct));
    }

    [Fact]
    public async Task Backend_rechaza_materia_ajena_y_baja_sin_designacion_en_la_materia()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var servicio = CrearServicio(Jefe, identityDb, db);
        var pedidosAntes = await db.Pedidos.CountAsync(ct);

        await Assert.ThrowsAsync<ErrorDominioPedido>(() => servicio.CrearAsync(
            Datos(Guid.Parse("d0000000-0000-4000-8000-000000000002")) with
            {
                MateriaId = Guid.Parse("70000000-0000-4000-8000-000000000201"),
            }, ct));
        await Assert.ThrowsAsync<ErrorDominioPedido>(() => servicio.CrearAsync(
            Datos(Guid.Parse("d0000000-0000-4000-8000-000000000002")) with
            {
                MateriaId = Guid.Parse("70000000-0000-4000-8000-000000000102"),
                Novedad = Novedades.Baja,
                TipoBaja = TiposBaja.Renuncia,
                Adjuntos = [new GuardarAdjuntoPedidoDto(TiposAdjunto.Justificativo, "baja.pdf")],
            }, ct));
        await Assert.ThrowsAsync<ErrorDominioPedido>(() => servicio.CrearAsync(
            Datos(Guid.Parse("d0000000-0000-4000-8000-000000000002")) with
            {
                Persona = new GuardarPersonaPedidoDto("39999777", "Grace", "Hopper"),
            }, ct));

        Assert.Equal(pedidosAntes, await db.Pedidos.CountAsync(ct));
        Assert.False(await identityDb.Personas.AnyAsync(p => p.Documento == "39999777", ct));
    }

    [Fact]
    public async Task Sin_novedad_no_se_crea_ni_se_edita_y_un_legado_se_lee_sin_poder_aceptarse()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        await using (var conexion = await AbrirConexionAsync())
        await using (var comando = new NpgsqlCommand("UPDATE designaciones.pedidos SET novedad = 'Sin novedad' WHERE id = 'd5000000-0000-4000-8000-000000000002'", conexion))
        {
            await comando.ExecuteNonQueryAsync(ct);
        }
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var persona = Guid.Parse("d0000000-0000-4000-8000-000000000003");
        var servicioJefe = CrearServicio(Jefe, identityDb, db);

        await Assert.ThrowsAsync<ErrorDominioPedido>(() => servicioJefe.CrearAsync(
            Datos(persona) with { Novedad = Novedades.SinNovedad }, ct));
        Assert.False(await db.Pedidos.AnyAsync(p => p.PersonaId == persona, ct));

        var creado = await servicioJefe.CrearAsync(Datos(persona), ct);
        await Assert.ThrowsAsync<ErrorDominioPedido>(() => servicioJefe.EditarAsync(
            creado.Id,
            Datos(persona) with { Novedad = Novedades.SinNovedad, Version = creado.Version },
            ct));

        var servicioCoordinador = CrearServicio(Coordinador, identityDb, db);
        var legado = await servicioCoordinador.ObtenerAsync(
            Guid.Parse("d5000000-0000-4000-8000-000000000002"), ct);
        Assert.Equal(Novedades.SinNovedad, legado.Novedad);
        var historial = await db.PedidoHistorial.CountAsync(h => h.PedidoId == legado.Id, ct);
        var estado = await db.Pedidos.Where(p => p.Id == legado.Id)
            .Select(p => new { p.Estado, p.Version }).SingleAsync(ct);

        await Assert.ThrowsAsync<ErrorDominioPedido>(() => servicioCoordinador.AplicarAccionAsync(
            legado.Id, new AccionPedido.Aceptar(), ct));

        var posterior = await db.Pedidos.Where(p => p.Id == legado.Id)
            .Select(p => new { p.Estado, p.Version }).SingleAsync(ct);
        Assert.Equal(estado, posterior);
        Assert.Equal(historial, await db.PedidoHistorial.CountAsync(h => h.PedidoId == legado.Id, ct));
    }

    [Fact]
    public async Task Un_pedido_historico_con_materia_inactiva_sigue_visible()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var materia = await identityDb.Materias.SingleAsync(m => m.Id == Materia, ct);
        materia.Activo = false;
        await identityDb.SaveChangesAsync(ct);
        var servicio = CrearServicio(Secretaria, identityDb, db);
        var pedidoId = Guid.Parse("d5000000-0000-4000-8000-000000000001");

        Assert.Contains(await servicio.ListarAsync(Periodo, ct), p => p.Id == pedidoId);
        Assert.Equal(Materia, (await servicio.ObtenerAsync(pedidoId, ct)).Materia.Id);
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
            Adjuntos =
            [
                new GuardarAdjuntoPedidoDto(TiposAdjunto.Cv, "original.pdf"),
                new GuardarAdjuntoPedidoDto(TiposAdjunto.DniFrente, "frente.pdf"),
                new GuardarAdjuntoPedidoDto(TiposAdjunto.DniDorso, "dorso.pdf"),
            ],
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
        Assert.Contains(posterior.Adjuntos, a => a.Nombre == "original.pdf");
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
            NullLogger<ServicioPedidos>.Instance,
            new ServicioPersonas(new RepositorioDocentes(identityDb)));
        return new ServicioPedidosApi(
            core, pedidos, resolutor, identity,
            new RepositorioIdempotencia(db), new UnidadDeTrabajo(db));
    }

    private static GuardarPedidoDto Datos(Guid personaId) => new(
        Periodo, personaId, Materia, Novedades.Alta,
        Guid.Parse("c3000000-0000-4000-8000-000000000001"),
        Guid.Parse("d6000000-0000-4000-8000-000000000001"),
        10,
        0,
        0,
        "Solicitud de alta",
        null,
        null,
        [
            new GuardarAdjuntoPedidoDto(TiposAdjunto.Cv, "cv.pdf"),
            new GuardarAdjuntoPedidoDto(TiposAdjunto.DniFrente, "dni-frente.pdf"),
            new GuardarAdjuntoPedidoDto(TiposAdjunto.DniDorso, "dni-dorso.pdf"),
        ]);

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
