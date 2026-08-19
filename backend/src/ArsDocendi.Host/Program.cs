using ArsDocendi.Shared;
using ArsDocendi.Shared.Persistencia;
using ArsDocendi.Host.Api;
using ArsDocendi.Host.Administracion;
using ArsDocendi.Host.Desarrollo;
using Microsoft.AspNetCore.Authentication;
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
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ManejadorExcepcionesApi>();
builder.Services.AddScoped<ServicioDocentes>();
var autenticacionDesarrolloHabilitada = !builder.Environment.IsProduction()
    && builder.Configuration.GetValue<bool>($"{AutenticacionDesarrolloOptions.Seccion}:Enabled");
if (autenticacionDesarrolloHabilitada)
{
    builder.Services
        .AddAuthentication(AutenticacionDesarrolloHandler.Esquema)
        .AddScheme<AuthenticationSchemeOptions, AutenticacionDesarrolloHandler>(
            AutenticacionDesarrolloHandler.Esquema, _ => { });
}
builder.Services.AddAuthorization(opciones =>
{
    foreach (var permiso in ArsDocendi.Shared.Auth.Permisos.Todos)
    {
        opciones.AddPolicy(permiso, politica =>
            politica.RequireClaim(ArsDocendi.Shared.Auth.Permisos.Claim, permiso));
    }
    opciones.AddPolicy(ArsDocendi.Shared.Auth.Permisos.DesignacionesRevisar, politica =>
        politica.RequireAssertion(contexto => contexto.User.Claims.Any(c =>
            c.Type == ArsDocendi.Shared.Auth.Permisos.Claim
            && (c.Value == ArsDocendi.Shared.Auth.Permisos.DesignacionesAprobarCoordinacion
                || c.Value == ArsDocendi.Shared.Auth.Permisos.DesignacionesAprobarSecretaria
                || c.Value == ArsDocendi.Shared.Auth.Permisos.DesignacionesAprobarDecanato))));
});
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
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (autenticacionDesarrolloHabilitada)
{
    app.UseAuthentication();
}
app.UseAuthorization();
app.MapControllers();
if (autenticacionDesarrolloHabilitada)
{
    app.MapIdentidadesDesarrollo();
}

app.Run();

public partial class Program;
