using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Modules.Designaciones.Domain;

namespace Modules.Designaciones.Infrastructure;

/// <summary>
/// Contexto del schema <c>designaciones</c>.
/// <para>
/// Igual que <c>IdentityDbContext</c>, todas las entidades van con
/// <c>ExcludeFromMigrations()</c>: el DDL es el SQL versionado bajo
/// <c>database/designaciones/</c>, que la migración ejecuta tal cual. El modelo acá
/// describe el schema para consultarlo, nunca lo genera — así EF no puede divergir
/// del SQL ni intentar recrear índices parciales o constraints EXCLUDE que no sabe
/// expresar.
/// </para>
/// </summary>
public sealed class DesignacionesDbContext(DbContextOptions<DesignacionesDbContext> options) : DbContext(options)
{
    public const string Schema = "designaciones";

    public DbSet<Cargo> Cargos => Set<Cargo>();
    public DbSet<Periodo> Periodos => Set<Periodo>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<PedidoAdjunto> PedidoAdjuntos => Set<PedidoAdjunto>();
    public DbSet<PedidoHistorial> PedidoHistorial => Set<PedidoHistorial>();
    public DbSet<Designacion> Designaciones => Set<Designacion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Cargo>(e =>
        {
            e.ToTable("cargos", Schema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Codigo).HasColumnName("codigo");
            e.Property(x => x.Nombre).HasColumnName("nombre");
            e.Property(x => x.Abreviatura).HasColumnName("abreviatura");
            e.Property(x => x.Orden).HasColumnName("orden");
            e.Property(x => x.Activo).HasColumnName("activo");
            e.Property(x => x.CreadoEn).HasColumnName("created_at");
            e.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<Periodo>(e =>
        {
            e.ToTable("periodos", Schema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Nombre).HasColumnName("nombre");
            e.Property(x => x.CargaDesde).HasColumnName("carga_desde");
            e.Property(x => x.CargaHasta).HasColumnName("carga_hasta");
            e.Property(x => x.ImpactoDesde).HasColumnName("impacto_desde");
            e.Property(x => x.ImpactoHasta).HasColumnName("impacto_hasta");
            e.Property(x => x.Activo).HasColumnName("activo");
            e.Property(x => x.CreadoEn).HasColumnName("created_at");
        });

        modelBuilder.Entity<Pedido>(e =>
        {
            e.ToTable("pedidos", Schema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Numero).HasColumnName("numero");
            e.Property(x => x.PeriodoId).HasColumnName("periodo_id");
            e.Property(x => x.PersonaId).HasColumnName("persona_id");
            e.Property(x => x.MateriaId).HasColumnName("materia_id");
            e.Property(x => x.Novedad).HasColumnName("novedad");
            e.Property(x => x.Estado).HasColumnName("estado");
            e.Property(x => x.Prioritario).HasColumnName("prioritario");
            e.Property(x => x.CargoSolicitadoId).HasColumnName("cargo_solicitado_id");
            e.Property(x => x.DedicacionSolicitada).HasColumnName("dedicacion_solicitada");
            e.Property(x => x.Horas).HasColumnName("horas");
            e.Property(x => x.HorasInvestigacion).HasColumnName("horas_investigacion");
            e.Property(x => x.HorasExternas).HasColumnName("horas_externas");
            e.Property(x => x.Justificacion).HasColumnName("justificacion");
            e.Property(x => x.TipoBaja).HasColumnName("tipo_baja");
            e.Property(x => x.TipoBajaDetalle).HasColumnName("tipo_baja_detalle");
            e.Property(x => x.EtapaRetorno).HasColumnName("etapa_retorno");
            e.Property(x => x.PropietarioActual).HasColumnName("propietario_actual");
            e.Property(x => x.CreadoEn).HasColumnName("created_at");
            e.Ignore(x => x.EsTerminal);

            // Documento congelado: se escribe entero al enviar y no se consulta por
            // campo, así que se serializa a jsonb en vez de explotarse en columnas.
            e.Property(x => x.Snapshot)
             .HasColumnName("snapshot")
             .HasColumnType("jsonb")
             .HasConversion(
                 v => v == null ? null : JsonSerializer.Serialize(v, OpcionesJson),
                 v => v == null ? null : JsonSerializer.Deserialize<SnapshotPedido>(v, OpcionesJson));

            e.HasOne(x => x.Periodo).WithMany().HasForeignKey(x => x.PeriodoId);
            e.HasOne(x => x.CargoSolicitado).WithMany().HasForeignKey(x => x.CargoSolicitadoId);
            e.HasMany(x => x.Adjuntos).WithOne(a => a.Pedido!).HasForeignKey(a => a.PedidoId);
            e.HasMany(x => x.Historial).WithOne(h => h.Pedido!).HasForeignKey(h => h.PedidoId);

            e.HasIndex(x => x.Numero).IsUnique();
        });

        modelBuilder.Entity<PedidoAdjunto>(e =>
        {
            e.ToTable("pedido_adjuntos", Schema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PedidoId).HasColumnName("pedido_id");
            e.Property(x => x.Tipo).HasColumnName("tipo");
            e.Property(x => x.Nombre).HasColumnName("nombre");
            e.Property(x => x.Uri).HasColumnName("uri");
            e.Property(x => x.CreadoEn).HasColumnName("created_at");
        });

        modelBuilder.Entity<PedidoHistorial>(e =>
        {
            e.ToTable("pedido_historial", Schema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PedidoId).HasColumnName("pedido_id");
            e.Property(x => x.Accion).HasColumnName("accion");
            e.Property(x => x.RolId).HasColumnName("rol_id");
            e.Property(x => x.ActorId).HasColumnName("actor_id");
            e.Property(x => x.Etapa).HasColumnName("etapa");
            e.Property(x => x.Comentario).HasColumnName("comentario");
            e.Property(x => x.CreadoEn).HasColumnName("created_at");
        });

        modelBuilder.Entity<Designacion>(e =>
        {
            e.ToTable("designaciones", Schema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PersonaId).HasColumnName("persona_id");
            e.Property(x => x.MateriaId).HasColumnName("materia_id");
            e.Property(x => x.CargoId).HasColumnName("cargo_id");
            e.Property(x => x.Dedicacion).HasColumnName("dedicacion");
            e.Property(x => x.Horas).HasColumnName("horas");
            e.Property(x => x.VigenteDesde).HasColumnName("vigente_desde");
            e.Property(x => x.VigenteHasta).HasColumnName("vigente_hasta");
            e.Property(x => x.OrigenPedidoId).HasColumnName("origen_pedido_id");
            e.Property(x => x.CreadoEn).HasColumnName("created_at");
            e.Ignore(x => x.EstaVigente);
            e.HasOne(x => x.Cargo).WithMany().HasForeignKey(x => x.CargoId);
        });

        base.OnModelCreating(modelBuilder);
    }

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);
}
