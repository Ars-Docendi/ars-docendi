using ArsDocendi.Shared.Auditing;
using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Notificaciones.Contracts;
using Modules.Notificaciones.Infrastructure;
using Modules.Notificaciones.Repositories;
using Modules.Notificaciones.Services;

namespace Modules.Notificaciones;

public static class ModuleExtensions
{
    public static IServiceCollection AddNotificacionesModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<NotificacionesDbContext>((sp, opt) =>
            opt.UseNpgsql(configuration.GetConnectionString("ArsDocendi"), npgsql =>
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", NotificacionesDbContext.Schema))
               .AddInterceptors(sp.GetRequiredService<AuditDbConnectionInterceptor>()));

        services.AddScoped<IMigradorModulo, MigradorNotificaciones>();

        services.AddScoped<RepositorioNotificaciones>();
        services.AddScoped<IServicioNotificaciones, ServicioNotificaciones>();
        services.AddScoped<INotificador, Notificador>();

        services.Configure<OpcionesWebPush>(configuration.GetSection(OpcionesWebPush.Seccion));
        services.AddSingleton<ConfiguracionWebPush>();
        services.AddSingleton<ColaPush>();
        services.AddSingleton(_ => new WebPush.WebPushClient());
        services.AddSingleton<IEnviadorPush, EnviadorWebPush>();
        services.AddHostedService<ProcesadorPush>();
        services.AddScoped<RepositorioPush>();
        services.AddScoped<IServicioPush, ServicioPush>();

        services.AddControllers()
            .AddApplicationPart(typeof(ModuleExtensions).Assembly);

        return services;
    }
}
