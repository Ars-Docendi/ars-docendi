using ArsDocendi.Shared.Auditing;
using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Designaciones.Contracts.Administracion;
using Modules.Designaciones.Infrastructure;
using Modules.Designaciones.Repositories;
using Modules.Designaciones.Services;

namespace Modules.Designaciones;

public static class ModuleExtensions
{
    public static IServiceCollection AddDesignacionesModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DesignacionesDbContext>((sp, opt) =>
            opt.UseNpgsql(configuration.GetConnectionString("ArsDocendi"))
               .AddInterceptors(sp.GetRequiredService<AuditDbConnectionInterceptor>()));

        services.AddScoped<IMigradorModulo, MigradorDesignaciones>();

        services.AddScoped<UnidadDeTrabajo>();
        services.AddScoped<RepositorioPedidos>();
        services.AddScoped<RepositorioLoteDesignaciones>();
        services.AddScoped<RepositorioDesignaciones>();
        services.AddScoped<RepositorioPeriodos>();
        services.AddScoped<RepositorioCatalogosDesignaciones>();
        services.AddScoped<RepositorioIdempotencia>();
        services.AddScoped<MaterializadorDesignaciones>();
        services.AddScoped<ResolutorActor>();
        services.AddScoped<ServicioPedidos>();
        services.AddScoped<IServicioPedidosApi, ServicioPedidosApi>();
        services.AddScoped<IServicioLoteDesignaciones, ServicioLoteDesignaciones>();
        services.AddScoped<ServicioPeriodos>();
        services.AddScoped<ServicioCatalogosDesignaciones>();
        services.AddScoped<IAdministracionDesignaciones, ServicioAdministracionDesignaciones>();

        services.AddControllers()
            .AddApplicationPart(typeof(ModuleExtensions).Assembly);

        return services;
    }
}
