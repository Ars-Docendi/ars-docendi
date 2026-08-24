using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Asistente.Infrastructure;

namespace Modules.Asistente;

/// <summary>
/// Registración del módulo del asistente conversacional en la composición del Host.
/// </summary>
public static class ModuleExtensions
{
    /// <summary>
    /// Suma el módulo del asistente al contenedor de dependencias.
    /// </summary>
    /// <remarks>
    /// La configuración de los roles NO se valida al arrancar: solo hace falta en
    /// el arranque one-shot <c>--migrate</c>, y exigirla siempre rompería el Host
    /// en cualquier ambiente que todavía no la tenga. El migrador falla con un
    /// mensaje que nombra el valor faltante cuando efectivamente la necesita.
    /// </remarks>
    public static IServiceCollection AddAsistenteModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OpcionesAsistente>(
            configuration.GetSection(OpcionesAsistente.Seccion));

        services.AddScoped<IMigradorModulo, MigradorAsistente>();

        services.AddControllers()
            .AddApplicationPart(typeof(ModuleExtensions).Assembly);

        return services;
    }
}
