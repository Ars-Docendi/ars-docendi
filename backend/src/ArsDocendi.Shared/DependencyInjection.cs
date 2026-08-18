using ArsDocendi.Shared.Auditing;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Persistencia;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArsDocendi.Shared;

public static class DependencyInjection
{
    /// <summary>
    /// Registra las utilidades transversales y la persistencia de identidad/auditoría.
    /// <para>
    /// DEBE invocarse ANTES que los <c>Add&lt;Modulo&gt;Module()</c> en el Host: el
    /// contenedor devuelve las implementaciones de <see cref="IMigradorModulo"/> en
    /// orden de registración, y el schema <c>audit</c> + las tablas de <c>identity</c>
    /// tienen que existir antes de que cualquier módulo aplique su DDL.
    /// </para>
    /// </summary>
    public static IServiceCollection AddArsDocendiShared(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<AuditDbConnectionInterceptor>();

        services.AddDbContext<IdentityDbContext>((sp, opt) =>
            opt.UseNpgsql(configuration.GetConnectionString("ArsDocendi"), npgsql =>
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema))
               .AddInterceptors(sp.GetRequiredService<AuditDbConnectionInterceptor>()));

        services.AddScoped<IMigradorModulo, MigradorIdentity>();
        services.AddScoped<IConsultasIdentity, ConsultasIdentity>();
        services.AddScoped<IVinculadorPrimerLogin, VinculadorPrimerLogin>();

        return services;
    }
}
