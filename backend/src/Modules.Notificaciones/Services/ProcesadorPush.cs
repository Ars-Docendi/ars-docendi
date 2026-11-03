using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Notificaciones.Repositories;

namespace Modules.Notificaciones.Services;

/// <summary>
/// Delivers the queued pushes in the background. Best effort: a failure is logged with its type or
/// the push service's status and reason, never with titles, messages or endpoints, and never stops
/// the loop. Each send has its own timeout so one dead endpoint cannot stall the rest for long.
/// </summary>
internal sealed class ProcesadorPush(
    ColaPush cola, IServiceScopeFactory scopes, IEnviadorPush enviador, ILogger<ProcesadorPush> logger)
    : BackgroundService
{
    public static readonly TimeSpan TiempoMaximoPorEnvio = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken detener)
    {
        try
        {
            await foreach (var trabajo in cola.Lector.ReadAllAsync(detener))
            {
                try
                {
                    await ProcesarAsync(trabajo, detener);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !detener.IsCancellationRequested)
                {
                    logger.LogWarning(
                        "Falló el envío de pushes del aviso {TipoEvento} ({TipoError})",
                        trabajo.TipoEvento, ex.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (detener.IsCancellationRequested)
        {
            // Host shutting down.
        }
    }

    internal async Task ProcesarAsync(TrabajoPush trabajo, CancellationToken detener)
    {
        await using var scope = scopes.CreateAsyncScope();
        var repositorio = scope.ServiceProvider.GetRequiredService<RepositorioPush>();

        var usuarios = trabajo.Destinos.Select(d => d.UsuarioId).Distinct().ToArray();
        var desactivaron = await repositorio.ListarQueDesactivaronAsync(usuarios, trabajo.TipoEvento, detener);
        var destinos = trabajo.Destinos.Where(d => !desactivaron.Contains(d.UsuarioId)).ToList();
        if (destinos.Count == 0) return;

        var suscripciones = await repositorio.ListarSuscripcionesAsync(
            destinos.Select(d => d.UsuarioId).Distinct().ToArray(), detener);
        if (suscripciones.Count == 0) return;

        var caducadas = new List<Guid>();
        await Task.WhenAll(suscripciones.Select(async suscripcion =>
        {
            foreach (var destino in destinos.Where(d => d.UsuarioId == suscripcion.UsuarioId))
            {
                using var limite = CancellationTokenSource.CreateLinkedTokenSource(detener);
                limite.CancelAfter(TiempoMaximoPorEnvio);
                var resultado = await enviador.EnviarAsync(
                    suscripcion, CargaPush.Serializar(trabajo, destino.NotificacionId), limite.Token);
                if (resultado == ResultadoEnvioPush.Caducada)
                {
                    lock (caducadas) caducadas.Add(suscripcion.Id);
                }
            }
        }));

        if (caducadas.Count > 0) await repositorio.EliminarSuscripcionesAsync(caducadas, detener);
    }
}
