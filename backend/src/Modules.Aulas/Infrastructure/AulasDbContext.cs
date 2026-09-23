using Microsoft.EntityFrameworkCore;
using Modules.Aulas.Domain;

namespace Modules.Aulas.Infrastructure;

/// <summary>
/// Contexto del schema <c>aulas</c>.
/// <para>
/// Igual que <c>DesignacionesDbContext</c>, la entidad va con <c>ExcludeFromMigrations()</c>:
/// el DDL es el SQL versionado bajo <c>database/aulas/</c>, que la migración ejecuta tal
/// cual. El modelo acá describe el schema para consultarlo, nunca lo genera.
/// </para>
/// </summary>
public sealed class AulasDbContext(DbContextOptions<AulasDbContext> options) : DbContext(options)
{
    public const string Schema = "aulas";

    public DbSet<SolicitudReservaAula> SolicitudesReserva => Set<SolicitudReservaAula>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<SolicitudReservaAula>(e =>
        {
            e.ToTable("solicitudes_reserva", Schema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.DocenteId).HasColumnName("docente_id");
            e.Property(x => x.Dia).HasColumnName("dia");
            e.Property(x => x.HorarioDesde).HasColumnName("horario_desde");
            e.Property(x => x.HorarioHasta).HasColumnName("horario_hasta");
            e.Property(x => x.CantidadAlumnosAprox).HasColumnName("cantidad_alumnos_aprox");
            e.Property(x => x.MateriaId).HasColumnName("materia_id");
            e.Property(x => x.Comision).HasColumnName("comision");
            e.Property(x => x.Estado).HasColumnName("estado");
            e.Property(x => x.AulaAsignada).HasColumnName("aula_asignada");
            e.Property(x => x.MotivoRechazo).HasColumnName("motivo_rechazo");
            e.Property(x => x.CreadoEn).HasColumnName("created_at");

            e.HasIndex(x => x.DocenteId);
            e.HasIndex(x => x.Estado);
            e.HasIndex(x => x.MateriaId);
        });

        base.OnModelCreating(modelBuilder);
    }
}
