using System.Data.Common;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Modules.Designaciones.Contracts.Administracion;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure;
using Modules.Designaciones.Repositories;
using Modules.Designaciones.Services;

namespace ArsDocendi.IntegrationTests.Designaciones;

public sealed partial class VigenciasPedidosTests
{
    [Fact]
    public async Task Relectura_administrativa_final_filtra_persona_en_SQL()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        var captura = new CapturaLecturasDesignaciones();
        await using var db = new DesignacionesDbContext(new DbContextOptionsBuilder<DesignacionesDbContext>()
            .UseNpgsql(Cadena).AddInterceptors(captura).Options);
        var pedido = await db.Pedidos.SingleAsync(p => p.Id == PedidoId, ct);
        var anteriores = await db.Designaciones.Where(d => d.PersonaId == pedido.PersonaId
            && d.VigenteHasta == null).ToListAsync(ct);
        var datos = anteriores.Select(d => new GuardarDesignacionVigenteDto(d.MateriaId, d.CarreraId,
            d.CargoId, d.DedicacionId, d.Horas + 1)).ToArray();
        captura.Consultas.Clear();
        var resultado = await new ServicioAdministracionDesignaciones(new RepositorioDesignaciones(db))
            .ReemplazarVigentesAsync(pedido.PersonaId, datos, ct);
        Assert.Equal(anteriores.Count, resultado.Count);
        Assert.All(resultado, d => Assert.Equal(pedido.PersonaId, d.PersonaId));
        var final = captura.Consultas.Last(q => q.Contains("FROM designaciones.designaciones", StringComparison.Ordinal));
        Assert.Contains("persona_id =", final);
        salida.WriteLine($"Relectura posterior: filas={resultado.Count}; SQL={final}");
    }

    [Fact]
    public async Task Historial_creciente_mantiene_cardinalidad_actor_fechas_y_acciones_del_listado()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        var captura = new CapturaLecturasDesignaciones();
        await using var db = new DesignacionesDbContext(new DbContextOptionsBuilder<DesignacionesDbContext>()
            .UseNpgsql(Cadena).AddInterceptors(captura).Options);
        var pedido = await db.Pedidos.SingleAsync(p => p.Id == PedidoId, ct);
        var fecha = new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
        db.PedidoHistorial.Add(new PedidoHistorial { Id = Guid.NewGuid(), PedidoId = PedidoId,
            Accion = AccionesHistorial.Enviar, RolId = Guid.Parse("a1000000-0000-4000-8000-000000000002"),
            ActorId = Jefe, Etapa = EstadosPedido.EnRevisionCoordinador, CreadoEn = fecha });
        db.PedidoHistorial.Add(new PedidoHistorial { Id = Guid.NewGuid(), PedidoId = PedidoId,
            Accion = AccionesHistorial.Editar, RolId = Guid.Parse("a1000000-0000-4000-8000-000000000002"),
            ActorId = Jefe, Etapa = EstadosPedido.Borrador, CreadoEn = fecha.AddSeconds(1) });
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        var api = CrearApi(Jefe, identity, db);
        var antes = await api.ListarAsync(pedido.PeriodoId, ct);
        for (var i = 0; i < 2000; i++) db.PedidoHistorial.Add(new PedidoHistorial
        {
            Id = Guid.NewGuid(), PedidoId = PedidoId, Accion = AccionesHistorial.Editar,
            RolId = Guid.Parse("a1000000-0000-4000-8000-000000000005"), ActorId = Decanato,
            Etapa = EstadosPedido.Borrador, Comentario = "Texto exclusivo del detalle",
            CreadoEn = fecha.AddSeconds(i + 2),
        });
        await db.SaveChangesAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE identity.users SET is_active=false WHERE id={Decanato}", ct);
        db.ChangeTracker.Clear();
        captura.Consultas.Clear();
        var despues = await api.ListarAsync(pedido.PeriodoId, ct);
        Assert.Equal(antes.Select(p => p.Id), despues.Select(p => p.Id));
        Assert.Equal(antes.Sum(p => p.Historial.Count), despues.Sum(p => p.Historial.Count));
        var fila = despues.Single(p => p.Id == PedidoId);
        Assert.Equal(fecha, fila.Historial.Single(h => h.Accion == AccionesHistorial.Enviar).CreadoEn);
        Assert.Equal(fecha.AddSeconds(2001), fila.Historial.Last().CreadoEn);
        Assert.Equal(Decanato, fila.Historial.Last().ActorId);
        Assert.NotNull(fila.Historial.Last().ActorNombre);
        Assert.Equal(antes.Single(p => p.Id == PedidoId).AccionesPermitidas, fila.AccionesPermitidas);
        Assert.All(despues, p => { Assert.Empty(p.Adjuntos); Assert.InRange(p.Historial.Count, 1, 4); });
        var resumen = Assert.Single(captura.Consultas,
            q => q.Contains("FROM designaciones.pedido_historial", StringComparison.Ordinal));
        Assert.Contains("LIMIT 1", resumen);
        Assert.DoesNotContain("comentario", resumen);
        var detalle = await api.ObtenerAsync(PedidoId, ct);
        Assert.Equal(2003, detalle.Historial.Count);
        Assert.Equal("Texto exclusivo del detalle", detalle.Historial.Last().Comentario);
        salida.WriteLine($"Cardinalidad real: filas={antes.Count}/{despues.Count}; eventos resumidos={antes.Sum(p => p.Historial.Count)}/{despues.Sum(p => p.Historial.Count)}; detalle={detalle.Historial.Count}; consultas resumen=1");
        salida.WriteLine("SQL resumen ejecutado: " + resumen);
    }

    private sealed class CapturaLecturasDesignaciones : DbCommandInterceptor
    {
        public List<string> Consultas { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand comando,
            CommandEventData datos, InterceptionResult<DbDataReader> resultado, CancellationToken ct = default)
        {
            Consultas.Add(comando.CommandText);
            return ValueTask.FromResult(resultado);
        }
    }
}
