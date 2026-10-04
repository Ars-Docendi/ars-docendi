namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Cuántas llamadas al modelo pueden estar en curso a la vez, en todo el proceso
/// (asistente-proveedor-local, design.md D4).
/// </summary>
/// <remarks>
/// Un semáforo con <b>dos colas</b> y no un <c>SemaphoreSlim</c>: una llamada de
/// un turno que ya hizo otra —la redacción después de la generación— entra antes
/// que la primera llamada de un turno nuevo. Con una sola cola FIFO, treinta
/// usuarios que preguntan a la vez intercalan sus llamadas y TODOS los turnos se
/// alargan; con prioridad, los turnos empezados terminan y liberan lugar. El
/// orden dentro de cada cola es de llegada.
///
/// Singleton: la GPU es una sola para todo el proceso. Con dos réplicas del
/// backend contra la misma GPU el límite efectivo se duplica — ver los riesgos
/// del design.
/// </remarks>
internal sealed class CompuertaDelModelo
{
    private readonly Lock _candado = new();
    private readonly LinkedList<TaskCompletionSource> _prioritarias = new();
    private readonly LinkedList<TaskCompletionSource> _nuevas = new();
    private readonly TimeProvider _reloj;
    private int _enCurso;

    public CompuertaDelModelo(int capacidad, TimeProvider reloj)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacidad);
        ArgumentNullException.ThrowIfNull(reloj);

        Capacidad = capacidad;
        _reloj = reloj;
    }

    /// <summary>Cuántas llamadas pueden estar en curso a la vez.</summary>
    public int Capacidad { get; }

    /// <summary>Cuántas llamadas están en curso ahora.</summary>
    public int EnCurso
    {
        get
        {
            lock (_candado)
            {
                return _enCurso;
            }
        }
    }

    /// <summary>Cuántas llamadas esperan lugar ahora.</summary>
    public int EnEspera
    {
        get
        {
            lock (_candado)
            {
                return _prioritarias.Count + _nuevas.Count;
            }
        }
    }

    /// <summary>
    /// Espera un lugar. <c>false</c> si no lo hubo dentro de <paramref name="espera"/>.
    /// </summary>
    /// <remarks>
    /// Quien recibe <c>true</c> DEBE llamar a <see cref="Salir"/> exactamente una
    /// vez. Una cancelación del request se propaga como tal y no ocupa lugar.
    /// </remarks>
    public async Task<bool> EntrarAsync(bool prioritaria, TimeSpan espera, CancellationToken ct)
    {
        TaskCompletionSource turno;
        LinkedListNode<TaskCompletionSource> nodo;

        lock (_candado)
        {
            if (_enCurso < Capacidad && _prioritarias.Count == 0 && _nuevas.Count == 0)
            {
                _enCurso++;
                return true;
            }

            // RunContinuationsAsynchronously: quien libera un lugar lo hace
            // adentro del candado, y la continuación de quien lo recibe no puede
            // correr ahí adentro.
            turno = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            nodo = (prioritaria ? _prioritarias : _nuevas).AddLast(turno);
        }

        try
        {
            await turno.Task.WaitAsync(espera, _reloj, ct);
            return true;
        }
        catch (Exception excepcion) when (excepcion is TimeoutException or OperationCanceledException)
        {
            var cancelado = excepcion is OperationCanceledException;

            lock (_candado)
            {
                // Pudo recibir el lugar justo mientras vencía. Si el request se
                // canceló, el lugar se devuelve —o queda ocupado para siempre—;
                // si sólo venció la espera, el lugar ya es suyo y se usa.
                if (turno.Task.IsCompletedSuccessfully)
                {
                    if (!cancelado)
                    {
                        return true;
                    }

                    LiberarAdentro();
                }
                else
                {
                    nodo.List?.Remove(nodo);
                }
            }

            if (cancelado)
            {
                throw;
            }

            return false;
        }
    }

    /// <summary>Devuelve un lugar; si alguien espera, se lo pasa directamente.</summary>
    public void Salir()
    {
        lock (_candado)
        {
            LiberarAdentro();
        }
    }

    private void LiberarAdentro()
    {
        var siguiente = _prioritarias.First ?? _nuevas.First;

        if (siguiente is null)
        {
            _enCurso--;
            return;
        }

        // El lugar pasa de mano sin bajar el contador: quien lo recibe ya cuenta
        // como en curso.
        siguiente.List!.Remove(siguiente);
        siguiente.Value.TrySetResult();
    }
}
