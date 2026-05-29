using ArsDocendi.Shared.Auditing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Designaciones.Infrastructure;

namespace Modules.Designaciones;

public static class ModuleExtensions
{
    public static IServiceCollection AddDesignacionesModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DesignacionesDbContext>((sp, opt) =>
            opt.UseNpgsql(configuration.GetConnectionString("Postgres"))
               .AddInterceptors(sp.GetRequiredService<AuditDbConnectionInterceptor>()));

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<DesignacionesDbContext>());

        services.AddControllers()
            .AddApplicationPart(typeof(ModuleExtensions).Assembly);

        return services;
    }
}
