using ArsDocendi.Shared.Auditing;
using ArsDocendi.Shared.Persistencia;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Portal.Infrastructure;
using Modules.Portal.Application;
using Modules.Portal.Repositories;

namespace Modules.Portal;

public static class ModuleExtensions
{
    public static IServiceCollection AddPortalModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PortalDbContext>((sp, opt) =>
            opt.UseNpgsql(configuration.GetConnectionString("ArsDocendi"))
               .AddInterceptors(sp.GetRequiredService<AuditDbConnectionInterceptor>()));

        services.AddScoped<IMigradorModulo, MigradorPortal>();
        services.AddScoped<IRepositorioPortal, RepositorioPortal>();
        services.AddScoped<ServicioPortal>();
        services.AddScoped<Modules.Portal.Contracts.Queries.IPortalQueries>(sp => sp.GetRequiredService<ServicioPortal>());

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<PortalDbContext>());

        services.AddControllers()
            .AddApplicationPart(typeof(ModuleExtensions).Assembly);

        return services;
    }
}
