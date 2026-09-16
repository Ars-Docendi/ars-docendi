using Microsoft.EntityFrameworkCore;

namespace ArsDocendi.Shared.Identity;

/// <summary>
/// Contexto de los schemas <c>identity</c> y <c>audit</c>.
/// <para>
/// Vive en <c>ArsDocendi.Shared</c> por decisión explícita (design D7): identity es
/// infraestructura transversal de la que dependen los 4 módulos, no un módulo de
/// negocio. Es la ÚNICA I/O admitida en este proyecto — ver invariante #4.
/// </para>
/// <para>
/// TODAS las entidades se mapean con <c>ExcludeFromMigrations()</c>: el DDL es el
/// SQL versionado bajo <c>database/</c>, que las migraciones ejecutan tal cual. El
/// modelo acá sólo describe el schema para poder consultarlo — nunca lo genera, así
/// que EF no puede divergir del SQL ni intentar recrear lo que el SQL ya define.
/// </para>
/// </summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public const string Schema = "identity";
    public const string SchemaAudit = "audit";

    public DbSet<Persona> Personas => Set<Persona>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<RolPermiso> RolPermisos => Set<RolPermiso>();
    public DbSet<UsuarioRol> UsuarioRoles => Set<UsuarioRol>();
    public DbSet<Carrera> Carreras => Set<Carrera>();
    public DbSet<Materia> Materias => Set<Materia>();
    public DbSet<RegistroCambio> RegistrosDeCambio => Set<RegistroCambio>();
    public DbSet<IdentidadSembrada> IdentidadesSembradas => Set<IdentidadSembrada>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Persona>(e =>
        {
            e.ToTable("personas", Schema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Documento).HasColumnName("documento");
            e.Property(x => x.Cuil).HasColumnName("cuil");
            e.Property(x => x.Legajo).HasColumnName("legajo");
            e.Property(x => x.Nombre).HasColumnName("nombre");
            e.Property(x => x.Apellido).HasColumnName("apellido");
            e.Property(x => x.FechaNacimiento).HasColumnName("fecha_nacimiento");
            e.Property(x => x.Telefono).HasColumnName("telefono");
            e.Property(x => x.CreadoEn).HasColumnName("created_at");
            e.HasIndex(x => x.Documento).IsUnique();
        });

        modelBuilder.Entity<Usuario>(e =>
        {
            e.ToTable("users", Schema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.AzureOid).HasColumnName("azure_oid");
            e.Property(x => x.Upn).HasColumnName("upn");
            e.Property(x => x.NombreParaMostrar).HasColumnName("display_name");
            e.Property(x => x.Activo).HasColumnName("is_active");
            e.Property(x => x.CreadoEn).HasColumnName("created_at");
            e.Property(x => x.UltimoLoginEn).HasColumnName("last_login_at");
            e.Property(x => x.PersonaId).HasColumnName("persona_id");
            e.Property(x => x.Version).IsRowVersion();
            e.HasOne(x => x.Persona).WithOne(p => p!.Usuario!).HasForeignKey<Usuario>(x => x.PersonaId);
        });

        modelBuilder.Entity<Rol>(e =>
        {
            e.ToTable("roles", Schema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Codigo).HasColumnName("code");
            e.Property(x => x.Nombre).HasColumnName("name");
            e.Property(x => x.Descripcion).HasColumnName("description");
            e.Property(x => x.Ambito).HasColumnName("scope");
            e.Property(x => x.EsSistema).HasColumnName("es_sistema");
            e.Property(x => x.Activo).HasColumnName("is_active");
            e.Property(x => x.CreadoEn).HasColumnName("created_at");
            e.Property(x => x.Version).IsRowVersion();
            e.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<Permiso>(e =>
        {
            e.ToTable("permisos", Schema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Codigo).HasColumnName("code");
            e.Property(x => x.Nombre).HasColumnName("nombre");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.CreadoEn).HasColumnName("created_at");
            e.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<RolPermiso>(e =>
        {
            e.ToTable("rol_permisos", Schema, t => t.ExcludeFromMigrations());
            e.HasKey(x => new { x.RolId, x.PermisoId });
            e.Property(x => x.RolId).HasColumnName("rol_id");
            e.Property(x => x.PermisoId).HasColumnName("permiso_id");
            e.Property(x => x.CreadoEn).HasColumnName("created_at");
            e.HasOne(x => x.Rol).WithMany(r => r!.Permisos).HasForeignKey(x => x.RolId);
            e.HasOne(x => x.Permiso).WithMany().HasForeignKey(x => x.PermisoId);
        });

        modelBuilder.Entity<UsuarioRol>(e =>
        {
            e.ToTable("user_roles", Schema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UsuarioId).HasColumnName("user_id");
            e.Property(x => x.RolId).HasColumnName("role_id");
            e.Property(x => x.MateriaId).HasColumnName("materia_id");
            e.Property(x => x.CarreraId).HasColumnName("carrera_id");
            e.Property(x => x.OtorgadoEn).HasColumnName("granted_at");
            e.Property(x => x.OtorgadoPor).HasColumnName("granted_by");
            e.Property(x => x.CreadoEn).HasColumnName("created_at");
            e.Property(x => x.EliminadoEn).HasColumnName("deleted_at");
            e.HasOne(x => x.Usuario).WithMany(u => u!.Roles).HasForeignKey(x => x.UsuarioId);
            e.HasOne(x => x.Rol).WithMany().HasForeignKey(x => x.RolId);
            e.HasOne(x => x.Materia).WithMany().HasForeignKey(x => x.MateriaId);
            e.HasOne(x => x.Carrera).WithMany().HasForeignKey(x => x.CarreraId);
            // Soft-delete de dominio: los repositorios filtran las asignaciones vivas.
            e.HasQueryFilter(x => x.EliminadoEn == null);
        });

        modelBuilder.Entity<Carrera>(e =>
        {
            e.ToTable("carreras", Schema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Codigo).HasColumnName("code");
            e.Property(x => x.Nombre).HasColumnName("name");
            e.Property(x => x.Activo).HasColumnName("is_active");
            e.Property(x => x.CreadoEn).HasColumnName("created_at");
            e.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<Materia>(e =>
        {
            e.ToTable("materias", Schema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Codigo).HasColumnName("code");
            e.Property(x => x.Nombre).HasColumnName("name");
            e.Property(x => x.CarreraId).HasColumnName("carrera_id");
            e.Property(x => x.Activo).HasColumnName("is_active");
            e.Property(x => x.CreadoEn).HasColumnName("created_at");
            e.HasOne(x => x.Carrera).WithMany(c => c!.Materias).HasForeignKey(x => x.CarreraId);
            e.HasIndex(x => new { x.CarreraId, x.Codigo }).IsUnique();
        });

        modelBuilder.Entity<RegistroCambio>(e =>
        {
            e.ToTable("change_log", SchemaAudit, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.NombreSchema).HasColumnName("schema_name");
            e.Property(x => x.NombreTabla).HasColumnName("table_name");
            e.Property(x => x.ClaveFila).HasColumnName("row_pk");
            e.Property(x => x.Accion).HasColumnName("action");
            e.Property(x => x.FilaAnterior).HasColumnName("old_row").HasColumnType("jsonb");
            e.Property(x => x.FilaNueva).HasColumnName("new_row").HasColumnType("jsonb");
            e.Property(x => x.ColumnasCambiadas).HasColumnName("changed_columns");
            e.Property(x => x.CambiadoPor).HasColumnName("changed_by");
            e.Property(x => x.CambiadoEn).HasColumnName("changed_at");
            e.Property(x => x.RequestId).HasColumnName("request_id");
        });

        modelBuilder.Entity<IdentidadSembrada>(e =>
        {
            e.ToTable("seed_identities", "public", t => t.ExcludeFromMigrations());
            e.HasKey(x => x.UsuarioId);
            e.Property(x => x.UsuarioId).HasColumnName("user_id");
            e.Property(x => x.VersionDataset).HasColumnName("dataset_version");
            e.Property(x => x.CreadoEn).HasColumnName("created_at");
        });

        base.OnModelCreating(modelBuilder);
    }
}
