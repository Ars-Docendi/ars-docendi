using ArsDocendi.Shared.Auditing;
using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArsDocendi.Shared;

public static class DependencyInjection
{
    public static IServiceCollection AddArsDocendiShared(this IServiceCollection services, IConfiguration _)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<AuditDbConnectionInterceptor>();
        return services;
    }
}
