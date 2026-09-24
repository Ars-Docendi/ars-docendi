using Microsoft.EntityFrameworkCore;
using Modules.Tareas.Domain;

namespace Modules.Tareas.Infrastructure;

/// <summary>
/// Todas las entidades se mapean con <c>ExcludeFromMigrations()</c>: el DDL de
/// <c>database/tareas/</c> es la fuente autorizada del schema (CHECKs, secuencias y
/// <c>audit.attach</c> que EF no genera). Los ids los asigna el servicio
/// (<c>ValueGeneratedNever</c>) para que EF trate las entidades nuevas como <c>Added</c>.
/// </summary>
public sealed class TareasDbContext(DbContextOptions<TareasDbContext> options) : DbContext(options)
{
    public const string Schema = "tareas";

    public DbSet<Tarea> Tareas => Set<Tarea>();
    public DbSet<Proyecto> Proyectos => Set<Proyecto>();
    public DbSet<RelacionTarea> Relaciones => Set<RelacionTarea>();
    public DbSet<ComentarioTarea> Comentarios => Set<ComentarioTarea>();
    public DbSet<EventoTarea> Historial => Set<EventoTarea>();
    public DbSet<EstadoProyectoCatalogo> EstadosProyecto => Set<EstadoProyectoCatalogo>();
    public DbSet<EstadoTareaCatalogo> EstadosTarea => Set<EstadoTareaCatalogo>();
    public DbSet<PrioridadCatalogo> Prioridades => Set<PrioridadCatalogo>();
    public DbSet<TipoTareaCatalogo> Tipos => Set<TipoTareaCatalogo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Proyecto>(e =>
        {
            e.ToTable("proyectos", Schema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.Numero).HasColumnName("numero")
                .HasDefaultValueSql("nextval('tareas.proyectos_numero_seq')").ValueGeneratedOnAdd();
            e.Property(x => x.Nombre).HasColumnName("nombre");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.FechaInicio).HasColumnName("fecha_inicio");
            e.Property(x => x.FechaFin).HasColumnName("fecha_fin");
            e.Property(x => x.Estado).HasColumnName("estado");
            e.Property(x => x.ResponsableId).HasColumnName("responsable_id");
            e.Property(x => x.CreadoEn).HasColumnName("created_at");
        });

        modelBuilder.Entity<Tarea>(e =>
        {
            e.ToTable("tareas", Schema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.Numero).HasColumnName("numero")
                .HasDefaultValueSql("nextval('tareas.tareas_numero_seq')").ValueGeneratedOnAdd();
            e.Property(x => x.Titulo).HasColumnName("titulo");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.FechaInicio).HasColumnName("fecha_inicio");
            e.Property(x => x.FechaFin).HasColumnName("fecha_fin");
            e.Property(x => x.Prioridad).HasColumnName("prioridad");
            e.Property(x => x.Tipo).HasColumnName("tipo");
            e.Property(x => x.Estado).HasColumnName("estado");
            e.Property(x => x.PorcentajeAvance).HasColumnName("porcentaje_avance");
            e.Property(x => x.Solucion).HasColumnName("solucion");
            e.Property(x => x.ResponsableId).HasColumnName("responsable_id");
            e.Property(x => x.CreadoPorId).HasColumnName("creado_por_id");
            e.Property(x => x.ProyectoId).HasColumnName("proyecto_id");
            e.Property(x => x.TareaPadreId).HasColumnName("tarea_padre_id");
            e.Property(x => x.CreadoEn).HasColumnName("created_at");
            e.HasMany(x => x.Comentarios).WithOne().HasForeignKey(x => x.TareaId);
            e.HasMany(x => x.Historial).WithOne().HasForeignKey(x => x.TareaId);
        });

        modelBuilder.Entity<RelacionTarea>(e =>
        {
            e.ToTable("tarea_relaciones", Schema, t => t.ExcludeFromMigrations());
            e.HasKey(x => new { x.TareaId, x.RelacionadaId });
            e.Property(x => x.TareaId).HasColumnName("tarea_id");
            e.Property(x => x.RelacionadaId).HasColumnName("relacionada_id");
            e.Property(x => x.CreadoEn).HasColumnName("created_at");
        });

        modelBuilder.Entity<ComentarioTarea>(e =>
        {
            e.ToTable("tarea_comentarios", Schema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.TareaId).HasColumnName("tarea_id");
            e.Property(x => x.AutorId).HasColumnName("autor_id");
            e.Property(x => x.AutorRol).HasColumnName("autor_rol");
            e.Property(x => x.Texto).HasColumnName("texto");
            e.Property(x => x.CreadoEn).HasColumnName("created_at");
        });

        modelBuilder.Entity<EventoTarea>(e =>
        {
            e.ToTable("tarea_historial", Schema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.TareaId).HasColumnName("tarea_id");
            e.Property(x => x.Accion).HasColumnName("accion");
            e.Property(x => x.ActorId).HasColumnName("actor_id");
            e.Property(x => x.ActorRol).HasColumnName("actor_rol");
            e.Property(x => x.Estado).HasColumnName("estado");
            e.Property(x => x.Detalle).HasColumnName("detalle");
            e.Property(x => x.CreadoEn).HasColumnName("created_at");
        });

        ConfigurarCatalogo<EstadoProyectoCatalogo>(modelBuilder, "estados_proyecto", e =>
        {
            e.Property(x => x.Verbo).HasColumnName("verbo");
            e.Property(x => x.EsInicial).HasColumnName("es_inicial");
            e.Property(x => x.AdmiteTareas).HasColumnName("admite_tareas");
        });
        ConfigurarCatalogo<EstadoTareaCatalogo>(modelBuilder, "estados_tarea",
            e => e.Property(x => x.EsInicial).HasColumnName("es_inicial"));
        ConfigurarCatalogo<PrioridadCatalogo>(modelBuilder, "prioridades", _ => { });
        ConfigurarCatalogo<TipoTareaCatalogo>(modelBuilder, "tipos_tarea", _ => { });

        base.OnModelCreating(modelBuilder);
    }

    private static void ConfigurarCatalogo<T>(
        ModelBuilder modelBuilder, string tabla, Action<Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T>> extra)
        where T : ItemCatalogo
    {
        var entidad = modelBuilder.Entity<T>();
        entidad.ToTable(tabla, Schema, t => t.ExcludeFromMigrations());
        entidad.HasKey(x => x.Codigo);
        entidad.Property(x => x.Codigo).HasColumnName("codigo").ValueGeneratedNever();
        entidad.Property(x => x.Nombre).HasColumnName("nombre");
        entidad.Property(x => x.Orden).HasColumnName("orden");
        extra(entidad);
    }
}
