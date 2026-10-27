namespace ArsDocendi.IntegrationTests.Infraestructura;

/// <summary>
/// Un <see cref="TimeProvider"/> con reloj movible, para los tests que dependen del
/// paso del tiempo.
/// </summary>
/// <remarks>
/// Existe para poder verificar la expiración del hilo sin esperar dos horas. Un
/// test que dependiera del reloj real sería lento o mentiroso, y probablemente las
/// dos cosas.
///
/// También maneja sus propios temporizadores, y eso no es un adorno: el presupuesto
/// del turno y el timeout de una llamada al modelo se arman con
/// <c>CancellationTokenSource(delay, TimeProvider)</c>, que pide el temporizador
/// acá. Sin esta parte, <see cref="Avanzar"/> movería la hora sin disparar nada y
/// un test de timeout tendría que esperar los treinta segundos de verdad.
/// </remarks>
public sealed class RelojFijo(DateTimeOffset inicio) : TimeProvider
{
    private readonly List<Temporizador> _temporizadores = [];
    private readonly Lock _candado = new();
    private DateTimeOffset _ahora = inicio;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_candado)
        {
            return _ahora;
        }
    }

    public override ITimer CreateTimer(
        TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);

        var temporizador = new Temporizador(this, callback, state);

        lock (_candado)
        {
            temporizador.Reprogramar(_ahora, dueTime, period);
            _temporizadores.Add(temporizador);
        }

        return temporizador;
    }

    /// <summary>
    /// Adelanta el reloj y dispara lo que haya vencido.
    /// </summary>
    /// <remarks>
    /// Los callbacks se invocan <b>fuera</b> del candado: uno de ellos cancela un
    /// <see cref="CancellationTokenSource"/>, que a su vez libera su temporizador y
    /// volvería a entrar acá. Con el candado tomado, eso sería un interbloqueo.
    /// </remarks>
    public void Avanzar(TimeSpan cuanto)
    {
        List<Temporizador> vencidos;

        lock (_candado)
        {
            _ahora += cuanto;
            vencidos = [.. _temporizadores.Where(t => t.VenceEn(_ahora))];

            foreach (var temporizador in vencidos)
            {
                temporizador.Consumir(_ahora);
            }

            _temporizadores.RemoveAll(t => t.Terminado);
        }

        foreach (var temporizador in vencidos)
        {
            temporizador.Disparar();
        }
    }

    private void Olvidar(Temporizador temporizador)
    {
        lock (_candado)
        {
            _temporizadores.Remove(temporizador);
        }
    }

    private sealed class Temporizador(RelojFijo reloj, TimerCallback callback, object? state) : ITimer
    {
        private DateTimeOffset? _proximo;
        private TimeSpan _periodo = Timeout.InfiniteTimeSpan;

        public bool Terminado => _proximo is null;

        public bool VenceEn(DateTimeOffset ahora) => _proximo is { } cuando && cuando <= ahora;

        public void Reprogramar(DateTimeOffset ahora, TimeSpan dueTime, TimeSpan period)
        {
            _periodo = period;
            _proximo = dueTime == Timeout.InfiniteTimeSpan ? null : ahora + dueTime;
        }

        /// <summary>Marca el disparo y reprograma si es periódico.</summary>
        public void Consumir(DateTimeOffset ahora) =>
            _proximo = _periodo == Timeout.InfiniteTimeSpan || _periodo <= TimeSpan.Zero
                ? null
                : ahora + _periodo;

        public void Disparar() => callback(state);

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            Reprogramar(reloj.GetUtcNow(), dueTime, period);
            return true;
        }

        public void Dispose() => reloj.Olvidar(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
