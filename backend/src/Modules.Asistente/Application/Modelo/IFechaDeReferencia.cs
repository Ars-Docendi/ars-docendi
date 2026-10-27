namespace Modules.Asistente.Application;

/// <summary>Resuelve el «hoy» del turno (RF-18).</summary>
public interface IFechaDeReferencia
{
    /// <summary>El día que el turno considera «hoy».</summary>
    DateOnly Hoy();
}

/// <summary>
/// Fecha de referencia fija: la del turno en producción (resuelta una vez, al
/// abrirse el request), y la del dataset en evaluación y en tests.
/// </summary>
internal sealed class FechaDeReferenciaFija(DateOnly fecha) : IFechaDeReferencia
{
    public DateOnly Hoy() => fecha;
}
