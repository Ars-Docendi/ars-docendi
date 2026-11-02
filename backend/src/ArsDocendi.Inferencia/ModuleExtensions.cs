using System.Net.Http.Headers;
using System.Text;
using ArsDocendi.Inferencia.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ArsDocendi.Inferencia;

public static class ModuleExtensions
{
    public static IServiceCollection AddInferencia(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<InferenciaOptions>()
            .Bind(configuration.GetSection(InferenciaOptions.Seccion))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<InferenciaOptions>, ValidacionInferenciaOptions>();

        services.AddHttpClient<ClienteInferencia>((sp, http) =>
        {
            var opciones = sp.GetRequiredService<IOptions<InferenciaOptions>>().Value;
            if (!opciones.Habilitada) return;

            http.BaseAddress = new Uri(opciones.Url.TrimEnd('/') + "/");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{opciones.Usuario}:{opciones.Contrasena}")));
            if (!string.IsNullOrWhiteSpace(opciones.AccessClientId))
            {
                http.DefaultRequestHeaders.Add("CF-Access-Client-Id", opciones.AccessClientId);
                http.DefaultRequestHeaders.Add("CF-Access-Client-Secret", opciones.AccessClientSecret);
            }
        });

        services.AddControllers().AddApplicationPart(typeof(ModuleExtensions).Assembly);
        return services;
    }

    // En Development no se exigen secretos locales, igual que Almacenamiento.
    private sealed class ValidacionInferenciaOptions(IHostEnvironment ambiente) : IValidateOptions<InferenciaOptions>
    {
        public ValidateOptionsResult Validate(string? name, InferenciaOptions opciones)
        {
            if (ambiente.IsDevelopment()) return ValidateOptionsResult.Success;
            var errores = opciones.Errores().ToList();
            return errores.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errores);
        }
    }
}
