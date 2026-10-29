using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Designaciones.Infrastructure;
using Modules.Designaciones.Repositories;
using Modules.Designaciones.Services.Integridad;

namespace ArsDocendi.IntegrationTests.Designaciones;

/// <summary>Arma el sellado como lo registra el módulo, con la clave de desarrollo o una explícita.</summary>
internal static class SellosPrueba
{
    public static ClavesSellado Claves(IntegridadOptions? opciones = null, string ambiente = "Development") =>
        new(Options.Create(opciones ?? new IntegridadOptions()), new HostingEnvironment { EnvironmentName = ambiente });

    public static SelladorPedidos Sellador(DesignacionesDbContext db, ClavesSellado? claves = null) =>
        new(new RepositorioSellos(db), claves ?? Claves(), new UnidadDeTrabajo(db), NullLogger<SelladorPedidos>.Instance);

    public static VerificadorIntegridadPedidos Verificador(DesignacionesDbContext db, ClavesSellado? claves = null) =>
        new(new RepositorioSellos(db), claves ?? Claves());
}
