using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Modules.Designaciones.Domain;

namespace ArsDocendi.IntegrationTests.Designaciones;

public sealed partial class VigenciasPedidosTests
{
    [Fact]
    public async Task Version_vigencia_obsoleta_no_pisa_horas_y_origen_confirmados()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var viejo = PostgresFixture.CrearDesignaciones(Cadena);
        var pedido = await viejo.Pedidos.SingleAsync(p => p.Id == PedidoId, ct);
        var leida = await viejo.Designaciones.SingleAsync(d => d.PersonaId == pedido.PersonaId && d.VigenteHasta == null, ct);
        await using (var nuevo = PostgresFixture.CrearDesignaciones(Cadena))
        {
            var actual = await nuevo.Designaciones.SingleAsync(d => d.Id == leida.Id, ct);
            actual.Horas = 32;
            actual.OrigenPedidoId = PedidoId;
            await nuevo.SaveChangesAsync(ct);
        }
        leida.Horas = 11;
        leida.OrigenPedidoId = null;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => viejo.SaveChangesAsync(ct));
        viejo.ChangeTracker.Clear();
        var conservada = await viejo.Designaciones.SingleAsync(d => d.Id == leida.Id, ct);
        Assert.Equal(32, conservada.Horas);
        Assert.Equal(PedidoId, conservada.OrigenPedidoId);
    }
}
