using ArsDocendi.Shared.Auditing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Tareas.Infrastructure;

namespace Modules.Tareas;

public static class ModuleExtensions
{
    public static IServiceCollection AddTareasModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<TareasDbContext>((sp, opt) =>
            opt.UseNpgsql(configuration.GetConnectionString("Postgres"))
               .AddInterceptors(sp.GetRequiredService<AuditDbConnectionInterceptor>()));

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<TareasDbContext>());

        services.AddControllers()
            .AddApplicationPart(typeof(ModuleExtensions).Assembly);

        return services;
    }
}
