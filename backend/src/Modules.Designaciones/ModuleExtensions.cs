using ArsDocendi.Shared.Auditing;
using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Designaciones.Contracts.Administracion;
using Modules.Designaciones.Infrastructure;
using Modules.Designaciones.Infrastructure.Guarani;
using Modules.Designaciones.Repositories;
using Modules.Designaciones.Services;

namespace Modules.Designaciones;

public static class ModuleExtensions
{
    public static IServiceCollection AddDesignacionesModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DesignacionesDbContext>((sp, opt) =>
            opt.UseNpgsql(configuration.GetConnectionString("ArsDocendi"), npgsql =>
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", DesignacionesDbContext.Schema))
               .AddInterceptors(sp.GetRequiredService<AuditDbConnectionInterceptor>()));

        services.AddScoped<IMigradorModulo, MigradorDesignaciones>();
        services.AddScoped<IDiagnosticoIntegridadModulo, DiagnosticoDesignaciones>();
        services.AddScoped<ReferenciasArchivosDesignaciones>();
        services.AddScoped<ArsDocendi.Storage.Contracts.IConsultasReferenciasArchivos>(sp =>
            sp.GetRequiredService<ReferenciasArchivosDesignaciones>());
        services.AddScoped<ArsDocendi.Storage.Contracts.IParticipanteTransaccionArchivos>(sp =>
            sp.GetRequiredService<ReferenciasArchivosDesignaciones>());

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
        services.AddHostedService<LimpiezaComandosIdempotentes>();

        // Guaraní: sin configuración completa la integración queda deshabilitada (503) y el Host arranca igual.
        services.Configure<OpcionesGuarani>(configuration.GetSection(OpcionesGuarani.Seccion));
        services.AddHttpClient<ClienteGuarani>((sp, http) =>
        {
            var opciones = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpcionesGuarani>>().Value;
            if (Uri.TryCreate(opciones.UrlBase, UriKind.Absolute, out var url))
                http.BaseAddress = new Uri(url.ToString().TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(Math.Max(1, opciones.TimeoutSegundos));
        });
        services.AddScoped<RepositorioComisiones>();
        services.AddScoped<ServicioComisiones>();

        services.AddControllers()
            .AddApplicationPart(typeof(ModuleExtensions).Assembly);

        return services;
    }
}
