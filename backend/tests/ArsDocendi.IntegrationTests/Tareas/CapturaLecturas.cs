using System.Collections.Concurrent;
using System.Data.Common;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.Shared.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Modules.Tareas.Infrastructure;

namespace ArsDocendi.IntegrationTests.Tareas;

internal sealed class CapturaLecturas : DbCommandInterceptor
{
    public ConcurrentQueue<string> Sql { get; } = new();
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Sql.Enqueue(command.CommandText);
        return ValueTask.FromResult(result);
    }
}

internal sealed class FabricaLecturas(string cadena) : WebApplicationFactory<Program>
{
    public CapturaLecturas Captura { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:ArsDocendi", cadena);
        builder.UseSetting($"{AutenticacionDesarrolloOptions.Seccion}:Enabled", bool.TrueString);
        builder.ConfigureTestServices(services =>
        {
            services.AddDbContext<IdentityDbContext>((_, options) => options.AddInterceptors(Captura));
            services.AddDbContext<TareasDbContext>((_, options) => options.AddInterceptors(Captura));
        });
    }
}
