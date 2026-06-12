using ArsDocendi.Shared.Persistencia;
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
        services.AddDbContext<AulasDbContext>(opt =>
            opt.UseNpgsql(configuration.GetConnectionString("ArsDocendi")));

        services.AddScoped<IMigradorModulo, MigradorAulas>();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<AulasDbContext>());

        services.AddControllers()
            .AddApplicationPart(typeof(ModuleExtensions).Assembly);

        return services;
    }
}
