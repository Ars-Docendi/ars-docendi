using ArsDocendi.Storage.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ArsDocendi.Storage.Infrastructure;

/// <summary>Retira cada hora las cargas abandonadas y los temporales sin referencias.</summary>
internal sealed class LimpiezaArchivos(IServiceScopeFactory alcances, ILogger<LimpiezaArchivos> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var reloj = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await reloj.WaitForNextTickAsync(ct))
        {
            try
            {
                await using var alcance = alcances.CreateAsyncScope();
                var retirados = await alcance.ServiceProvider.GetRequiredService<IAlmacenamientoArchivos>()
                    .LimpiarAsync(DateTimeOffset.UtcNow, ct);
                if (retirados > 0) logger.LogInformation("Limpieza de archivos: {Retirados} objetos retirados", retirados);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogError(error, "La limpieza de archivos falló; se reintenta en una hora");
            }
        }
    }
}
