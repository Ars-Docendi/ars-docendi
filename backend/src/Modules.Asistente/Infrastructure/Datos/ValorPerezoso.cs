namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Un valor que se calcula una sola vez, aunque lo pidan varios turnos a la vez.
/// </summary>
/// <typeparam name="T">Lo que se calcula. De referencia: el nulo es la marca de
/// «todavía no».</typeparam>
/// <remarks>
/// El módulo tenía cinco copias de esta doble comprobación —índice de entidades,
/// catálogo de sensibilidad, proveedor de esquema, catálogo del dominio y caché de
/// capacidades—, cada una con su semáforo, su campo nulo y su contador.
///
/// <b>Una de las cinco tenía un defecto de concurrencia real</b>, y es la razón de
/// que esto exista y no sea sólo higiene: leía un <c>Dictionary</c> con
/// <c>TryGetValue</c> FUERA del semáforo mientras otro hilo podía estar
/// escribiéndolo adentro. Un diccionario no admite esa combinación: la lectura
/// puede devolver basura o tirar. Con un valor por rol y ninguna colección
/// compartida, esa forma no se puede volver a escribir.
///
/// El campo es <c>volatile</c> a propósito. La comprobación rápida corre sin
/// tomar el semáforo, así que hace falta la barrera: sin ella, un hilo podría ver
/// la referencia publicada antes de que el objeto que apunta esté completamente
/// escrito.
///
/// <c>Calculos</c> cuenta <b>veces que hubo que calcular</b> y no «veces que se
/// consultó la base». En este tipo son lo mismo porque el cálculo es siempre una
/// lectura, pero el nombre dice lo que el tipo sabe.
/// </remarks>
internal sealed class ValorPerezoso<T>
    where T : class
{
    private readonly SemaphoreSlim _turnoDeCalculo = new(1, 1);
    private volatile T? _valor;

    /// <summary>Cuántas veces hubo que calcularlo. Lo miran los tests del caché.</summary>
    internal int Calculos { get; private set; }

    /// <summary>El valor si ya está, o <c>null</c>. No lo calcula.</summary>
    /// <remarks>
    /// Existe para los consumidores que exponen una lectura sincrónica después de
    /// una preparación explícita: el que la usa tiene que decidir qué hacer con el
    /// nulo, y esa decisión es suya y no de este tipo.
    /// </remarks>
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

/// <summary>
/// Dos valores perezosos, uno por rol de lectura.
/// </summary>
/// <remarks>
/// Los roles del asistente son exactamente dos —con y sin datos personales— y sus
/// privilegios no cambian en runtime, así que lo que se cachea por rol son dos
/// cosas y no una colección. Escribirlo como dos campos y no como un diccionario
/// es lo que hace <b>estructuralmente imposible</b> el defecto de concurrencia que
/// este renglón cerró.
/// </remarks>
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
