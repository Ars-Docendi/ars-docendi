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

    /// <summary>Tokens de entrada facturados en este turno.</summary>
    public int TokensDeEntrada { get; private set; }

    /// <summary>Tokens de salida facturados en este turno.</summary>
    public int TokensDeSalida { get; private set; }

    /// <summary>Si el turno tuvo que volver a generar la consulta.</summary>
    /// <remarks>
    /// Es el reintento <b>semántico</b> del pipeline, no el de transporte: el de
    /// transporte ocurre dentro de una llamada y tiene su propia cota. Se anota acá
    /// porque el registro operativo lo pide y porque este objeto ya es el que sabe
    /// lo que costó el turno.
    /// </remarks>
    public bool HuboReintento { get; private set; }

    /// <summary>Anota lo que facturó una respuesta del modelo.</summary>
    public void Contabilizar(RespuestaDelModelo respuesta)
    {
        ArgumentNullException.ThrowIfNull(respuesta);

        TokensDeEntrada += respuesta.TokensDeEntrada;
        TokensDeSalida += respuesta.TokensDeSalida;
    }

    /// <summary>Deja constancia de que el turno volvió a generar.</summary>
    public void MarcarReintento() => HuboReintento = true;

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
