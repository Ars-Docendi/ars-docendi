using ArsDocendi.Shared.Auditing;
using ArsDocendi.Shared.Persistencia;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Portal.Infrastructure;

namespace Modules.Portal;

public static class ModuleExtensions
{
    public static IServiceCollection AddPortalModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PortalDbContext>((sp, opt) =>
            opt.UseNpgsql(sp.GetRequiredService<CadenaDuena>().Valor)
               .AddInterceptors(sp.GetRequiredService<AuditDbConnectionInterceptor>()));

        services.AddScoped<IMigradorModulo, MigradorPortal>();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<PortalDbContext>());

        services.AddControllers()
            .AddApplicationPart(typeof(ModuleExtensions).Assembly);

        return services;
    }
}
