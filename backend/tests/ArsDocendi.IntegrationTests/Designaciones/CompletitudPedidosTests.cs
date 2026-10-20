using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Aplicacion;
using Microsoft.EntityFrameworkCore;
using Modules.Designaciones.Domain;

namespace ArsDocendi.IntegrationTests.Designaciones;

public sealed class CompletitudPedidosTests(PostgresFixture postgres) : VigenciasPedidosBase(postgres)
{
    [Theory]
    [InlineData("enviar", "cargoSolicitadoId")]
    [InlineData("enviar", "dedicacionSolicitadaId")]
    [InlineData("enviar", "horas")]
    [InlineData("enviar", "horasCero")]
    [InlineData("reenviar", "dedicacionSolicitadaId")]
    [InlineData("reenviar", "horas")]
    [InlineData("reenviar", "horasCero")]
    [InlineData("aceptar", "cargoSolicitadoId")]
    [InlineData("aceptar", "dedicacionSolicitadaId")]
    [InlineData("aceptar", "horas")]
    [InlineData("aceptar", "horasCero")]
    public async Task Circuito_rechaza_incompleto_por_campo_sin_snapshot_ni_evento(string accion, string campo)
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        // Simula datos legados ajenos al protocolo sobre una base exclusivamente desechable.
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE designaciones.pedidos DROP CONSTRAINT IF EXISTS pedidos_completitud_circuito", ct);
        var pedido = await db.Pedidos.SingleAsync(p => p.Id == PedidoId, ct);
        var estado = accion switch { "aceptar" => EstadosPedido.EnRevisionDecanato, "reenviar" => EstadosPedido.Devuelto, _ => EstadosPedido.Borrador };
        var snapshot = pedido.Snapshot;
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE designaciones.pedidos DISABLE TRIGGER validar_dedicacion_pedido", ct);
        try
        {
            // La fila inválida representa datos anteriores al trigger. La desactivación
            // es local a esta base desechable y permite probar la defensa de aplicación.
            pedido.Estado = estado;
            pedido.PropietarioActual = accion == "reenviar" ? RolesCircuito.JefeCatedra : null;
            pedido.EtapaRetorno = accion == "reenviar" ? EstadosPedido.EnRevisionCoordinador : null;
            switch (campo)
            {
                case "horas": pedido.Horas = null; break;
                case "horasCero": pedido.Horas = 0; break;
                case "dedicacionSolicitadaId": pedido.DedicacionSolicitadaId = null; break;
                default: pedido.CargoSolicitadoId = null; break;
            }
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE designaciones.pedidos ENABLE TRIGGER validar_dedicacion_pedido", ct);
        }
        db.ChangeTracker.Clear();
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        AccionPedido comando = accion switch { "aceptar" => new AccionPedido.Aceptar(), "reenviar" => new AccionPedido.Reenviar(), _ => new AccionPedido.Enviar() };
        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => CrearApi(accion == "aceptar" ? Decanato : Jefe, identity, db)
            .AplicarAccionAsync(PedidoId, comando, ct));
        Assert.Equal(TipoErrorAplicacion.Validacion, error.Tipo);
        Assert.Contains(campo == "horasCero" ? "horas" : campo, error.Errores!.Keys);
        db.ChangeTracker.Clear();
        var actual = await db.Pedidos.SingleAsync(p => p.Id == PedidoId, ct);
        Assert.Equal(estado, actual.Estado);
        Assert.Equal(snapshot, actual.Snapshot);
        Assert.False(await db.PedidoHistorial.AnyAsync(h => h.PedidoId == PedidoId &&
            (h.Accion == AccionesHistorial.Aceptar || h.Accion == AccionesHistorial.Enviar || h.Accion == AccionesHistorial.Reenviar), ct));
    }
}
