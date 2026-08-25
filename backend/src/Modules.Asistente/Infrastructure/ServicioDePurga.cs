using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Corre la purga de los registros cada tanto, mientras el Host viva.
/// </summary>
/// <remarks>
/// Resuelve la purga desde un scope propio en cada vuelta y no la toma por
/// constructor: <see cref="PurgaDeRegistros"/> depende de la cadena dueña, que es
/// scoped, y capturarla en un singleton la dejaría viva para siempre.
///
/// Un fallo de una vuelta se loguea y no tumba el servicio. Una purga que se cae y
/// se lleva puesto el Host convertiría la retención en un riesgo de
/// disponibilidad, que es justamente al revés de lo que se busca.
/// </remarks>
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
