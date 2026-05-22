using Microsoft.EntityFrameworkCore;

namespace Modules.Designaciones.Infrastructure;

public sealed class DesignacionesDbContext(DbContextOptions<DesignacionesDbContext> options) : DbContext(options)
{
    public const string Schema = "designaciones";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        base.OnModelCreating(modelBuilder);
    }
}
