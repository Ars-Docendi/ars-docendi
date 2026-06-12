using ArsDocendi.Shared;
using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modules.Aulas;
using Modules.Designaciones;
using Modules.Portal;
using Modules.Tareas;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new() { Title = "Ars Docendi API", Version = "v1" });
});

builder.Services
    .AddArsDocendiShared(builder.Configuration)
    .AddDesignacionesModule(builder.Configuration)
    .AddAulasModule(builder.Configuration)
    .AddPortalModule(builder.Configuration)
    .AddTareasModule(builder.Configuration);

var app = builder.Build();

// Arranque one-shot de migraciones: aplica las migraciones de cada módulo y
// termina con exit 0, sin levantar el web server. Lo invoca la infra de deploy
// (spin-up.sh -> `dotnet ArsDocendi.Host.dll --migrate`). El Host resuelve cada
// módulo solo a través de IMigradorModulo; nunca toca los DbContext internos.
if (args.Contains("--migrate"))
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    foreach (var migrador in scope.ServiceProvider.GetServices<IMigradorModulo>())
    {
        logger.LogInformation("Aplicando migraciones de {Migrador}", migrador.GetType().Name);
        await migrador.MigrarAsync(CancellationToken.None);
    }

    logger.LogInformation("Migraciones aplicadas; el proceso termina sin abrir el listener");
    return;
}

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.Run();
