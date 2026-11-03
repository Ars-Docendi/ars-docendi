using Microsoft.EntityFrameworkCore;
using Modules.Notificaciones.Domain;

namespace Modules.Notificaciones.Infrastructure;

/// <summary>
/// Context of the <c>notificaciones</c> schema. As in the other modules, the DDL is the versioned
/// SQL under <c>database/notificaciones/</c>; the model describes the schema for querying, it never
/// generates it.
/// </summary>
public sealed class NotificacionesDbContext(DbContextOptions<NotificacionesDbContext> options) : DbContext(options)
{
    public const string Schema = "notificaciones";

    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();
    public DbSet<SuscripcionPush> SuscripcionesPush => Set<SuscripcionPush>();
    public DbSet<PreferenciaPush> PreferenciasPush => Set<PreferenciaPush>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Notificacion>(e =>
        {
            e.ToTable("notificaciones", Schema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.DestinatarioUsuarioId).HasColumnName("destinatario_usuario_id");
            e.Property(x => x.RolDestino).HasColumnName("rol_destino");
            e.Property(x => x.TipoEvento).HasColumnName("tipo_evento");
            e.Property(x => x.TipoEntidad).HasColumnName("tipo_entidad");
            e.Property(x => x.EntidadId).HasColumnName("entidad_id");
            e.Property(x => x.Titulo).HasColumnName("titulo");
            e.Property(x => x.Mensaje).HasColumnName("mensaje");
            e.Property(x => x.Contexto).HasColumnName("contexto").HasColumnType("jsonb");
            e.Property(x => x.LeidaEn).HasColumnName("leida_en");
            e.Property(x => x.CreadaEn).HasColumnName("creada_en");

            e.HasIndex(x => new { x.DestinatarioUsuarioId, x.LeidaEn, x.CreadaEn });
        });

        modelBuilder.Entity<SuscripcionPush>(e =>
        {
            e.ToTable("suscripciones_push", Schema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UsuarioId).HasColumnName("usuario_id");
            e.Property(x => x.Endpoint).HasColumnName("endpoint");
            e.Property(x => x.P256dh).HasColumnName("p256dh");
            e.Property(x => x.Auth).HasColumnName("auth");
            e.Property(x => x.CreadaEn).HasColumnName("creada_en");

            e.HasIndex(x => x.Endpoint).IsUnique();
            e.HasIndex(x => x.UsuarioId);
        });

        modelBuilder.Entity<PreferenciaPush>(e =>
        {
            e.ToTable("preferencias_push", Schema, t => t.ExcludeFromMigrations());
            e.HasKey(x => new { x.UsuarioId, x.TipoEvento });
            e.Property(x => x.UsuarioId).HasColumnName("usuario_id");
            e.Property(x => x.TipoEvento).HasColumnName("tipo_evento");
            e.Property(x => x.Habilitado).HasColumnName("habilitado");
            e.Property(x => x.ActualizadaEn).HasColumnName("actualizada_en");
        });

        base.OnModelCreating(modelBuilder);
    }
}
