using Microsoft.Extensions.DependencyInjection;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Infraestructura;

/// <summary>
/// For tests that compose the module without a database: the per-turn resolver
/// reads the stored model selection, and these tests say "there is none" instead of
/// reaching for a database that is not there (asistente-modelo-en-caliente, D3).
/// </summary>
internal static class SeleccionDelModeloFalsa
{
    /// <summary>The module runs on server configuration, as before the runtime selection existed.</summary>
    public static IServiceCollection SinSeleccionGuardada(this IServiceCollection servicios) =>
        servicios.AddScoped<ILectorDeLaSeleccion, SinSeleccion>();

    private sealed class SinSeleccion : ILectorDeLaSeleccion
    {
        public Task<SeleccionDeModelo?> LeerAsync(CancellationToken ct) =>
            Task.FromResult<SeleccionDeModelo?>(null);
    }
}
