namespace Modules.Asistente.Application;

/// <summary>
/// La cota de tiempo del turno, medida punta a punta (RNF-09).
/// </summary>
/// <remarks>
/// <b>Es una sola cota, no la suma de las de cada etapa.</b> Agregar un timeout por
/// llamada al modelo no alcanza: cuatro llamadas de diez segundos son cuarenta
/// segundos de espera, y cada una habría respetado su límite. El usuario espera el
/// total, no el máximo.
///
/// Las etapas conservan sus propios timeouts —el de sentencia libera el backend de
/// la base, cosa que cancelar el token no hace—, pero ninguno de ellos es la cota
/// del turno.
///
/// <see cref="Vencio"/> es lo que distingue «se acabó el presupuesto» de «el
/// usuario cerró la pestaña». Sin esa distinción, cada abandono se registraría como
/// una degradación del servicio y la métrica de disponibilidad mentiría.
/// </remarks>
public sealed class PresupuestoDelTurno : IDisposable
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

    /// <summary>
    /// Abre el presupuesto de un turno.
    /// </summary>
    /// <param name="delRequest">Token del request; cancelarlo cancela el turno.</param>
    /// <param name="presupuesto">
    /// Cuánto dura el turno como mucho. Cero o menos lo deja sin cota, que es lo que
    /// necesitan los tests que miden otra cosa.
    /// </param>
    /// <param name="reloj">
    /// De dónde sale el temporizador. Inyectado para que un test pueda adelantar el
    /// tiempo en vez de esperarlo.
    /// </param>
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
