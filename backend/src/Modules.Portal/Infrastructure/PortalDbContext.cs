using Microsoft.EntityFrameworkCore;
using Modules.Portal.Domain;

namespace Modules.Portal.Infrastructure;

public sealed class PortalDbContext(DbContextOptions<PortalDbContext> options) : DbContext(options)
{
    public const string Schema = "portal";

    public DbSet<Perfil> Perfiles => Set<Perfil>();
    public DbSet<Contacto> Contactos => Set<Contacto>();
    public DbSet<Cv> Cvs => Set<Cv>();
    public DbSet<Experiencia> Experiencias => Set<Experiencia>();
    public DbSet<Educacion> Educaciones => Set<Educacion>();
    public DbSet<Certificacion> Certificaciones => Set<Certificacion>();
    public DbSet<Proyecto> Proyectos => Set<Proyecto>();
    public DbSet<DocumentoProyecto> ProyectoDocumentos => Set<DocumentoProyecto>();
    public DbSet<Habilidad> Habilidades => Set<Habilidad>();
    public DbSet<DocenteHabilidad> DocenteHabilidades => Set<DocenteHabilidad>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Perfil>(e =>
        {
            e.ToTable("perfiles", Schema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PersonaId).HasColumnName("persona_id");
            e.Property(x => x.CreadoEn).HasColumnName("created_at");
            e.HasIndex(x => x.PersonaId).IsUnique();
            e.HasOne(x => x.Contacto).WithOne().HasForeignKey<Contacto>(x => x.PerfilId);
            e.HasOne(x => x.Cv).WithOne().HasForeignKey<Cv>(x => x.PerfilId);
            e.HasMany(x => x.Experiencias).WithOne().HasForeignKey(x => x.PerfilId);
            e.HasMany(x => x.Educaciones).WithOne().HasForeignKey(x => x.PerfilId);
            e.HasMany(x => x.Certificaciones).WithOne().HasForeignKey(x => x.PerfilId);
            e.HasMany(x => x.Proyectos).WithOne().HasForeignKey(x => x.PerfilId);
            e.HasMany(x => x.Habilidades).WithOne().HasForeignKey(x => x.PerfilId);
        });

        ConfigurarEntidad<Contacto>(modelBuilder, "contactos");
        modelBuilder.Entity<Contacto>().Property(x => x.Telefono).HasColumnName("telefono");
        modelBuilder.Entity<Contacto>().Property(x => x.Mail).HasColumnName("mail");
        ConfigurarEntidad<Cv>(modelBuilder, "cvs");
        modelBuilder.Entity<Cv>().Property(x => x.Nombre).HasColumnName("nombre");
        modelBuilder.Entity<Cv>().Property(x => x.FechaCarga).HasColumnName("fecha_carga");
        modelBuilder.Entity<Cv>().Property(x => x.Uri).HasColumnName("uri");
        ConfigurarEntidad<Experiencia>(modelBuilder, "experiencias");
        modelBuilder.Entity<Experiencia>().Property(x => x.Puesto).HasColumnName("puesto");
        modelBuilder.Entity<Experiencia>().Property(x => x.Organizacion).HasColumnName("organizacion");
        modelBuilder.Entity<Experiencia>().Property(x => x.Descripcion).HasColumnName("descripcion");
        modelBuilder.Entity<Experiencia>().Property(x => x.Desde).HasColumnName("desde");
        modelBuilder.Entity<Experiencia>().Property(x => x.Hasta).HasColumnName("hasta");
        ConfigurarEntidad<Educacion>(modelBuilder, "educaciones");
        modelBuilder.Entity<Educacion>().Property(x => x.Nivel).HasColumnName("nivel");
        modelBuilder.Entity<Educacion>().Property(x => x.Carrera).HasColumnName("carrera");
        modelBuilder.Entity<Educacion>().Property(x => x.Institucion).HasColumnName("institucion");
        modelBuilder.Entity<Educacion>().Property(x => x.Desde).HasColumnName("desde");
        modelBuilder.Entity<Educacion>().Property(x => x.Hasta).HasColumnName("hasta");
        ConfigurarEntidad<Certificacion>(modelBuilder, "certificaciones");
        modelBuilder.Entity<Certificacion>().Property(x => x.Nombre).HasColumnName("nombre");
        modelBuilder.Entity<Certificacion>().Property(x => x.Emisor).HasColumnName("emisor");
        modelBuilder.Entity<Certificacion>().Property(x => x.Fecha).HasColumnName("fecha");
        modelBuilder.Entity<Certificacion>().Property(x => x.Vencimiento).HasColumnName("vencimiento");
        ConfigurarEntidad<Proyecto>(modelBuilder, "proyectos");
        modelBuilder.Entity<Proyecto>().Property(x => x.Nombre).HasColumnName("nombre");
        modelBuilder.Entity<Proyecto>().Property(x => x.Rol).HasColumnName("rol");
        modelBuilder.Entity<Proyecto>().Property(x => x.Descripcion).HasColumnName("descripcion");
        modelBuilder.Entity<Proyecto>().Property(x => x.Desde).HasColumnName("desde");
        modelBuilder.Entity<Proyecto>().Property(x => x.Hasta).HasColumnName("hasta");
        modelBuilder.Entity<Proyecto>().Property(x => x.Doi).HasColumnName("doi");
        var documento = modelBuilder.Entity<DocumentoProyecto>();
        documento.ToTable("proyecto_documentos", Schema, t => t.ExcludeFromMigrations());
        documento.HasKey(x => x.Id);
        documento.Property(x => x.Id).HasColumnName("id");
        modelBuilder.Entity<DocumentoProyecto>().Property(x => x.ProyectoId).HasColumnName("proyecto_id");
        modelBuilder.Entity<DocumentoProyecto>().Property(x => x.Nombre).HasColumnName("nombre");
        modelBuilder.Entity<DocumentoProyecto>().Property(x => x.FechaCarga).HasColumnName("fecha_carga");
        modelBuilder.Entity<DocumentoProyecto>().Property(x => x.Uri).HasColumnName("uri");

        modelBuilder.Entity<Habilidad>(e =>
        {
            e.ToTable("habilidades", Schema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Termino).HasColumnName("termino");
            e.Property(x => x.TerminoNorm).HasColumnName("termino_norm");
            e.Property(x => x.Sugerido).HasColumnName("sugerido");
            e.Property(x => x.CanonicaId).HasColumnName("canonica_id");
            e.Property(x => x.Usos).HasColumnName("usos");
            e.Property(x => x.CreadoEn).HasColumnName("created_at");
            e.HasIndex(x => x.TerminoNorm).IsUnique();
        });

        modelBuilder.Entity<DocenteHabilidad>(e =>
        {
            e.ToTable("docente_habilidades", Schema, t => t.ExcludeFromMigrations());
            e.HasKey(x => new { x.PerfilId, x.HabilidadId, x.Tipo });
            e.Property(x => x.PerfilId).HasColumnName("perfil_id");
            e.Property(x => x.HabilidadId).HasColumnName("habilidad_id");
            e.Property(x => x.Tipo).HasColumnName("tipo");
            e.Property(x => x.CreadoEn).HasColumnName("created_at");
            e.HasOne(x => x.Habilidad).WithMany().HasForeignKey(x => x.HabilidadId);
        });

        modelBuilder.Entity<Proyecto>().HasOne(x => x.Documento).WithOne().HasForeignKey<DocumentoProyecto>(x => x.ProyectoId);

        base.OnModelCreating(modelBuilder);
    }

    private static void ConfigurarEntidad<T>(ModelBuilder modelBuilder, string tabla)
        where T : class
    {
        var entity = modelBuilder.Entity<T>();
        entity.ToTable(tabla, Schema, t => t.ExcludeFromMigrations());
        entity.HasKey("Id");
        entity.Property("Id").HasColumnName("id");
        entity.Property("PerfilId").HasColumnName("perfil_id");
        entity.Property("CreadoEn").HasColumnName("created_at");
    }
}
