using System.Data.Common;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Modules.Designaciones.Api;
using Modules.Designaciones.Domain;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Designaciones;

public sealed partial class VigenciasPedidosTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Crear_y_editar_revalidan_actor_dentro_de_transaccion_tras_prevalidacion(bool crear)
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var pedido = await db.Pedidos.SingleAsync(p => p.Id == PedidoId, ct);
        var pausa = new PausaCatalogoDestinos();
        var opciones = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(Cadena).AddInterceptors(pausa).Options;
        await using var identity = new IdentityDbContext(opciones);
        var persona = pedido.PersonaId;
        if (crear)
        {
            var usadas = await db.Pedidos.Select(p => p.PersonaId).ToArrayAsync(ct);
            persona = await identity.Personas.Where(p => !usadas.Contains(p.Id)).Select(p => p.Id).FirstAsync(ct);
        }
        var datos = new GuardarPedidoDto(pedido.PeriodoId, persona, pedido.MateriaId, pedido.CarreraId,
            crear ? Novedades.Alta : pedido.Novedad, pedido.CargoSolicitadoId,
            pedido.DedicacionSolicitadaId, 17, 4, 2, "Justificación sintética", null, null,
            crear ? [new(TiposAdjunto.Cv, "cv.pdf"), new(TiposAdjunto.DniFrente, "frente.png"), new(TiposAdjunto.DniDorso, "dorso.png")] : [],
            crear ? null : pedido.Version);
        var total = await db.Pedidos.CountAsync(ct);
        var eventos = await db.PedidoHistorial.CountAsync(ct);
        var api = CrearApi(Jefe, identity, db);
        var operacion = crear ? api.CrearAsync(datos, ct) : api.EditarAsync(PedidoId, datos, ct);
        try
        {
            await pausa.Alcanzada.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            await using var conexion = await AbrirConexionAsync();
            await using var comando = new NpgsqlCommand("UPDATE identity.users SET is_active=false WHERE id=@id", conexion);
            comando.Parameters.AddWithValue("id", Jefe);
            await comando.ExecuteNonQueryAsync(ct);
        }
        finally { pausa.Continuar.TrySetResult(); }
        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => operacion);
        Assert.Equal("identity-destination-conflict", error.Codigo);
        Assert.Equal(TipoErrorAplicacion.Conflicto, error.Tipo);
        db.ChangeTracker.Clear();
        Assert.Equal(total, await db.Pedidos.CountAsync(ct));
        Assert.Equal(eventos, await db.PedidoHistorial.CountAsync(ct));
        Assert.Equal(pedido.Horas, (await db.Pedidos.SingleAsync(p => p.Id == PedidoId, ct)).Horas);
    }

    private sealed class PausaCatalogoDestinos : DbCommandInterceptor
    {
        public TaskCompletionSource Alcanzada { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continuar { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand comando,
            CommandExecutedEventData datos, DbDataReader resultado, CancellationToken ct = default)
        {
            if (comando.CommandText.Contains("FROM identity.materias_plan", StringComparison.Ordinal)
                && Alcanzada.TrySetResult()) await Continuar.Task.WaitAsync(ct);
            return resultado;
        }
    }
}
