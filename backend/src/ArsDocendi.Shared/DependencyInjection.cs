using ArsDocendi.Shared.Auditing;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Identity.Administracion;
using ArsDocendi.Shared.Identity.Desarrollo;
using ArsDocendi.Shared.Identity.Ingreso;
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
    /// DEBE invocarse antes de AddMigracionesIdentity y de los módulos en el Host.
    /// El adaptador de migraciones vive en ArsDocendi.Migraciones para no agregar
    /// I/O de otros contextos a Shared; Identity/Audit se aplica antes de Storage
    /// y de las tablas de negocio que dependen de ambas infraestructuras.
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

        services.AddScoped<IConsultasIdentity, ConsultasIdentity>();
        services.AddScoped<IRepositorioUsuarios, RepositorioUsuarios>();
        services.AddScoped<ServicioUsuarios>();
        services.AddScoped<ServicioAdministradorInicial>();
        services.AddScoped<IRepositorioRoles, RepositorioRoles>();
        services.AddScoped<ServicioRoles>();
        services.AddScoped<IRepositorioDocentes, RepositorioDocentes>();
        services.AddScoped<IAdministracionIdentity, ServicioPersonas>();
        services.AddScoped<IUnidadDeTrabajoAdministracion, UnidadDeTrabajoAdministracion>();
        services.AddScoped<ServicioIdentidadesDesarrollo>();
        services.AddScoped<RepositorioIngreso>();
        services.AddScoped<ServicioSesion>();
        services.AddScoped<ServicioIngreso>();

        return services;
    }
}
