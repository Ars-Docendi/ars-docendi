using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Repositories;
using Modules.Designaciones.Services;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Designaciones;

public sealed class VigenciasPedidosCasosTests(PostgresFixture postgres) : VigenciasPedidosBase(postgres)
{
    [Fact]
    public async Task Cambio_con_inicio_futuro_se_rechaza_sin_eventos_parciales()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var pedido = await db.Pedidos.SingleAsync(p => p.Id == PedidoId, ct);
        var vigente = await db.Designaciones.SingleAsync(d => d.PersonaId == pedido.PersonaId && d.VigenteHasta == null, ct);
        vigente.VigenteDesde = new DateOnly(2026, 10, 14);
        pedido.Estado = EstadosPedido.EnLote;
        await db.SaveChangesAsync(ct);
        await SellosPrueba.SellarEtapaForzadaAsync(db, pedido, ct);
        db.ChangeTracker.Clear();
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        var error = await Assert.ThrowsAsync<ErrorDominioPedido>(() =>
            ConciliacionPrueba.SincronizarAsync(PedidoId, identity, db, ct, new RelojFijo()));
        Assert.Contains("posterior", error.Message);
        db.ChangeTracker.Clear();
        Assert.Equal(EstadosPedido.EnLote, (await db.Pedidos.SingleAsync(p => p.Id == PedidoId, ct)).Estado);
        Assert.Null((await db.Designaciones.SingleAsync(d => d.Id == vigente.Id, ct)).VigenteHasta);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Fallo_en_apertura_o_actualizacion_revierte_pedido_vigencia_y_eventos(bool intradia)
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var pedido = await db.Pedidos.SingleAsync(p => p.Id == PedidoId, ct);
        var vigente = await db.Designaciones.SingleAsync(d => d.PersonaId == pedido.PersonaId && d.VigenteHasta == null, ct);
        vigente.VigenteDesde = new DateOnly(2026, 10, intradia ? 13 : 12);
        pedido.Estado = EstadosPedido.EnLote;
        await db.SaveChangesAsync(ct);
        await SellosPrueba.SellarEtapaForzadaAsync(db, pedido, ct);
        var horas = vigente.Horas;
        var inicio = vigente.VigenteDesde;
        await using var conexion = await AbrirConexionAsync();
        await using (var fallo = new NpgsqlCommand("""
            CREATE FUNCTION designaciones.fallo_ensayo() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW.origen_pedido_id IS NOT NULL THEN RAISE EXCEPTION 'fallo inyectado'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER fallo_ensayo BEFORE INSERT OR UPDATE ON designaciones.designaciones
            FOR EACH ROW EXECUTE FUNCTION designaciones.fallo_ensayo();
            """, conexion)) await fallo.ExecuteNonQueryAsync(ct);
        db.ChangeTracker.Clear();
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            ConciliacionPrueba.SincronizarAsync(PedidoId, identity, db, ct, new RelojFijo()));
        db.ChangeTracker.Clear();
        var actual = await db.Designaciones.SingleAsync(d => d.Id == vigente.Id, ct);
        Assert.Equal((inicio, horas, (DateOnly?)null), (actual.VigenteDesde, actual.Horas, actual.VigenteHasta));
        var pedidoActual = await db.Pedidos.SingleAsync(p => p.Id == PedidoId, ct);
        Assert.Equal(EstadosPedido.EnLote, pedidoActual.Estado);
        Assert.Null(pedidoActual.MaterializadoEn);
        Assert.False(await db.PedidoHistorial.AnyAsync(h => h.PedidoId == PedidoId && h.Accion == AccionesHistorial.Sincronizar, ct));
    }

    [Fact]
    public async Task Cambio_anterior_cierra_y_abre_sin_solapamiento_y_baja_intradía_cierra_manana()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var pedido = await db.Pedidos.SingleAsync(p => p.Id == PedidoId, ct);
        var anterior = await db.Designaciones.SingleAsync(d => d.PersonaId == pedido.PersonaId && d.VigenteHasta == null, ct);
        var materializador = new MaterializadorDesignaciones(new RepositorioDesignaciones(db), new RelojFijo());
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await materializador.MaterializarAsync(pedido, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        Assert.Equal(new DateOnly(2026, 10, 13), anterior.VigenteHasta);
        var nueva = await db.Designaciones.SingleAsync(d => d.PersonaId == pedido.PersonaId && d.VigenteHasta == null, ct);
        Assert.NotEqual(anterior.Id, nueva.Id);
        Assert.Equal(new DateOnly(2026, 10, 13), nueva.VigenteDesde);
        Assert.Equal((pedido.Horas, pedido.HorasInvestigacion, pedido.HorasExternas), ((int?)nueva.Horas, nueva.HorasInvestigacion, nueva.HorasExternas));
        pedido.Novedad = Novedades.Baja;
        await materializador.MaterializarAsync(pedido, ct);
        await db.SaveChangesAsync(ct);
        Assert.Equal(new DateOnly(2026, 10, 14), nueva.VigenteHasta);
    }
}
