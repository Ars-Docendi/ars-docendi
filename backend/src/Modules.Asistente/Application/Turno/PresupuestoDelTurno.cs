namespace Modules.Asistente.Application;

/// <summary>La cota de tiempo del turno, medida punta a punta (RNF-09).</summary>
internal sealed class PresupuestoDelTurno : IDisposable
{
    private readonly CancellationToken _delRequest;
    private readonly CancellationTokenSource? _propio;
    private readonly CancellationTokenSource _enlazado;

    private PresupuestoDelTurno(
        CancellationToken delRequest, CancellationTokenSource? propio, CancellationTokenSource enlazado)
    {
        _delRequest = delRequest;
        _propio = propio;
        _enlazado = enlazado;
    }

    /// <summary>El token que se propaga a todo el pipeline.</summary>
    public CancellationToken Token => _enlazado.Token;

    /// <summary>
    /// Si el turno se cortó por agotar su presupuesto y no porque el usuario se fue.
    /// </summary>
    public bool Vencio => _propio is { IsCancellationRequested: true }
        && !_delRequest.IsCancellationRequested;

    /// <summary>Abre el presupuesto de un turno.</summary>
    public static PresupuestoDelTurno Abrir(
        CancellationToken delRequest, TimeSpan presupuesto, TimeProvider reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (presupuesto <= TimeSpan.Zero)
        {
            return new PresupuestoDelTurno(
                delRequest, null, CancellationTokenSource.CreateLinkedTokenSource(delRequest));
        }

        var propio = new CancellationTokenSource(presupuesto, reloj);

        return new PresupuestoDelTurno(
            delRequest,
            propio,
            CancellationTokenSource.CreateLinkedTokenSource(delRequest, propio.Token));
    }

    public void Dispose()
    {
        _enlazado.Dispose();
        _propio?.Dispose();
    }
}
