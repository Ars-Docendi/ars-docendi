namespace Modules.Asistente.Application;

/// <summary>Lleva la decisión del enrutador sombra desde el paso 5 hasta el registro.</summary>
public sealed class DecisionSombraDelTurno
{
    /// <summary>La intención que el enrutador de dominio eligió, o nulo si no capturó.</summary>
    public string? Intencion { get; private set; }

    /// <summary>Anota qué intención habría capturado el turno.</summary>
    public void Anotar(string intencion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intencion);

        Intencion = intencion;
    }
}
