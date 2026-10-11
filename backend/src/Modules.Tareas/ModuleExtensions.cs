using ArsDocendi.Shared.Auditing;
using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Tareas.Application;
using Modules.Tareas.Infrastructure;
using Modules.Tareas.Repositories;

namespace Modules.Tareas;

public static class ModuleExtensions
{
    public static IServiceCollection AddTareasModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<TareasDbContext>((sp, opt) =>
            opt.UseNpgsql(configuration.GetConnectionString("ArsDocendi"))
               .AddInterceptors(sp.GetRequiredService<AuditDbConnectionInterceptor>()));

        services.AddScoped<IMigradorModulo, MigradorTareas>();
        services.AddScoped<RepositorioTareas>();
        services.AddScoped<DirectorioPersonas>();
        services.AddScoped<ServicioTareas>();
        services.AddScoped<ServicioProyectos>();

        services.AddControllers()
            .AddApplicationPart(typeof(ModuleExtensions).Assembly);

        return services;
    }
}
