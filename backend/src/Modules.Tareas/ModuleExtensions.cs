using ArsDocendi.Shared.Persistencia;
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
        services.AddDbContext<TareasDbContext>(opt =>
            opt.UseNpgsql(configuration.GetConnectionString("ArsDocendi")));

        services.AddScoped<IMigradorModulo, MigradorTareas>();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<TareasDbContext>());

        services.AddControllers()
            .AddApplicationPart(typeof(ModuleExtensions).Assembly);

        return services;
    }
}
