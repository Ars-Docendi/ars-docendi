using ArsDocendi.Shared.Auditing;
using ArsDocendi.Shared.Persistencia;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ArsDocendi.Storage;

public static class ModuleExtensions
{
    public static IServiceCollection AddAlmacenamientoModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AlmacenamientoOptions>(configuration.GetSection(AlmacenamientoOptions.Seccion));
        services.AddDbContext<Infrastructure.AlmacenamientoDbContext>((sp, opt) =>
            opt.UseNpgsql(
                    configuration.GetConnectionString("ArsDocendi"),
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", Infrastructure.AlmacenamientoDbContext.Schema))
               .AddInterceptors(sp.GetRequiredService<AuditDbConnectionInterceptor>()));
        services.AddScoped<IMigradorModulo, Infrastructure.MigradorAlmacenamiento>();

        services.AddSingleton<IAmazonS3>(sp =>
        {
            var opciones = sp.GetRequiredService<IOptions<AlmacenamientoOptions>>().Value;
            var ambiente = sp.GetRequiredService<IHostEnvironment>();
            var accessKey = opciones.AccessKey;
            var secretKey = opciones.SecretKey;
            if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
            {
                if (!ambiente.IsDevelopment())
                    throw new InvalidOperationException("Faltan las credenciales S3 del almacenamiento.");
                accessKey = "development-not-configured";
                secretKey = "development-not-configured";
            }

            var endpoint = opciones.Endpoint.TrimEnd('/');
            if (!endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                endpoint = $"{(opciones.UseSsl ? "https" : "http")}://{endpoint}";
            }

            var config = new AmazonS3Config
            {
                ServiceURL = endpoint,
                ForcePathStyle = true,
                UseHttp = !opciones.UseSsl,
                AuthenticationRegion = "us-east-1",
            };
            return new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), config);
        });
        services.AddScoped<Infrastructure.IProveedorObjetos, Infrastructure.ProveedorSeaweedFs>();
        services.AddScoped<Infrastructure.IAntivirusArchivos, Infrastructure.ClamAvAntivirus>();
        services.AddScoped<Contracts.IAlmacenamientoArchivos, Infrastructure.ServicioAlmacenamientoArchivos>();
        services.AddControllers().AddApplicationPart(typeof(ModuleExtensions).Assembly);
        return services;
    }
}
