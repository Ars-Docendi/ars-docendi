using ArsDocendi.Host.Administracion;
using ArsDocendi.Host.Api;
using ArsDocendi.Host.Autenticacion;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.Host.Despliegue;
using ArsDocendi.Shared;
using ArsDocendi.Migraciones;
using ArsDocendi.Shared.Identity.Administracion;
using ArsDocendi.Shared.Persistencia;
using ArsDocendi.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modules.Aulas;
using ArsDocendi.Storage;
using ArsDocendi.Inferencia;
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
var modoMigracion = solicitud.Modo != ModoMigracion.Ninguno;
var accesos = builder.AddAutenticacionArsDocendi(
    exigirMicrosoft: builder.Environment.IsProduction() && !modoMigracion);
builder.Services.AddProxyInverso();
builder.Services.AddClavesDataProtection(builder.Configuration);
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
    .AddInferencia(builder.Configuration)
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
        CancellationToken.None,
        scope.ServiceProvider.GetServices<IDiagnosticoIntegridadModulo>().ToArray(),
        builder.Environment.EnvironmentName);

    // El bootstrap escribe datos: sólo después de aplicar correctamente las migraciones,
    // nunca durante estado, preview de scripts ni verificación de recursos sin DB.
    if (solicitud.Modo == ModoMigracion.Migrar && Environment.ExitCode == 0)
    {
        await scope.ServiceProvider.GetRequiredService<ServicioAdministradorInicial>().AsegurarAsync(
            app.Configuration.GetSection(DatosAdministradorInicial.Seccion).Get<DatosAdministradorInicial>(),
            CancellationToken.None);
    }
    return;
}

app.UseForwardedHeaders();
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

if (accesos.Alguno)
{
    app.UseAuthentication();
}
if (accesos.Microsoft)
{
    app.UseAntifalsificacionSesion();
}
app.UseAuthorization();
app.MapControllers();
if (accesos.Desarrollo)
{
    app.MapIdentidadesDesarrollo();
}
if (accesos.Microsoft)
{
    app.MapAutenticacionMicrosoft();
}

app.Run();

public partial class Program;
