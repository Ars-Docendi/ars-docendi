using Microsoft.EntityFrameworkCore;

namespace Modules.Tareas.Infrastructure;

public sealed class TareasDbContext(DbContextOptions<TareasDbContext> options) : DbContext(options)
{
    public const string Schema = "tareas";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        base.OnModelCreating(modelBuilder);
    }
}
