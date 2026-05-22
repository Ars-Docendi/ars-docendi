using Microsoft.EntityFrameworkCore;

namespace Modules.Aulas.Infrastructure;

public sealed class AulasDbContext(DbContextOptions<AulasDbContext> options) : DbContext(options)
{
    public const string Schema = "aulas";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        base.OnModelCreating(modelBuilder);
    }
}
