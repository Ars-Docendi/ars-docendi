using System.Text.Json;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure;
using Modules.Designaciones.Repositories;
using Modules.Designaciones.Services;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Designaciones;

/// <summary>
/// Datos y helpers compartidos. Cada archivo de tests es su propia clase para que
/// xUnit pueda correrlos en paralelo (paraleliza por clase, no por método).
/// </summary>
public abstract class VigenciasPedidosBase(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "vigencias_pedidos")
{
    protected static readonly Guid PedidoId = Guid.Parse("d5000000-0000-4000-8000-000000000001");
    protected static readonly Guid Jefe = Guid.Parse("a0000000-0000-4000-8000-000000000002");
    protected static readonly Guid Decanato = Guid.Parse("a0000000-0000-4000-8000-000000000005");

    protected async Task SembrarAsync(CancellationToken ct)
    {
        var raiz = new DirectoryInfo(AppContext.BaseDirectory);
        while (raiz is not null && !File.Exists(Path.Combine(raiz.FullName, "AGENTS.md"))) raiz = raiz.Parent;
        var sql = await File.ReadAllTextAsync(Path.Combine(raiz!.FullName,
            "infra/scripts/seed-data/sintetico.sql"), ct);
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = 60 };
        await comando.ExecuteNonQueryAsync(ct);
    }

    protected static IServicioPedidosApi CrearApi(Guid usuario, IdentityDbContext identityDb, DesignacionesDbContext db)
    {
        var identity = new ConsultasIdentity(identityDb);
        var resolutor = new ResolutorActor(new UsuarioActual(usuario), identity);
        var pedidos = new RepositorioPedidos(db);
        var designaciones = new RepositorioDesignaciones(db);
        var unidad = new UnidadDeTrabajo(db);
        var servicio = new ServicioPedidos(pedidos, designaciones, new MaterializadorDesignaciones(designaciones, new RelojFijo()),
            resolutor, identity, unidad, SellosPrueba.Sellador(db), NullLogger<ServicioPedidos>.Instance);
        return new ServicioPedidosApi(servicio, pedidos, resolutor, identity, new RepositorioIdempotencia(db), unidad);
    }

    protected sealed class RelojFijo : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    }

    protected sealed class UsuarioActual(Guid id) : ICurrentUser
    {
        public string UserId => id.ToString();
        public string? Email => "fixture@example.invalid";
        public IReadOnlyList<string> Roles => [];
        public bool IsAuthenticated => true;
    }
}

public sealed class VigenciasPedidosTests(PostgresFixture postgres) : VigenciasPedidosBase(postgres)
{
    [Fact]
    public async Task Cambio_intradía_conserva_id_inicio_snapshot_y_audita_valores_y_origenes()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var pedido = await db.Pedidos.SingleAsync(p => p.Id == PedidoId, ct);
        var anterior = await db.Designaciones.SingleAsync(d => d.PersonaId == pedido.PersonaId
            && d.MateriaId == pedido.MateriaId && d.VigenteHasta == null, ct);
        var fecha = new DateOnly(2026, 10, 13);
        anterior.VigenteDesde = fecha;
        var anteriorCargo = anterior.CargoId;
        var anteriorDedicacion = anterior.DedicacionId;
        var anteriores = (anterior.Horas, anterior.HorasInvestigacion, anterior.HorasExternas);
        var origenAnterior = Guid.Parse("d5000000-0000-4000-8000-000000000006");
        anterior.OrigenPedidoId = origenAnterior;
        pedido.Estado = EstadosPedido.EnRevisionDecanato;
        pedido.CargoSolicitadoId = Guid.Parse("c3000000-0000-4000-8000-000000000002");
        pedido.DedicacionSolicitadaId = Guid.Parse("d6000000-0000-4000-8000-000000000006");
        pedido.Horas = 16;
        pedido.HorasInvestigacion = 5;
        pedido.HorasExternas = 2;
        pedido.Snapshot = new SnapshotPedido("Anterior", "Categoría 1", 8, "Materia", 1, 0);
        await db.SaveChangesAsync(ct);
        var id = anterior.Id;
        var snapshot = pedido.Snapshot;
        db.ChangeTracker.Clear();
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        var api = CrearApi(Decanato, identity, db);
        var clave = Guid.NewGuid();
        await api.AplicarAccionIdempotenteAsync(PedidoId, new AccionPedido.Aceptar(), clave, "aceptar", "", ct);
        await api.AplicarAccionIdempotenteAsync(PedidoId, new AccionPedido.Aceptar(), clave, "aceptar", "", ct);
        db.ChangeTracker.Clear();
        var actual = Assert.Single(await db.Designaciones.Where(d => d.PersonaId == pedido.PersonaId
            && d.MateriaId == pedido.MateriaId && d.VigenteHasta == null).ToListAsync(ct));
        Assert.Equal(id, actual.Id);
        Assert.Equal(fecha, actual.VigenteDesde);
        Assert.Equal(pedido.CargoSolicitadoId, actual.CargoId);
        Assert.Equal(pedido.DedicacionSolicitadaId, actual.DedicacionId);
        Assert.Equal((16, 5, 2), (actual.Horas, actual.HorasInvestigacion, actual.HorasExternas));
        Assert.Equal(PedidoId, actual.OrigenPedidoId);
        var persistido = await db.Pedidos.Include(p => p.Historial).SingleAsync(p => p.Id == PedidoId, ct);
        Assert.Equal(snapshot, persistido.Snapshot);
        var aprobacion = Assert.Single(persistido.Historial, h => h.Accion == AccionesHistorial.Aceptar);
        Assert.NotNull(aprobacion.Comentario);
        const string prefijoCambio = "Cambio intradía: ";
        Assert.StartsWith(prefijoCambio, aprobacion.Comentario);
        using var detalleCambio = JsonDocument.Parse(aprobacion.Comentario[prefijoCambio.Length..]);
        var valoresAnteriores = detalleCambio.RootElement.GetProperty("anterior");
        var valoresNuevos = detalleCambio.RootElement.GetProperty("nuevo");
        Assert.Equal(anteriorCargo, valoresAnteriores.GetProperty("CargoId").GetGuid());
        Assert.Equal(anteriorDedicacion, valoresAnteriores.GetProperty("DedicacionId").GetGuid());
        Assert.Equal(anteriores.Horas, valoresAnteriores.GetProperty("Horas").GetInt32());
        Assert.Equal(anteriores.HorasInvestigacion, valoresAnteriores.GetProperty("HorasInvestigacion").GetInt32());
        Assert.Equal(anteriores.HorasExternas, valoresAnteriores.GetProperty("HorasExternas").GetInt32());
        Assert.Equal(origenAnterior, valoresAnteriores.GetProperty("OrigenPedidoId").GetGuid());
        Assert.Equal(fecha.ToString("yyyy-MM-dd"), valoresAnteriores.GetProperty("VigenteDesde").GetString());
        Assert.Equal(actual.CargoId, valoresNuevos.GetProperty("CargoId").GetGuid());
        Assert.Equal(actual.DedicacionId, valoresNuevos.GetProperty("DedicacionId").GetGuid());
        Assert.Equal(actual.Horas, valoresNuevos.GetProperty("Horas").GetInt32());
        Assert.Equal(actual.HorasInvestigacion, valoresNuevos.GetProperty("HorasInvestigacion").GetInt32());
        Assert.Equal(actual.HorasExternas, valoresNuevos.GetProperty("HorasExternas").GetInt32());
        Assert.Equal(PedidoId, valoresNuevos.GetProperty("OrigenPedidoId").GetGuid());
        Assert.Equal(fecha.ToString("yyyy-MM-dd"), valoresNuevos.GetProperty("VigenteDesde").GetString());
        await using var conexion = await AbrirConexionAsync();
        await using var auditoria = new NpgsqlCommand("""
            SELECT old_row::text, new_row::text FROM audit.change_log
            WHERE schema_name = 'designaciones' AND table_name = 'designaciones'
              AND row_pk = @id AND new_row->>'cargo_id' = @cargo
            ORDER BY id DESC LIMIT 1
            """, conexion);
        auditoria.Parameters.AddWithValue("id", id.ToString());
        auditoria.Parameters.AddWithValue("cargo", actual.CargoId.ToString());
        await using var lector = await auditoria.ExecuteReaderAsync(ct);
        Assert.True(await lector.ReadAsync(ct));
        using var viejo = JsonDocument.Parse(lector.GetString(0));
        using var nuevo = JsonDocument.Parse(lector.GetString(1));
        Assert.Equal(anteriorCargo.ToString(), viejo.RootElement.GetProperty("cargo_id").GetString());
        Assert.Equal(anteriorDedicacion.ToString(), viejo.RootElement.GetProperty("dedicacion_id").GetString());
        Assert.Equal(origenAnterior.ToString(), viejo.RootElement.GetProperty("origen_pedido_id").GetString());
        Assert.Equal(anteriores.Horas, viejo.RootElement.GetProperty("horas").GetInt32());
        Assert.Equal(anteriores.HorasInvestigacion, viejo.RootElement.GetProperty("horas_investigacion").GetInt32());
        Assert.Equal(anteriores.HorasExternas, viejo.RootElement.GetProperty("horas_externas").GetInt32());
        Assert.Equal(actual.CargoId.ToString(), nuevo.RootElement.GetProperty("cargo_id").GetString());
        Assert.Equal(actual.DedicacionId.ToString(), nuevo.RootElement.GetProperty("dedicacion_id").GetString());
        Assert.Equal(actual.Horas, nuevo.RootElement.GetProperty("horas").GetInt32());
        Assert.Equal(actual.HorasInvestigacion, nuevo.RootElement.GetProperty("horas_investigacion").GetInt32());
        Assert.Equal(actual.HorasExternas, nuevo.RootElement.GetProperty("horas_externas").GetInt32());
        Assert.Equal(PedidoId.ToString(), nuevo.RootElement.GetProperty("origen_pedido_id").GetString());
    }
}
