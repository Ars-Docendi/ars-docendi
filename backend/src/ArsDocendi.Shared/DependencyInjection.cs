using ArsDocendi.Shared.Auditing;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Identity.Administracion;
using ArsDocendi.Shared.Identity.Desarrollo;
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
        // La cadena del dueño se resuelve UNA vez y viaja tipada. Los DbContext y
        // los migradores la piden por tipo, no por clave de configuración: pedir la
        // cadena equivocada deja de ser posible sin romper la compilación.
        services.AddSingleton(CadenaDuena.Desde(configuration));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<AuditDbConnectionInterceptor>();

        services.AddDbContext<IdentityDbContext>((sp, opt) =>
            opt.UseNpgsql(sp.GetRequiredService<CadenaDuena>().Valor, npgsql =>
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema))
               .AddInterceptors(sp.GetRequiredService<AuditDbConnectionInterceptor>()));

        services.AddScoped<IMigradorModulo, MigradorIdentity>();
        services.AddScoped<IConsultasIdentity, ConsultasIdentity>();
        services.AddScoped<IVinculadorPrimerLogin, VinculadorPrimerLogin>();
        services.AddScoped<IRepositorioUsuarios, RepositorioUsuarios>();
        services.AddScoped<ServicioUsuarios>();
        services.AddScoped<IRepositorioRoles, RepositorioRoles>();
        services.AddScoped<ServicioRoles>();
        services.AddScoped<IRepositorioDocentes, RepositorioDocentes>();
        services.AddScoped<IUnidadDeTrabajoAdministracion, UnidadDeTrabajoAdministracion>();
        services.AddScoped<ServicioIdentidadesDesarrollo>();

        return services;
    }
}
