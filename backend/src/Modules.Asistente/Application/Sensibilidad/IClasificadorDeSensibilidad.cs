namespace Modules.Asistente.Application;

/// <summary>
/// Resuelve la clasificación de sensibilidad de una columna de resultado a partir de los
/// identificadores que reporta el motor.
/// </summary>
public interface IClasificadorDeSensibilidad
{
    /// <summary>Clasifica una columna del resultado.</summary>
    SensibilidadDeColumna Clasificar(uint oidDeTabla, short numeroDeAtributo);

    /// <summary>Deja la resolución lista, consultando el catálogo si hace falta.</summary>
    Task PrepararAsync(CancellationToken ct);
}

/// <summary>Lo que el enmascarador necesita saber de una columna del resultado.</summary>
public sealed record SensibilidadDeColumna(
    ClasificacionDeSensibilidad Clasificacion,
    string? Etiqueta = null)
{
    /// <summary>Una columna que viaja tal cual.</summary>
    public static readonly SensibilidadDeColumna Publica =
        new(ClasificacionDeSensibilidad.Publica);

    /// <summary>Una columna cuyo origen el motor no reportó.</summary>
    public static readonly SensibilidadDeColumna Desconocida =
        new(ClasificacionDeSensibilidad.Desconocida);

    /// <summary>Si el valor no puede viajar al modelo tal cual.</summary>
    public bool Tapa =>
        Clasificacion is ClasificacionDeSensibilidad.SensibleValor
            or ClasificacionDeSensibilidad.SensibleTexto;
}
