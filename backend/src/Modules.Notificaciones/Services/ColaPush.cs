using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Modules.Notificaciones.Services;

/// <summary>
/// In-memory hand-off between <c>Notificador</c> and the push worker, so that a business request
/// never waits for a push service. Bounded: when full the job is dropped and counted, never queued
/// without limit. A crash loses the queued jobs; the in-app notice is the durable record.
/// </summary>
internal sealed class ColaPush(ConfiguracionWebPush configuracion, ILogger<ColaPush> logger)
{
    public const int Capacidad = 1000;

    private readonly Channel<TrabajoPush> canal = Channel.CreateBounded<TrabajoPush>(
        new BoundedChannelOptions(Capacidad) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    public bool Habilitada => configuracion.Habilitada;

    public ChannelReader<TrabajoPush> Lector => canal.Reader;

    /// <summary>Never blocks and never throws. Does nothing when Web Push is disabled.</summary>
    public void Encolar(TrabajoPush trabajo)
    {
        if (!Habilitada) return;
        if (!canal.Writer.TryWrite(trabajo))
            logger.LogWarning("Cola de push llena: se descartó el envío del aviso {TipoEvento}", trabajo.TipoEvento);
    }
}
