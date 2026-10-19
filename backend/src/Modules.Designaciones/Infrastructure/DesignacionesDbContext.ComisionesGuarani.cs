using Microsoft.EntityFrameworkCore;
using Modules.Designaciones.Domain;

namespace Modules.Designaciones.Infrastructure;

/// <summary>Mapeo del snapshot <c>sga_*</c> importado desde Guaraní, en el schema <c>comisiones</c>.</summary>
public sealed partial class DesignacionesDbContext
{
    public const string SchemaComisiones = "comisiones";

    private static void MapearComisionesGuarani(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ImportacionComisiones>(e =>
        {
            e.ToTable("sga_importaciones", SchemaComisiones, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.AnioAcademico).HasColumnName("anio_academico");
            e.Property(x => x.PeriodoLectivoGuaraniId).HasColumnName("periodo_lectivo_guarani_id");
            e.Property(x => x.PeriodoLectivoNombre).HasColumnName("periodo_lectivo_nombre");
            e.Property(x => x.UsuarioId).HasColumnName("usuario_id");
            e.Property(x => x.IniciadaEn).HasColumnName("iniciada_at");
            e.Property(x => x.FinalizadaEn).HasColumnName("finalizada_at");
            e.Property(x => x.Estado).HasColumnName("estado");
            e.Property(x => x.Cantidad).HasColumnName("cantidad");
            e.Property(x => x.DocentesSinVincular).HasColumnName("docentes_sin_vincular");
            e.Property(x => x.CarrerasSinVincular).HasColumnName("carreras_sin_vincular");
            e.Property(x => x.Causa).HasColumnName("causa");
            e.Property(x => x.CreadoEn).HasColumnName("created_at");
        });

        modelBuilder.Entity<ComisionGuarani>(e =>
        {
            e.ToTable("sga_comisiones", SchemaComisiones, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.ComisionGuaraniId);
            e.Property(x => x.ComisionGuaraniId).HasColumnName("comision_guarani_id");
            e.Property(x => x.ImportacionId).HasColumnName("importacion_id");
            e.Property(x => x.AnioAcademico).HasColumnName("anio_academico");
            e.Property(x => x.PeriodoLectivoGuaraniId).HasColumnName("periodo_lectivo_guarani_id");
            e.Property(x => x.PeriodoLectivoNombre).HasColumnName("periodo_lectivo_nombre");
            e.Property(x => x.Nombre).HasColumnName("nombre");
            e.Property(x => x.MateriaCodigo).HasColumnName("materia_codigo");
            e.Property(x => x.MateriaNombre).HasColumnName("materia_nombre");
            e.Property(x => x.MateriaId).HasColumnName("materia_id");
            e.Property(x => x.ModalidadCodigo).HasColumnName("modalidad_codigo");
            e.Property(x => x.Modalidad).HasColumnName("modalidad");
            e.Property(x => x.Ubicacion).HasColumnName("ubicacion");
            e.Property(x => x.CreadoEn).HasColumnName("created_at");
            e.HasOne<ImportacionComisiones>().WithMany().HasForeignKey(x => x.ImportacionId);
            e.HasMany(x => x.Carreras).WithOne().HasForeignKey(c => c.ComisionGuaraniId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Docentes).WithOne().HasForeignKey(d => d.ComisionGuaraniId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Horarios).WithOne().HasForeignKey(h => h.ComisionGuaraniId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ComisionCarrera>(e =>
        {
            e.ToTable("sga_comision_carreras", SchemaComisiones, t => t.ExcludeFromMigrations());
            e.HasKey(x => new { x.ComisionGuaraniId, x.CarreraCodigo });
            e.Property(x => x.ComisionGuaraniId).HasColumnName("comision_guarani_id");
            e.Property(x => x.CarreraCodigo).HasColumnName("carrera_codigo");
            e.Property(x => x.CarreraNombre).HasColumnName("carrera_nombre");
            e.Property(x => x.CarreraId).HasColumnName("carrera_id");
        });

        modelBuilder.Entity<ComisionDocente>(e =>
        {
            e.ToTable("sga_comision_docentes", SchemaComisiones, t => t.ExcludeFromMigrations());
            e.HasKey(x => new { x.ComisionGuaraniId, x.Orden });
            e.Property(x => x.ComisionGuaraniId).HasColumnName("comision_guarani_id");
            e.Property(x => x.Orden).HasColumnName("orden");
            e.Property(x => x.Nombre).HasColumnName("nombre");
            e.Property(x => x.Responsabilidad).HasColumnName("responsabilidad");
            e.Property(x => x.PersonaId).HasColumnName("persona_id");
        });

        modelBuilder.Entity<ComisionHorario>(e =>
        {
            e.ToTable("sga_comision_horarios", SchemaComisiones, t => t.ExcludeFromMigrations());
            e.HasKey(x => new { x.ComisionGuaraniId, x.AsignacionGuaraniId });
            e.Property(x => x.ComisionGuaraniId).HasColumnName("comision_guarani_id");
            e.Property(x => x.AsignacionGuaraniId).HasColumnName("asignacion_guarani_id");
            e.Property(x => x.Dia).HasColumnName("dia");
            e.Property(x => x.Desde).HasColumnName("desde");
            e.Property(x => x.Hasta).HasColumnName("hasta");
            e.Property(x => x.Aula).HasColumnName("aula");
        });
    }
}
