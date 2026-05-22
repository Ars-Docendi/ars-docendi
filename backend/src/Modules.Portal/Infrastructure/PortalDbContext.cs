using Microsoft.EntityFrameworkCore;

namespace Modules.Portal.Infrastructure;

public sealed class PortalDbContext(DbContextOptions<PortalDbContext> options) : DbContext(options)
{
    public const string Schema = "portal";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        base.OnModelCreating(modelBuilder);
    }
}
