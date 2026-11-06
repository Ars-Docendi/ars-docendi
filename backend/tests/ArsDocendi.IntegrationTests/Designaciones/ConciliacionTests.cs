using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Storage.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Designaciones.Api;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure;
using Modules.Designaciones.Repositories;
using Modules.Designaciones.Services;

namespace ArsDocendi.IntegrationTests.Designaciones;

/// <summary>
/// Sincronización de la conciliación sobre una base desechable: la persona queda con su
/// designación y su rol Docente, todo o nada (conciliacion-designaciones).
/// </summary>
public sealed class ConciliacionTests(PostgresFixture postgres) : VigenciasPedidosBase(postgres)
{
    private static readonly Guid Materia = Guid.Parse("70000000-0000-4000-8000-000000000101");
    private static readonly Guid Carrera = Guid.Parse("c0000000-0000-4000-8000-000000000201");
    private static readonly Guid RolDocente = Guid.Parse("a1000000-0000-4000-8000-000000000001");
    /// <summary>Alta en revisión de Decanato de una persona sin cuenta.</summary>
    private static readonly Guid AltaId = Guid.Parse("d5000000-0000-4000-8000-000000000004");
    /// <summary>Carla López: tiene cuenta y rol Docente en la materia 101.</summary>
    private static readonly Guid PersonaConCuenta = Guid.Parse("d0000000-0000-4000-8000-000000000001");
    /// <summary>Marina Díaz: tiene cuenta (Coordinadora) y ninguna designación en la materia 101.</summary>
    private static readonly Guid PersonaSinDesignacion = Guid.Parse("d0000000-0000-4000-8000-000000000003");
    /// <summary>9006: en lote por el circuito anterior, ya materializado.</summary>
    private static readonly Guid HeredadoId = Guid.Parse("d5000000-0000-4000-8000-000000000006");

    [Fact]
    public async Task Alta_de_persona_sin_mail_queda_con_su_designacion_y_sin_cuenta()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        var alta = await EnLoteAsync(db, AltaId, _ => { }, ct);

        await ConciliacionPrueba.SincronizarAsync(AltaId, identity, db, ct);

        var designacion = await db.Designaciones.AsNoTracking().SingleAsync(d =>
            d.PersonaId == alta.PersonaId && d.MateriaId == Materia && d.VigenteHasta == null, ct);
        Assert.Equal(AltaId, designacion.OrigenPedidoId);
        Assert.Equal(alta.Horas, designacion.Horas);
        Assert.False(await identity.Usuarios.AnyAsync(u => u.PersonaId == alta.PersonaId, ct));
        var pedido = await db.Pedidos.AsNoTracking().SingleAsync(p => p.Id == AltaId, ct);
        Assert.Equal(EstadosPedido.Aprobado, pedido.Estado);
        Assert.NotNull(pedido.MaterializadoEn);
    }

    [Fact]
    public async Task Alta_de_persona_con_cuenta_le_asigna_el_rol_Docente_en_la_misma_transaccion()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        await EnLoteAsync(db, AltaId, p => p.PersonaId = PersonaSinDesignacion, ct);

        await ConciliacionPrueba.SincronizarAsync(AltaId, identity, db, ct);

        var usuario = await identity.Usuarios.AsNoTracking().SingleAsync(u => u.PersonaId == PersonaSinDesignacion, ct);
        var docente = await identity.UsuarioRoles.AsNoTracking().SingleAsync(r =>
            r.UsuarioId == usuario.Id && r.RolId == RolDocente, ct);
        Assert.Equal((Materia, Carrera), (docente.MateriaId!.Value, docente.CarreraId!.Value));
        Assert.Equal(ConciliacionPrueba.Secretaria, docente.OtorgadoPor);
        // El rol que ya tenía se conserva.
        Assert.True(await identity.UsuarioRoles.AnyAsync(r => r.UsuarioId == usuario.Id && r.RolId != RolDocente, ct));
    }

    [Fact]
    public async Task Baja_cierra_la_designacion_y_retira_solo_el_rol_Docente_de_esa_materia()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        await EnLoteAsync(db, PedidoId, p =>
        {
            p.Novedad = Novedades.Baja;
            p.TipoBaja = TiposBaja.Renuncia;
            p.Justificacion = null;
            p.CargoSolicitadoId = null;
            p.DedicacionSolicitadaId = null;
            p.Horas = null;
        }, ct);

        await ConciliacionPrueba.SincronizarAsync(PedidoId, identity, db, ct, new RelojFijo());

        Assert.False(await db.Designaciones.AnyAsync(d =>
            d.PersonaId == PersonaConCuenta && d.MateriaId == Materia && d.VigenteHasta == null, ct));
        var usuario = await identity.Usuarios.AsNoTracking().SingleAsync(u => u.PersonaId == PersonaConCuenta, ct);
        Assert.False(await identity.UsuarioRoles.AnyAsync(r =>
            r.UsuarioId == usuario.Id && r.RolId == RolDocente && r.MateriaId == Materia, ct));
        // Dado de baja, no borrado: la fila sigue con su fecha de baja.
        Assert.True(await identity.UsuarioRoles.IgnoreQueryFilters().AnyAsync(r =>
            r.UsuarioId == usuario.Id && r.RolId == RolDocente && r.MateriaId == Materia && r.EliminadoEn != null, ct));
    }

    [Fact]
    public async Task Un_pedido_que_cambio_de_version_no_deja_aplicar_ninguno()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        var alta = await EnLoteAsync(db, AltaId, _ => { }, ct);
        var cambio = await EnLoteAsync(db, PedidoId, _ => { }, ct);
        var horasVigentes = (await db.Designaciones.AsNoTracking().SingleAsync(d =>
            d.PersonaId == PersonaConCuenta && d.MateriaId == Materia && d.VigenteHasta == null, ct)).Horas;

        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() =>
            ConciliacionPrueba.Crear(ConciliacionPrueba.Secretaria, identity, db).SincronizarAsync(
                new SincronizarConciliacionDto(
                [
                    // Se aplican por persona: el Cambio (9001) se escribe antes de que el Alta,
                    // desactualizada, haga fallar la sincronización.
                    new PedidoAceptadoDto(AltaId, alta.Version + 1),
                    new PedidoAceptadoDto(PedidoId, cambio.Version),
                ]),
                Guid.NewGuid(), ct));

        Assert.Equal(TipoErrorAplicacion.Conflicto, error.Tipo);
        Assert.Contains(alta.Numero, error.Message, StringComparison.Ordinal);
        db.ChangeTracker.Clear();
        Assert.All(await db.Pedidos.Where(p => p.Id == AltaId || p.Id == PedidoId).ToListAsync(ct),
            p => Assert.Equal(EstadosPedido.EnLote, p.Estado));
        Assert.False(await db.Designaciones.AnyAsync(d => d.PersonaId == alta.PersonaId && d.VigenteHasta == null, ct));
        Assert.Equal(horasVigentes, (await db.Designaciones.SingleAsync(d =>
            d.PersonaId == PersonaConCuenta && d.MateriaId == Materia && d.VigenteHasta == null, ct)).Horas);
        Assert.False(await db.PedidoHistorial.AnyAsync(h => h.Accion == AccionesHistorial.Sincronizar, ct));
    }

    [Fact]
    public async Task Un_pedido_heredado_se_aprueba_sin_volver_a_escribir_la_designacion()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        var heredado = await db.Pedidos.AsNoTracking().SingleAsync(p => p.Id == HeredadoId, ct);
        Assert.NotNull(heredado.MaterializadoEn);
        await SellosPrueba.Sellador(db).SellarHistorialExistenteAsync(ct);
        var antes = await db.Designaciones.AsNoTracking()
            .Where(d => d.PersonaId == heredado.PersonaId).OrderBy(d => d.Id).ToListAsync(ct);

        await ConciliacionPrueba.SincronizarAsync(HeredadoId, identity, db, ct);

        var despues = await db.Designaciones.AsNoTracking()
            .Where(d => d.PersonaId == heredado.PersonaId).OrderBy(d => d.Id).ToListAsync(ct);
        Assert.Equal(antes.Select(d => (d.Id, d.Horas, d.VigenteHasta)), despues.Select(d => (d.Id, d.Horas, d.VigenteHasta)));
        var pedido = await db.Pedidos.AsNoTracking().SingleAsync(p => p.Id == HeredadoId, ct);
        Assert.Equal(EstadosPedido.Aprobado, pedido.Estado);
        Assert.Equal(heredado.MaterializadoEn, pedido.MaterializadoEn);
    }

    [Fact]
    public async Task Sincronizar_sella_el_evento_y_el_pedido_aprobado_no_admite_otra_sincronizacion()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        await EnLoteAsync(db, AltaId, _ => { }, ct);

        await ConciliacionPrueba.SincronizarAsync(AltaId, identity, db, ct);

        db.ChangeTracker.Clear();
        var aprobado = await db.Pedidos.Include(p => p.Adjuntos).Include(p => p.Historial)
            .SingleAsync(p => p.Id == AltaId, ct);
        // El último sello coincide con el pedido aprobado [BR-designaciones-019].
        await SellosPrueba.Sellador(db).VerificarEstadoPrevioAsync(aprobado, ct);
        Assert.Single(aprobado.Historial, h => h.Accion == AccionesHistorial.Sincronizar);
        await Assert.ThrowsAsync<ExcepcionAplicacion>(() =>
            ConciliacionPrueba.SincronizarAsync(AltaId, identity, db, ct));
    }

    [Fact]
    public async Task Rechazado_en_la_conciliacion_se_corrige_con_permiso_y_vuelve_a_conciliarse()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        var alta = await EnLoteAsync(db, AltaId, _ => { }, ct);
        var servicio = Servicio(identity, db,
            ArsDocendi.Shared.Auth.Permisos.DesignacionesConciliar, ArsDocendi.Shared.Auth.Permisos.DesignacionesEditar);

        var rechazado = await servicio.AplicarAccionAsync(
            AltaId, new AccionPedido.NoAprobar("La rectificación pidió 8 horas"), ct);
        Assert.Equal(EstadosPedido.NoAprobado, rechazado.Estado);

        db.ChangeTracker.Clear();
        var corregido = await Servicio(identity, db, ArsDocendi.Shared.Auth.Permisos.DesignacionesEditar).EditarAsync(
            AltaId,
            new GuardarPedidoDto(
                alta.PeriodoId, alta.PersonaId, Materia, Carrera, Novedades.Alta,
                alta.CargoSolicitadoId, alta.DedicacionSolicitadaId, 8, 3, 2, "Nueva comisión",
                null, null,
                [
                    new GuardarAdjuntoPedidoDto(TiposAdjunto.Cv, "cv.pdf"),
                    new GuardarAdjuntoPedidoDto(TiposAdjunto.DniFrente, "dni-frente.pdf"),
                    new GuardarAdjuntoPedidoDto(TiposAdjunto.DniDorso, "dni-dorso.pdf"),
                ],
                rechazado.Version),
            ct);

        Assert.Equal(EstadosPedido.EnLote, corregido.Estado);
        Assert.Equal(8, corregido.Horas);
        db.ChangeTracker.Clear();
        var historial = await db.PedidoHistorial.AsNoTracking()
            .Where(h => h.PedidoId == AltaId).OrderBy(h => h.CreadoEn).Select(h => h.Accion).ToListAsync(ct);
        Assert.Equal([AccionesHistorial.NoAprobar, AccionesHistorial.Editar], historial[^2..]);
    }

    [Fact]
    public async Task Editar_en_la_conciliacion_conserva_los_adjuntos_que_subio_el_jefe()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        var jefe = Guid.Parse("a0000000-0000-4000-8000-000000000002");
        var cv = Guid.Parse("e0000000-0000-4000-8000-000000000011");
        var frente = Guid.Parse("e0000000-0000-4000-8000-000000000012");
        var dorso = Guid.Parse("e0000000-0000-4000-8000-000000000013");
        var ajeno = Guid.Parse("e0000000-0000-4000-8000-000000000014");
        var almacenamiento = new PedidosApiTests.AlmacenamientoFalso(jefe, [
            PedidosApiTests.Archivo(cv, PropositosArchivo.Cv, "cv.pdf"),
            PedidosApiTests.Archivo(frente, PropositosArchivo.DniFrente, "frente.png"),
            PedidosApiTests.Archivo(dorso, PropositosArchivo.DniDorso, "dorso.png"),
            PedidosApiTests.Archivo(ajeno, PropositosArchivo.Cv, "otro-cv.pdf"),
        ]);
        // Los adjuntos sembrados del Alta pasan a referir archivos que subió el Jefe.
        var archivoPorTipo = new Dictionary<string, Guid>
        {
            [TiposAdjunto.Cv] = cv, [TiposAdjunto.DniFrente] = frente, [TiposAdjunto.DniDorso] = dorso,
        };
        var alta = await EnLoteAsync(db, AltaId, pedido =>
        {
            Assert.Equal(archivoPorTipo.Keys.Order(), pedido.Adjuntos.Select(a => a.Tipo).Order());
            foreach (var adjunto in pedido.Adjuntos) adjunto.ArchivoId = archivoPorTipo[adjunto.Tipo];
        }, ct);
        GuardarPedidoDto Edicion(Guid cvAdjunto, uint version) => new(
            alta.PeriodoId, alta.PersonaId, Materia, Carrera, Novedades.Alta,
            alta.CargoSolicitadoId, alta.DedicacionSolicitadaId, 8, 3, 2, null, null, null,
            [
                new GuardarAdjuntoPedidoDto(TiposAdjunto.Cv, cvAdjunto),
                new GuardarAdjuntoPedidoDto(TiposAdjunto.DniFrente, frente),
                new GuardarAdjuntoPedidoDto(TiposAdjunto.DniDorso, dorso),
            ],
            version);

        // Secretaría edita con los archivos del Jefe: no son suyos, pero ya eran del pedido.
        var editado = await Servicio(identity, db, almacenamiento, ArsDocendi.Shared.Auth.Permisos.DesignacionesEditar)
            .EditarAsync(AltaId, Edicion(cv, alta.Version), ct);
        Assert.Equal(8, editado.Horas);
        Assert.Contains(editado.Adjuntos, a => a.ArchivoId == cv);

        // Un archivo ajeno que el pedido no tenía sigue sin poder adjuntarse.
        db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ErrorDominioPedido>(() =>
            Servicio(identity, db, almacenamiento, ArsDocendi.Shared.Auth.Permisos.DesignacionesEditar)
                .EditarAsync(AltaId, Edicion(ajeno, editado.Version), ct));
    }

    [Fact]
    public async Task Sin_designaciones_editar_un_pedido_en_lote_no_se_edita()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        var alta = await EnLoteAsync(db, AltaId, _ => { }, ct);

        await Assert.ThrowsAsync<ErrorDominioPedido>(() =>
            Servicio(identity, db, ArsDocendi.Shared.Auth.Permisos.DesignacionesConciliar).EditarAsync(
                AltaId,
                new GuardarPedidoDto(
                    alta.PeriodoId, alta.PersonaId, Materia, Carrera, Novedades.Alta,
                    alta.CargoSolicitadoId, alta.DedicacionSolicitadaId, 8, 3, 2, "Nueva comisión",
                    null, null, [], alta.Version),
                ct));
        Assert.Equal(alta.Horas, (await db.Pedidos.AsNoTracking().SingleAsync(p => p.Id == AltaId, ct)).Horas);
    }

    /// <summary>La API de pedidos actuando como Secretaría con los permisos indicados.</summary>
    private static IServicioPedidosApi Servicio(
        IdentityDbContext identityDb, DesignacionesDbContext db, params string[] permisos) =>
        Servicio(identityDb, db, null, permisos);

    private static IServicioPedidosApi Servicio(
        IdentityDbContext identityDb, DesignacionesDbContext db,
        IAlmacenamientoArchivos? almacenamiento, params string[] permisos)
    {
        var identity = new ConsultasIdentity(identityDb);
        var resolutor = new ResolutorActor(
            new ConciliacionPrueba.UsuarioConPermisos(ConciliacionPrueba.Secretaria, permisos), identity);
        var pedidos = new RepositorioPedidos(db);
        var core = new ServicioPedidos(
            pedidos, new RepositorioDesignaciones(db), resolutor, identity, new UnidadDeTrabajo(db),
            SellosPrueba.Sellador(db), NullLogger<ServicioPedidos>.Instance, almacenamiento: almacenamiento);
        return new ServicioPedidosApi(
            core, pedidos, resolutor, identity, new RepositorioIdempotencia(db), new UnidadDeTrabajo(db),
            almacenamiento);
    }

    /// <summary>Lleva el pedido a en lote por EF y lo sella como si hubiera llegado por el circuito.</summary>
    private static async Task<Pedido> EnLoteAsync(
        DesignacionesDbContext db, Guid pedidoId, Action<Pedido> ajustar, CancellationToken ct)
    {
        // Con adjuntos: el sello fotografía el pedido completo [BR-designaciones-019].
        var pedido = await db.Pedidos.Include(p => p.Adjuntos).SingleAsync(p => p.Id == pedidoId, ct);
        ajustar(pedido);
        pedido.Estado = EstadosPedido.EnLote;
        pedido.EtapaRetorno = null;
        pedido.PropietarioActual = null;
        await db.SaveChangesAsync(ct);
        await SellosPrueba.SellarEtapaForzadaAsync(db, pedido, ct);
        db.ChangeTracker.Clear();
        return await db.Pedidos.AsNoTracking().SingleAsync(p => p.Id == pedidoId, ct);
    }
}
