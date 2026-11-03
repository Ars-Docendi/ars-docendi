namespace Modules.Asistente.Infrastructure;

/// <summary>Un valor que se calcula una sola vez, aunque lo pidan varios turnos a la vez.</summary>
internal sealed class ValorPerezoso<T>
    where T : class
{
    private readonly SemaphoreSlim _turnoDeCalculo = new(1, 1);
    private volatile T? _valor;

    /// <summary>Cuántas veces hubo que calcularlo. Lo miran los tests del caché.</summary>
    internal int Calculos { get; private set; }

    /// <summary>El valor si ya está, o <c>null</c>. No lo calcula.</summary>
    internal T? Calculado => _valor;

    public async Task<T> ObtenerAsync(Func<CancellationToken, Task<T>> calcular, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(calcular);

        var yaEsta = _valor;
        if (yaEsta is not null)
        {
            return yaEsta;
        }

        await _turnoDeCalculo.WaitAsync(ct);
        try
        {
            // Segunda comprobación: entre la primera y el semáforo pudo haber
            // calculado otro.
            if (_valor is not null)
            {
                return _valor;
            }

            Calculos++;
            var recien = await calcular(ct);
            _valor = recien;

            return recien;
        }
        finally
        {
            _turnoDeCalculo.Release();
        }
    }
}

/// <summary>Dos valores perezosos, uno por rol de lectura.</summary>
internal sealed class ValorPerezosoPorRol<T>
    where T : class
{
    private readonly ValorPerezoso<T> _basico = new();
    private readonly ValorPerezoso<T> _conDatosPersonales = new();

    /// <summary>Cuántas veces hubo que calcular, sumando los dos roles.</summary>
    internal int Calculos => _basico.Calculos + _conDatosPersonales.Calculos;

    public Task<T> ObtenerAsync(
        bool conDatosPersonales,
        Func<CancellationToken, Task<T>> calcular,
        CancellationToken ct) =>
        (conDatosPersonales ? _conDatosPersonales : _basico).ObtenerAsync(calcular, ct);
}
