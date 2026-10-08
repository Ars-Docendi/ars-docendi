using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.DependencyInjection;

namespace ArsDocendi.Migraciones;

public static class ModuleExtensions
{
    public static IServiceCollection AddMigracionesIdentity(this IServiceCollection services)
    {
        services.AddScoped<IMigradorModulo, MigradorIdentity>();
        return services;
    }
}

internal sealed class MigradorIdentity(IdentityDbContext db)
    : MigradorEfSql<IdentityDbContext>(db, "identity", IdentityDbContext.Schema, IdentityDbContext.SchemaAudit);
