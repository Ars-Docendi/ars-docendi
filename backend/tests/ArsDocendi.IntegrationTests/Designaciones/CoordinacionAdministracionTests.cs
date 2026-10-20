using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Modules.Designaciones.Contracts.Administracion;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Repositories;
using Modules.Designaciones.Services;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Designaciones;

public sealed class CoordinacionAdministracionTests(PostgresFixture postgres) : VigenciasPedidosBase(postgres)
{
    [Fact]
    public async Task Administracion_de_identidad_y_circuito_sobre_el_propio_actor_no_invierten_locks()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var circuito = PostgresFixture.CrearDesignaciones(Cadena);
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        var personaId = (await identity.Usuarios.SingleAsync(u => u.Id == Jefe, ct)).PersonaId!.Value;
        var pedido = await circuito.Pedidos.SingleAsync(p => p.Id == PedidoId, ct);
        pedido.PersonaId = personaId;
        var vigente = await circuito.Designaciones.FirstOrDefaultAsync(d => d.PersonaId == personaId
            && d.MateriaId == pedido.MateriaId && d.VigenteHasta == null, ct);
        if (vigente is null)
        {
            vigente = new Designacion { Id = Guid.NewGuid(), PersonaId = personaId, MateriaId = pedido.MateriaId,
                CarreraId = pedido.CarreraId, CargoId = pedido.CargoSolicitadoId!.Value,
                DedicacionId = pedido.DedicacionSolicitadaId, Horas = 10, VigenteDesde = new DateOnly(2026, 1, 1) };
            circuito.Designaciones.Add(vigente);
        }
        await circuito.SaveChangesAsync(ct);
        circuito.ChangeTracker.Clear();
        await using var administrador = PostgresFixture.CrearDesignaciones(Cadena);
        await using var transaccion = await administrador.Database.BeginTransactionAsync(ct);
        // La edición administrativa de la cuenta precede al reemplazo de sus vigencias.
        await administrador.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE identity.users SET display_name=display_name || '_sintetico' WHERE id={Jefe}", ct);
        var operacion = CrearApi(Jefe, identity, circuito).AplicarAccionAsync(PedidoId, new AccionPedido.Enviar(), ct);
        await using var observador = await AbrirConexionAsync();
        var bloqueada = false;
        for (var i = 0; i < 100 && !operacion.IsCompleted; i++)
        {
            await using var consulta = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname=current_database() AND wait_event='transactionid' AND cardinality(pg_blocking_pids(pid)) > 0)", observador);
            if ((bool)(await consulta.ExecuteScalarAsync(ct))!) { bloqueada = true; break; }
            await Task.Delay(20, ct);
        }
        try
        {
            var actuales = await administrador.Designaciones.Where(d => d.PersonaId == personaId && d.VigenteHasta == null).ToListAsync(ct);
            var datos = actuales.Select(d => new GuardarDesignacionVigenteDto(d.MateriaId, d.CarreraId,
                d.CargoId, d.DedicacionId, 32)).ToArray();
            await new ServicioAdministracionDesignaciones(new RepositorioDesignaciones(administrador))
                .ReemplazarVigentesAsync(personaId, datos, ct);
            await transaccion.CommitAsync(ct);
        }
        catch
        {
            await transaccion.RollbackAsync(ct);
            try { await operacion; } catch { /* Observar también la sesión perdedora. */ }
            throw;
        }
        var respuesta = await operacion;
        Assert.True(bloqueada);
        Assert.Equal(32, respuesta.Snapshot!.Horas);
    }

    [Fact]
    public async Task Administracion_obsoleta_tras_aprobacion_no_pisa_horas_origen_ni_eventos()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var circuito = PostgresFixture.CrearDesignaciones(Cadena);
        var pedido = await circuito.Pedidos.SingleAsync(p => p.Id == PedidoId, ct);
        pedido.Estado = EstadosPedido.EnRevisionDecanato;
        pedido.Horas = 16;
        pedido.Snapshot = new SnapshotPedido("Congelado", "Anterior", 10, "Materia", 1, 2);
        await circuito.SaveChangesAsync(ct);
        await using var administrador = PostgresFixture.CrearDesignaciones(Cadena);
        var repositorio = new RepositorioDesignaciones(administrador);
        var actuales = await repositorio.ListarVigentesDePersonaAsync(pedido.PersonaId, ct);
        var datos = actuales.Select(d => new GuardarDesignacionVigenteDto(d.MateriaId, d.CarreraId,
            d.CargoId, d.DedicacionId, 11)).ToArray();
        await using var transaccion = await circuito.Database.BeginTransactionAsync(ct);
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        await CrearApi(Decanato, identity, circuito).AplicarAccionAsync(PedidoId, new AccionPedido.Aceptar(), ct);
        var operacion = new ServicioAdministracionDesignaciones(repositorio)
            .ReemplazarVigentesAsync(pedido.PersonaId, datos, ct);
        await using var observador = await AbrirConexionAsync();
        var bloqueada = false;
        for (var i = 0; i < 100 && !operacion.IsCompleted; i++)
        {
            await using var consulta = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname=current_database() AND wait_event='advisory' AND cardinality(pg_blocking_pids(pid)) > 0)", observador);
            if ((bool)(await consulta.ExecuteScalarAsync(ct))!) { bloqueada = true; break; }
            await Task.Delay(20, ct);
        }
        await transaccion.CommitAsync(ct);
        Assert.True(bloqueada);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => operacion);
        circuito.ChangeTracker.Clear();
        var conservada = await circuito.Designaciones.SingleAsync(d => d.PersonaId == pedido.PersonaId
            && d.MateriaId == pedido.MateriaId && d.VigenteHasta == null, ct);
        Assert.Equal(16, conservada.Horas);
        Assert.Equal(PedidoId, conservada.OrigenPedidoId);
        var persistido = await circuito.Pedidos.Include(p => p.Historial).SingleAsync(p => p.Id == PedidoId, ct);
        Assert.Equal(pedido.Snapshot, persistido.Snapshot);
        Assert.Single(persistido.Historial, h => h.Accion == AccionesHistorial.Aceptar);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Administracion_y_circuito_revalidan_vigencia_bajo_exclusion_compartida(bool aprobar)
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var circuito = PostgresFixture.CrearDesignaciones(Cadena);
        var pedido = await circuito.Pedidos.SingleAsync(p => p.Id == PedidoId, ct);
        if (aprobar)
        {
            pedido.Estado = EstadosPedido.EnRevisionDecanato;
            pedido.Snapshot = new SnapshotPedido("Congelado", "Anterior", 10, "Materia", 1, 2);
            pedido.Horas = 16;
            await circuito.SaveChangesAsync(ct);
        }
        // Estado seguido antes de la exclusión: no debe reutilizarse tras esperar.
        var leida = await circuito.Designaciones.SingleAsync(d => d.PersonaId == pedido.PersonaId
            && d.MateriaId == pedido.MateriaId && d.VigenteHasta == null, ct);
        await using var administrador = PostgresFixture.CrearDesignaciones(Cadena);
        await using var transaccion = await administrador.Database.BeginTransactionAsync(ct);
        var actuales = await administrador.Designaciones.Where(d => d.PersonaId == pedido.PersonaId
            && d.VigenteHasta == null).ToListAsync(ct);
        var datos = actuales.Select(d => new GuardarDesignacionVigenteDto(d.MateriaId, d.CarreraId,
            d.CargoId, d.DedicacionId, d.MateriaId == pedido.MateriaId ? 32 : d.Horas)).ToArray();
        await new ServicioAdministracionDesignaciones(new RepositorioDesignaciones(administrador))
            .ReemplazarVigentesAsync(pedido.PersonaId, datos, ct);
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        var operacion = CrearApi(aprobar ? Decanato : Jefe, identity, circuito).AplicarAccionAsync(PedidoId,
            aprobar ? new AccionPedido.Aceptar() : new AccionPedido.Enviar(), ct);
        var bloqueada = false;
        try
        {
            await using var observador = await AbrirConexionAsync();
            for (var i = 0; i < 100 && !operacion.IsCompleted; i++)
            {
                await using var consulta = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname=current_database() AND wait_event='advisory' AND cardinality(pg_blocking_pids(pid)) > 0)", observador);
                if ((bool)(await consulta.ExecuteScalarAsync(ct))!) { bloqueada = true; break; }
                await Task.Delay(20, ct);
            }
        }
        finally { await transaccion.CommitAsync(ct); }
        var respuesta = await operacion;
        Assert.True(bloqueada, "El circuito confirmó o leyó snapshot sin esperar el guard de administración.");
        Assert.Equal(aprobar ? 10 : 32, respuesta.Snapshot!.Horas);
        circuito.ChangeTracker.Clear();
        var vigente = await circuito.Designaciones.SingleAsync(d => d.PersonaId == pedido.PersonaId
            && d.MateriaId == pedido.MateriaId && d.VigenteHasta == null, ct);
        Assert.Equal(aprobar ? 16 : 32, vigente.Horas);
        Assert.Equal(aprobar ? PedidoId : null, vigente.OrigenPedidoId);
        Assert.Single(respuesta.Historial, h => h.Accion == (aprobar ? AccionesHistorial.Aceptar : AccionesHistorial.Enviar));
    }
}
