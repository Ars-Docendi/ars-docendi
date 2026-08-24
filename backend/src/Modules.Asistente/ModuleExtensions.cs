using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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

        // Las dos cadenas de solo lectura se DERIVAN de la del dueño: mismo host,
        // mismo puerto, misma base, otro usuario. Con tres cadenas configuradas por
        // separado, un typo en el nombre de la base haría que el asistente leyera
        // otro ambiente sin que nada fallara.
        //
        // Se registran como fábrica, no como valor: recién fallan si alguien las
        // pide sin haberlas configurado. Construirlas acá rompería el arranque en
        // cualquier ambiente que todavía no tenga los dos roles.
        services.AddSingleton(sp => new CadenaSoloLectura(CadenasDeConexion.Derivar(
            sp.GetRequiredService<CadenaDuena>(),
            Requerido(sp, o => o.RolSoloLectura, nameof(OpcionesAsistente.RolSoloLectura)),
            Requerido(sp, o => o.PasswordSoloLectura, nameof(OpcionesAsistente.PasswordSoloLectura)))));

        services.AddSingleton(sp => new CadenaSoloLecturaPii(CadenasDeConexion.Derivar(
            sp.GetRequiredService<CadenaDuena>(),
            Requerido(sp, o => o.RolSoloLecturaPii, nameof(OpcionesAsistente.RolSoloLecturaPii)),
            Requerido(sp, o => o.PasswordSoloLecturaPii, nameof(OpcionesAsistente.PasswordSoloLecturaPii)))));

        services.AddControllers()
            .AddApplicationPart(typeof(ModuleExtensions).Assembly);

        return services;
    }

    private static string Requerido(
        IServiceProvider sp, Func<OpcionesAsistente, string> leer, string nombre)
    {
        var valor = leer(sp.GetRequiredService<IOptions<OpcionesAsistente>>().Value);
        return string.IsNullOrWhiteSpace(valor)
            ? throw new InvalidOperationException(
                $"Falta '{OpcionesAsistente.Seccion}:{nombre}' en la configuración del ambiente.")
            : valor;
    }
}
