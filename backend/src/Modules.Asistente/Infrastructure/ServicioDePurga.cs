using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Modules.Asistente.Infrastructure;

/// <summary>Corre la purga de los registros cada tanto, mientras el Host viva.</summary>
internal sealed class ServicioDePurga(
    IServiceScopeFactory scopes,
    IOptions<OpcionesAsistente> opciones,
    TimeProvider reloj,
    ILogger<ServicioDePurga> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var periodo = TimeSpan.FromHours(Math.Max(1, opciones.Value.PeriodoDePurgaHoras));

        using var reloj_ = new PeriodicTimer(periodo, reloj);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var purga = scope.ServiceProvider.GetRequiredService<PurgaDeRegistros>();
                await purga.PurgarAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception excepcion)
            {
                log.LogError(excepcion, "La purga de registros del asistente falló esta vuelta.");
            }

            if (!await reloj_.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                return;
            }
        }
    }
}
