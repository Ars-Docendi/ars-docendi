using ArsDocendi.Host.Administracion;
using ArsDocendi.Host.Api;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.Shared;
using ArsDocendi.Migraciones;
using ArsDocendi.Shared.Persistencia;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modules.Aulas;
using ArsDocendi.Storage;
using Modules.Designaciones;
using Modules.Portal;
using Modules.Tareas;
using Serilog;

SolicitudMigracion solicitud;
try { solicitud = SolicitudMigracion.Parsear(args); }
catch (ArgumentException)
{
    Console.Error.WriteLine("Argumentos de migraciones inválidos; elegir un modo único y una ruta segura.");
    Environment.ExitCode = 2;
    return;
}
var builder = WebApplication.CreateBuilder(solicitud.Modo == ModoMigracion.Ninguno ? args : []);

builder.Host.UseSerilog((ctx, lc) =>
{
    if (solicitud.Modo != ModoMigracion.Ninguno)
        lc.MinimumLevel.Fatal().WriteTo.Console(standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose);
    else
        lc.ReadFrom.Configuration(ctx.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console();
});

builder.Services.AddControllers();
builder.Services.AddProblemDetails();

// Orígenes del front (Vite en desarrollo). Sin configuración no se habilita ninguno.
var origenesPermitidos = builder.Configuration.GetSection("Cors:OrigenesPermitidos").Get<string[]>() ?? [];
if (origenesPermitidos.Length > 0)
{
    builder.Services.AddCors(opciones => opciones.AddDefaultPolicy(politica =>
        politica.WithOrigins(origenesPermitidos).AllowAnyHeader().AllowAnyMethod()));
}
builder.Services.AddExceptionHandler<ManejadorExcepcionesApi>();
builder.Services.AddScoped<ServicioDocentes>();
builder.Services.AddScoped<ServicioUsuariosAdministracion>();
builder.Services.AddScoped<ResolutorAlcanceDocentes>();
builder.Services.AddScoped<IRepositorioEstadoSistema, RepositorioEstadoSistema>();
builder.Services.AddScoped<ServicioEstadoSistema>();
builder.Services.AddScoped<IRepositorioAuditoria, RepositorioAuditoria>();
builder.Services.AddScoped<ServicioAuditoria>();
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
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new() { Title = "Ars Docendi API", Version = "v1" });
    o.CustomSchemaIds(tipo => tipo.FullName!.Replace('+', '.'));
});

builder.Services
    .AddArsDocendiShared(builder.Configuration)
    .AddMigracionesIdentity()
    .AddAlmacenamientoModule(builder.Configuration)
    .AddDesignacionesModule(builder.Configuration)
    .AddAulasModule(builder.Configuration)
    .AddPortalModule(builder.Configuration)
    .AddTareasModule(builder.Configuration);

var app = builder.Build();

// Todos los modos CLI terminan sin listener; el Host sólo consume contratos públicos.
if (solicitud.Modo != ModoMigracion.Ninguno)
{
    using var scope = app.Services.CreateScope();
    Environment.ExitCode = await RunnerMigraciones.EjecutarAsync(solicitud,
        scope.ServiceProvider.GetServices<IMigradorModulo>().ToArray(),
        builder.Configuration.GetConnectionString("ArsDocendi") ?? string.Empty,
        Environment.GetEnvironmentVariable("RELEASE_SHA")
            ?? Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "local",
        CancellationToken.None);
    return;
}

app.UseSerilogRequestLogging();
app.UseExceptionHandler();

if (origenesPermitidos.Length > 0)
{
    app.UseCors();
}

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
