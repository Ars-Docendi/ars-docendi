using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Modules.Asistente;

/// <summary>
/// States at startup whether redaction masks <c>sensible-valor</c> values
/// (asistente-redaccion-sin-enmascarado-local, design.md D6).
/// </summary>
/// <remarks>
/// It is what an operator can read without inspecting prompts. It takes the
/// options already bound instead of <c>IOptions</c>: reading them through
/// <c>IOptions</c> would trigger validation at startup, and the module decided
/// that an invalid configuration fails on the first turn rather than taking the
/// Host down.
/// </remarks>
internal sealed class AnuncioDeLaRedaccion(
    OpcionesAsistente opciones, ILogger<AnuncioDeLaRedaccion> log) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (opciones.RedaccionSinEnmascararVigente)
        {
            log.LogInformation(
                "Redaction runs WITHOUT masking sensible-valor values (provider {Provider}, no cassette directory).",
                opciones.Proveedor);
        }
        else
        {
            log.LogInformation("Redaction masks sensible-valor values.");
        }

        if (opciones.RedaccionSinEnmascarar && !opciones.RedaccionSinEnmascararVigente)
        {
            log.LogWarning(
                "{Option} is set but not in effect, so redaction keeps masking: it needs provider {LocalProvider} (current: {Provider}) and no cassette directory (set: {HasCassettes}).",
                nameof(OpcionesAsistente.RedaccionSinEnmascarar),
                Infrastructure.ProveedorLocal.Clave,
                opciones.Proveedor,
                !string.IsNullOrWhiteSpace(opciones.DirectorioDeCassettes));
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
