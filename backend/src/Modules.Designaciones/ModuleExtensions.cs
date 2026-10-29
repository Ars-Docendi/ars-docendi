using ArsDocendi.Shared.Auditing;
using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Modules.Designaciones.Contracts.Administracion;
using Modules.Designaciones.Infrastructure;
using Modules.Designaciones.Repositories;
using Modules.Designaciones.Services;
using Modules.Designaciones.Services.Integridad;

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
        services.AddScoped<RepositorioSellos>();
        services.AddOptions<IntegridadOptions>()
            .Bind(configuration.GetSection(IntegridadOptions.Seccion))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<IntegridadOptions>, ValidacionIntegridadOptions>();
        services.AddSingleton<ClavesSellado>();
        services.AddScoped<SelladorPedidos>();
        services.AddScoped<VerificadorIntegridadPedidos>();
        services.AddScoped<MaterializadorDesignaciones>();
        services.AddScoped<ResolutorActor>();
        services.AddScoped<ServicioPedidos>();
        services.AddScoped<IServicioPedidosApi, ServicioPedidosApi>();
        services.AddScoped<IServicioLoteDesignaciones, ServicioLoteDesignaciones>();
        services.AddScoped<ServicioPeriodos>();
        services.AddScoped<ServicioCatalogosDesignaciones>();
        services.AddScoped<IAdministracionDesignaciones, ServicioAdministracionDesignaciones>();
        services.AddHostedService<LimpiezaComandosIdempotentes>();

        services.AddControllers()
            .AddApplicationPart(typeof(ModuleExtensions).Assembly);

        return services;
    }

    /// <summary>
    /// Modo <c>--sellar-historial-existente</c>: sella el historial previo a la cadena o
    /// sembrado. Explícito e idempotente; nunca corre al migrar ni al arrancar.
    /// </summary>
    public static Task<int> SellarHistorialExistenteAsync(IServiceProvider servicios, CancellationToken ct) =>
        servicios.GetRequiredService<SelladorPedidos>().SellarHistorialExistenteAsync(ct);
}
