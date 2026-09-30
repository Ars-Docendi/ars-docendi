using ArsDocendi.Host.Administracion;
using ArsDocendi.Host.Api;
using ArsDocendi.Host.Auditoria;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.Shared;
using ArsDocendi.Shared.Auditing;
using ArsDocendi.Shared.Persistencia;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modules.Aulas;
using ArsDocendi.Storage;
using Modules.Designaciones;
using Modules.Portal;
using Modules.Tareas;
using Npgsql;
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
    .AddAlmacenamientoModule(builder.Configuration)
    .AddDesignacionesModule(builder.Configuration)
    .AddAulasModule(builder.Configuration)
    .AddPortalModule(builder.Configuration)
    .AddTareasModule(builder.Configuration);

var app = builder.Build();

var salidaVerificacion = await JobsVerificacionAuditoria.EjecutarAsync(
    args, app.Configuration, app.Services.GetRequiredService<ILogger<Program>>(), CancellationToken.None);
if (salidaVerificacion is not null)
{
    Environment.ExitCode = salidaVerificacion.Value;
    return;
}

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

// Job one-shot: no abre HTTP, usa credenciales DB y servicios externos dedicados.
if (args.Contains("--sellar-auditoria"))
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    var cadenaDatos = app.Configuration.GetConnectionString("AuditSeal")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:AuditSeal para el job de sellado.");
    var ambienteSellado = app.Configuration["AuditoriaSellado:Ambiente"]
        ?? throw new InvalidOperationException("Falta AuditoriaSellado:Ambiente.");
    var maxEventos = app.Configuration.GetValue("AuditoriaSellado:MaxEventosPorLote", 1000);
    var opcionesSellado = new OpcionesPublicacionLotes(
        new Uri(ExigirConfiguracion("AuditoriaSellado:FirmadorEndpoint")),
        ExigirConfiguracion("AuditoriaSellado:FirmadorToken"),
        new Uri(ExigirConfiguracion("AuditoriaSellado:TestigoPrimarioEndpoint")),
        ExigirConfiguracion("AuditoriaSellado:TestigoPrimarioToken"),
        new Uri(ExigirConfiguracion("AuditoriaSellado:TestigoSecundarioEndpoint")),
        ExigirConfiguracion("AuditoriaSellado:TestigoSecundarioToken"));

    await using var dataSource = NpgsqlDataSource.Create(cadenaDatos);
    using var http = new HttpClient(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5)
    }) { Timeout = TimeSpan.FromSeconds(30) };
    var claves = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
        ExigirConfiguracion("AuditoriaSellado:ClavesPublicasJson"))
        ?? throw new InvalidOperationException("Faltan claves públicas de auditoría.");
    var transiciones = System.Text.Json.JsonSerializer.Deserialize<List<TransicionClaveFirma>>(
        app.Configuration["AuditoriaSellado:TransicionesJson"] ?? "[]");
    var tokenLecturaPrimario = ExigirConfiguracion("AuditoriaSellado:TestigoPrimarioLecturaToken");
    var tokenLecturaSecundario = ExigirConfiguracion("AuditoriaSellado:TestigoSecundarioLecturaToken");
    if (tokenLecturaPrimario == tokenLecturaSecundario
        || tokenLecturaPrimario == opcionesSellado.TokenTestigoPrimario
        || tokenLecturaPrimario == opcionesSellado.TokenTestigoSecundario
        || tokenLecturaSecundario == opcionesSellado.TokenTestigoPrimario
        || tokenLecturaSecundario == opcionesSellado.TokenTestigoSecundario)
        throw new InvalidOperationException("Los lectores requieren credenciales distintas de las de escritura.");
    var lector = new VerificadorTestigosRemotos(http, new(
        new Uri(ExigirConfiguracion("AuditoriaSellado:TestigoPrimarioLecturaEndpoint")),
        tokenLecturaPrimario,
        new Uri(ExigirConfiguracion("AuditoriaSellado:TestigoSecundarioLecturaEndpoint")),
        tokenLecturaSecundario, claves, transiciones));
    var publicador = new PublicadorRemotoLotes(
        new PreparadorLotesAuditoria(dataSource), http, opcionesSellado,
        new CompuertaPublicacionAuditoria(dataSource, lector));
    var resultado = await publicador.PublicarSiguienteAsync(
        ambienteSellado, maxEventos, CancellationToken.None);
    logger.LogInformation(
        "Job de auditoría finalizado. SinTrabajo={SinTrabajo} Ocupado={Ocupado} DobleCustodia={DobleCustodia} LoteId={LoteId} UltimaSecuencia={UltimaSecuencia}",
        resultado.SinTrabajo, resultado.Ocupado, resultado.DobleCustodiaRegistrada,
        resultado.LoteId, resultado.UltimaSecuencia);
    return;
}

string ExigirConfiguracion(string clave) =>
    app.Configuration[clave] is { Length: > 0 } valor
        ? valor
        : throw new InvalidOperationException($"Falta la configuración requerida {clave}.");

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
