using ArsDocendi.Shared.Auditing;
using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;

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

        services.AddSingleton<IMinioClient>(sp =>
        {
            var opciones = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AlmacenamientoOptions>>().Value;
            var cliente = new MinioClient();
            cliente.WithEndpoint(opciones.Endpoint);
            cliente.WithCredentials(
                string.IsNullOrWhiteSpace(opciones.AccessKey) ? "not-configured" : opciones.AccessKey,
                string.IsNullOrWhiteSpace(opciones.SecretKey) ? "not-configured" : opciones.SecretKey);
            cliente.WithSSL(opciones.UseSsl);
            cliente.Build();
            return cliente;
        });
        services.AddScoped<Infrastructure.IProveedorObjetos, Infrastructure.ProveedorMinio>();
        services.AddScoped<Infrastructure.IAntivirusArchivos, Infrastructure.ClamAvAntivirus>();
        services.AddScoped<Contracts.IAlmacenamientoArchivos, Infrastructure.ServicioAlmacenamientoArchivos>();
        services.AddControllers().AddApplicationPart(typeof(ModuleExtensions).Assembly);
        return services;
    }
}
