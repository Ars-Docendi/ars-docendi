namespace Modules.Asistente.Application;

/// <summary>
/// Cuenta las llamadas al modelo de UN turno y las corta en el techo (RNF-10).
/// </summary>
/// <remarks>
/// Es un solo contador para todo el turno, no uno por capa, y esa es la decisión
/// entera. El reintento semántico del pipeline y el de transporte se multiplican:
/// con un techo por capa, «dos por capa» en tres capas son seis llamadas
/// facturadas por pregunta, y cada capa habría respetado su límite.
///
/// El techo se cuenta sobre llamadas al modelo, no sobre requests HTTP. Un
/// reintento de transporte ocurre DENTRO de una llamada y no suma acá: para eso
/// tiene su propio máximo de intentos. Las dos cotas juntas son las que acotan el
/// peor caso.
///
/// Vive con el alcance del request, así que un turno no hereda el conteo de otro.
/// </remarks>
public sealed class ContadorDeLlamadasDelTurno(int techo)
{
    private int _llamadas;

    /// <summary>Llamadas al modelo consumidas en este turno.</summary>
    public int Llamadas => _llamadas;

    /// <summary>Máximo de llamadas al modelo permitidas en el turno.</summary>
    public int Techo { get; } = techo > 0
        ? techo
        : throw new ArgumentOutOfRangeException(
            nameof(techo), techo, "El techo de llamadas por turno tiene que ser positivo.");

    /// <summary>
    /// Reserva una llamada. Falla si el turno ya agotó su techo.
    /// </summary>
    /// <exception cref="TechoDeLlamadasSuperado">Cuando no queda cupo.</exception>
    public void Reservar()
    {
        if (_llamadas >= Techo)
        {
            throw new TechoDeLlamadasSuperado(Techo);
        }

        _llamadas++;
    }
}

/// <summary>
/// El turno pidió más llamadas al modelo de las que su techo permite.
/// </summary>
public sealed class TechoDeLlamadasSuperado(int techo)
    : Exception($"El turno agotó su techo de {techo} llamadas al modelo.")
{
    /// <summary>Techo que se superó.</summary>
    public int Techo { get; } = techo;
}
