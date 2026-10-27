namespace Modules.Asistente.Application;

/// <summary>
/// Qué le pasa a una columna cuando el resultado va camino al proveedor del modelo. Los
/// <c>GRANT</c> por columna deciden quién puede <b>leer</b> qué, y eso lo hace cumplir PostgreSQL.
/// </summary>
public enum ClasificacionDeSensibilidad
{
    /// <summary>Viaja al modelo tal cual.</summary>
    Publica,

    /// <summary>Al modelo va un marcador estable; el valor real sigue viaje al llamador.</summary>
    SensibleValor,

    /// <summary>
    /// No viaja al modelo en absoluto: se suprime la columna entera, nombre incluido.
    /// </summary>
    SensibleTexto,

    /// <summary>
    /// El motor no reportó de qué columna viene, así que no se la pudo clasificar.
    /// </summary>
    Desconocida,
}
