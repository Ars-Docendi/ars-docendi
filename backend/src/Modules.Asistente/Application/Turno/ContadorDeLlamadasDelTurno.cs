namespace Modules.Asistente.Application;

/// <summary>Cuenta las llamadas al modelo de UN turno y las corta en el techo (RNF-10).</summary>
public sealed class ContadorDeLlamadasDelTurno(int techo)
{
    private int _llamadas;

    /// <summary>Llamadas al modelo consumidas en este turno.</summary>
    public int Llamadas => _llamadas;

    /// <summary>Tokens de entrada facturados en este turno.</summary>
    public int TokensDeEntrada { get; private set; }

    /// <summary>Tokens de salida facturados en este turno.</summary>
    public int TokensDeSalida { get; private set; }

    /// <summary>Cuántos de los tokens de entrada del turno los sirvió la caché.</summary>
    public int TokensDeCache { get; private set; }

    /// <summary>Si el turno tuvo que volver a generar la consulta.</summary>
    public bool HuboReintento { get; private set; }

    /// <summary>Anota lo que facturó una respuesta del modelo.</summary>
    public void Contabilizar(RespuestaDelModelo respuesta)
    {
        ArgumentNullException.ThrowIfNull(respuesta);

        TokensDeEntrada += respuesta.TokensDeEntrada;
        TokensDeSalida += respuesta.TokensDeSalida;
        TokensDeCache += respuesta.TokensDeCache;
    }

    /// <summary>Deja constancia de que el turno volvió a generar.</summary>
    public void MarcarReintento() => HuboReintento = true;

    /// <summary>Máximo de llamadas al modelo permitidas en el turno.</summary>
    public int Techo { get; } = techo > 0
        ? techo
        : throw new ArgumentOutOfRangeException(
            nameof(techo), techo, "El techo de llamadas por turno tiene que ser positivo.");

    /// <summary>Reserva una llamada. Falla si el turno ya agotó su techo.</summary>
    public void Reservar()
    {
        if (_llamadas >= Techo)
        {
            throw new TechoDeLlamadasSuperado(Techo);
        }

        _llamadas++;
    }
}

/// <summary>El turno pidió más llamadas al modelo de las que su techo permite.</summary>
internal sealed class TechoDeLlamadasSuperado(int techo)
    : Exception($"El turno agotó su techo de {techo} llamadas al modelo.")
{
    /// <summary>Techo que se superó.</summary>
    public int Techo { get; } = techo;
}
