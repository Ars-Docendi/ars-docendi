namespace Modules.Asistente.Application;

/// <summary>Lo que devolvió la ejecución de una consulta generada.</summary>
public sealed record ResultadoDeConsulta(
    IReadOnlyList<string> Columnas,
    IReadOnlyList<IReadOnlyList<object?>> Filas,
    bool Truncado,
    IReadOnlyList<SensibilidadDeColumna>? Sensibilidad = null)
{
    /// <summary>Si el resultado no dice nada, en cualquiera de sus dos formas.</summary>
    public bool EstaVacio =>
        Filas.Count == 0 || (Filas.Count == 1 && Filas[0].All(valor => valor is null));

    /// <summary>Si alguna columna no puede viajar al modelo tal cual.</summary>
    public bool TieneColumnasTapadas =>
        Sensibilidad is not null && Sensibilidad.Any(columna => columna.Tapa);

    /// <summary>Clasificación de una columna por su posición.</summary>
    public SensibilidadDeColumna SensibilidadDe(int indice) =>
        Sensibilidad is not null && indice < Sensibilidad.Count
            ? Sensibilidad[indice]
            : SensibilidadDeColumna.Publica;
}
