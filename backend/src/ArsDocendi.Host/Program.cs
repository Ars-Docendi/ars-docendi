using ArsDocendi.Shared;
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

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.Run();
