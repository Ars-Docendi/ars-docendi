using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Repositories;

namespace ArsDocendi.IntegrationTests.Designaciones;

public sealed partial class VigenciasPedidosTests
{
    [Fact]
    public async Task Listado_no_materializa_historial_y_detalle_lo_conserva_ordenado()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var pedido = await db.Pedidos.SingleAsync(p => p.Id == PedidoId, ct);
        for (var i = 0; i < 200; i++) db.PedidoHistorial.Add(new PedidoHistorial
        {
            Id = Guid.NewGuid(), PedidoId = PedidoId, Accion = AccionesHistorial.Editar,
            RolId = Guid.Parse("a1000000-0000-4000-8000-000000000002"),
            ActorId = Jefe, Etapa = EstadosPedido.Borrador, CreadoEn = new DateTimeOffset(2026, 10, 13, 10, 0, 0, TimeSpan.Zero).AddSeconds(i),
        });
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        var repositorio = new RepositorioPedidos(db);
        var lista = await repositorio.ListarDelPeriodoAsync(pedido.PeriodoId, ct);
        Assert.Equal(8, lista.Count);
        Assert.All(lista, p => Assert.Empty(p.Historial));
        Assert.All(lista, p => Assert.Empty(p.Adjuntos));
        var detalle = await repositorio.ObtenerPorIdAsync(PedidoId, ct);
        Assert.NotNull(detalle);
        Assert.Equal(201, detalle.Historial.Count);
        Assert.Equal(detalle.Historial.OrderBy(h => h.CreadoEn).ThenBy(h => h.Id).Select(h => h.Id), detalle.Historial.Select(h => h.Id));
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        var api = CrearApi(Jefe, identity, db);
        var respuesta = await api.ListarAsync(pedido.PeriodoId, ct);
        Assert.Equal(8, respuesta.Count);
        Assert.All(respuesta, p => Assert.InRange(p.Historial.Count, 1, 4));
        var fila = respuesta.Single(p => p.Id == PedidoId);
        var completa = await api.ObtenerAsync(PedidoId, ct);
        Assert.Equal(completa.Historial.First().Id, fila.Historial.First().Id);
        Assert.Equal(completa.Historial.Last().Id, fila.Historial.Last().Id);
        Assert.Equal(completa.Historial.Last().CreadoEn, fila.Historial.Last().CreadoEn);
        Assert.Equal(completa.Historial.Last().ActorNombre, fila.Historial.Last().ActorNombre);
        Assert.All(fila.Historial, h => Assert.Null(h.Comentario));
        Assert.Equal(completa.AccionesPermitidas, fila.AccionesPermitidas);
        Assert.Contains("enviar", fila.AccionesPermitidas);
        Assert.Equal(201, completa.Historial.Count);
    }
}
