using System.Data.Common;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Aplicacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Modules.Designaciones.Contracts.Administracion;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure;
using Modules.Designaciones.Repositories;
using Modules.Designaciones.Services;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Designaciones;

public sealed partial class VigenciasPedidosTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Nueva_vigencia_rechaza_persona_inexistente_en_transaccion_local_o_ambiente(bool ambiente)
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var modelo = await db.Designaciones.FirstAsync(ct);
        var datos = new[] { new GuardarDesignacionVigenteDto(modelo.MateriaId, modelo.CarreraId,
            modelo.CargoId, modelo.DedicacionId, 12) };
        using var alcance = ambiente ? new System.Transactions.TransactionScope(
            System.Transactions.TransactionScopeAsyncFlowOption.Enabled) : null;
        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() =>
            new ServicioAdministracionDesignaciones(new RepositorioDesignaciones(db))
                .ReemplazarVigentesAsync(Guid.NewGuid(), datos, ct));
        Assert.Equal(TipoErrorAplicacion.Conflicto, error.Tipo);
        Assert.Equal("identity-destination-conflict", error.Codigo);
    }

    [Fact]
    public async Task Actor_desactivado_tras_resolver_no_confirma_referencia_ni_snapshot_ni_evento()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        var pausa = new PausaConsultaPedidoDestino();
        await using var circuito = new DesignacionesDbContext(new DbContextOptionsBuilder<DesignacionesDbContext>()
            .UseNpgsql(Cadena).AddInterceptors(pausa).Options);
        var cantidad = await circuito.PedidoHistorial.CountAsync(h => h.PedidoId == PedidoId, ct);
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        pausa.Activada = true;
        var operacion = CrearApi(Jefe, identity, circuito).AplicarAccionAsync(PedidoId, new AccionPedido.Enviar(), ct);
        try
        {
            // Lectura real del pedido posterior a resolver actor, anterior al guard.
            await pausa.Alcanzada.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            await using var observador = await AbrirConexionAsync();
            await using var desactivar = new NpgsqlCommand("UPDATE identity.users SET is_active=false WHERE id=@id", observador);
            desactivar.Parameters.AddWithValue("id", Jefe);
            await desactivar.ExecuteNonQueryAsync(ct);
        }
        finally { pausa.Continuar.TrySetResult(); }
        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => operacion);
        Assert.Equal(TipoErrorAplicacion.Conflicto, error.Tipo);
        Assert.Equal("identity-destination-conflict", error.Codigo);
        circuito.ChangeTracker.Clear();
        var conservado = await circuito.Pedidos.SingleAsync(p => p.Id == PedidoId, ct);
        Assert.Equal(EstadosPedido.Borrador, conservado.Estado);
        Assert.Null(conservado.Snapshot);
        Assert.Equal(cantidad, await circuito.PedidoHistorial.CountAsync(h => h.PedidoId == PedidoId, ct));
    }

    private sealed class PausaConsultaPedidoDestino : DbCommandInterceptor
    {
        public bool Activada { get; set; }
        public TaskCompletionSource Alcanzada { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continuar { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand comando,
            CommandExecutedEventData datos, DbDataReader resultado, CancellationToken ct = default)
        {
            if (Activada && comando.CommandText.Contains("FROM designaciones.pedidos", StringComparison.Ordinal)
                && Alcanzada.TrySetResult()) await Continuar.Task.WaitAsync(ct);
            return resultado;
        }
    }
}
