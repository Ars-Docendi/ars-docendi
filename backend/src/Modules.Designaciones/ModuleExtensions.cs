using ArsDocendi.Shared.Auditing;
using ArsDocendi.Shared.Persistencia;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Designaciones.Infrastructure;
using Modules.Designaciones.Repositories;
using Modules.Designaciones.Services;
using Modules.Designaciones.Contracts.Administracion;

namespace Modules.Designaciones;

public static class ModuleExtensions
{
    public static IServiceCollection AddDesignacionesModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DesignacionesDbContext>((sp, opt) =>
            opt.UseNpgsql(configuration.GetConnectionString("ArsDocendi"))
               .AddInterceptors(sp.GetRequiredService<AuditDbConnectionInterceptor>()));

        services.AddScoped<IMigradorModulo, MigradorDesignaciones>();

        services.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajo>();
        services.AddScoped<IRepositorioPedidos, RepositorioPedidos>();
        services.AddScoped<IRepositorioDesignaciones, RepositorioDesignaciones>();
        services.AddScoped<IRepositorioPeriodos, RepositorioPeriodos>();
        services.AddScoped<IRepositorioCatalogosDesignaciones, RepositorioCatalogosDesignaciones>();
        services.AddScoped<IRepositorioIdempotencia, RepositorioIdempotencia>();
        services.AddScoped<MaterializadorDesignaciones>();
        services.AddScoped<ResolutorActor>();
        services.AddScoped<ServicioPedidos>();
        services.AddScoped<IServicioPedidosApi, ServicioPedidosApi>();
        services.AddScoped<ServicioPeriodos>();
        services.AddScoped<ServicioCatalogosDesignaciones>();
        services.AddScoped<IAdministracionDesignaciones, ServicioAdministracionDesignaciones>();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<DesignacionesDbContext>());

        services.AddControllers()
            .AddApplicationPart(typeof(ModuleExtensions).Assembly);

        return services;
    }
}
