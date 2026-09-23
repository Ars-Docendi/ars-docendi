using ArsDocendi.Shared.Auditing;
using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Aulas.Infrastructure;
using Modules.Aulas.Repositories;
using Modules.Aulas.Services;

namespace Modules.Aulas;

public static class ModuleExtensions
{
    public static IServiceCollection AddAulasModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AulasDbContext>((sp, opt) =>
            opt.UseNpgsql(configuration.GetConnectionString("ArsDocendi"))
               .AddInterceptors(sp.GetRequiredService<AuditDbConnectionInterceptor>()));

        services.AddScoped<IMigradorModulo, MigradorAulas>();

        services.AddScoped<RepositorioSolicitudesAula>();
        services.AddScoped<IServicioSolicitudesAula, ServicioSolicitudesAula>();

        services.AddControllers()
            .AddApplicationPart(typeof(ModuleExtensions).Assembly);

        return services;
    }
}
