using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
    /// El módulo todavía no tiene servicios propios. Lo único que hace acá es
    /// declarar su assembly como application part, para que MVC descubra los
    /// controllers que lleguen después sin volver a tocar el Host.
    ///
    /// <paramref name="configuration"/> no se usa todavía: va en la firma por
    /// paridad con el resto de los módulos y porque de ahí van a salir las
    /// cadenas de conexión de solo lectura del asistente.
    /// </remarks>
    public static IServiceCollection AddAsistenteModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddControllers()
            .AddApplicationPart(typeof(ModuleExtensions).Assembly);

        return services;
    }
}
