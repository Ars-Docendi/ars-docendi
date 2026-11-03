using Microsoft.Extensions.Logging;

namespace ArsDocendi.Shared.Aplicacion;

/// <summary>
/// Runs a side effect that happens AFTER a business action committed (for example emitting a
/// notification) so that nothing it does can reach the action: any failure, including the lookups
/// needed to build the effect, is logged and swallowed. The log carries the event name and the
/// exception type only, never messages or data, which can contain personal information.
/// </summary>
public static class EmisionPosterior
{
    public static async Task IntentarAsync(ILogger logger, string evento, Func<Task> emitir)
    {
        try
        {
            await emitir();
        }
        catch (Exception ex)
        {
            logger.LogError(
                "No se pudo emitir {Evento} después de confirmar la acción; se pierde el aviso ({TipoError})",
                evento, ex.GetType().Name);
        }
    }
}
