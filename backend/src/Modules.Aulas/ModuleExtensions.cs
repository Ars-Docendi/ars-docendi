using ArsDocendi.Shared.Auditing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Aulas.Infrastructure;

namespace Modules.Aulas;

public static class ModuleExtensions
{
    public static IServiceCollection AddAulasModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AulasDbContext>((sp, opt) =>
            opt.UseNpgsql(configuration.GetConnectionString("Postgres"))
               .AddInterceptors(sp.GetRequiredService<AuditDbConnectionInterceptor>()));

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<AulasDbContext>());

        services.AddControllers()
            .AddApplicationPart(typeof(ModuleExtensions).Assembly);

        return services;
    }
}
