using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Designaciones.Repositories;

namespace Modules.Designaciones.Infrastructure;

/// <summary>Retira cada hora las claves de idempotencia vencidas; sin esto solo se borran al reusarse la clave.</summary>
internal sealed class LimpiezaComandosIdempotentes(IServiceScopeFactory alcances, ILogger<LimpiezaComandosIdempotentes> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var reloj = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await reloj.WaitForNextTickAsync(ct))
        {
            try
            {
                await using var alcance = alcances.CreateAsyncScope();
                var retirados = await alcance.ServiceProvider.GetRequiredService<RepositorioIdempotencia>()
                    .LimpiarVencidosAsync(ct);
                if (retirados > 0) logger.LogInformation("Limpieza de idempotencia: {Retirados} claves vencidas", retirados);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogError(error, "La limpieza de idempotencia falló; se reintenta en una hora");
            }
        }
    }
}
