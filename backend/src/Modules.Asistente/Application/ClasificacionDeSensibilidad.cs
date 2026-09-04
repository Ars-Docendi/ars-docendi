namespace Modules.Asistente.Application;

/// <summary>
/// Qué le pasa a una columna cuando el resultado va camino al proveedor del modelo.
/// </summary>
/// <remarks>
/// Es una frontera de <b>salida</b>, y es distinta de la que ya impone el motor.
/// Los <c>GRANT</c> por columna deciden quién puede <b>leer</b> qué, y eso lo
/// hace cumplir PostgreSQL. Esto decide qué <b>sale hacia un tercero</b>, y no
/// puede imponerlo el motor porque el motor no sabe qué hacemos con las filas
/// después de devolverlas.
/// </remarks>
public enum ClasificacionDeSensibilidad
{
    /// <summary>Viaja al modelo tal cual.</summary>
    Publica,

    /// <summary>
    /// Al modelo va un marcador estable; el valor real sigue viaje al llamador.
    /// </summary>
    SensibleValor,

    /// <summary>
    /// No viaja al modelo en absoluto: se suprime la columna entera, nombre
    /// incluido.
    /// </summary>
    SensibleTexto,

    /// <summary>
    /// El motor no reportó de qué columna viene, así que no se la pudo clasificar.
    /// </summary>
    /// <remarks>
    /// Pasa con toda columna que no sea una referencia directa a una columna de
    /// tabla: <c>count(*)</c>, <c>documento || ''</c>, <c>substring(telefono, 1, 4)</c>.
    ///
    /// Se trata como pública, y es una decisión tomada, no un olvido. Enmascarar
    /// todo origen desconocido rompería <c>count(*)</c>, que es la forma más común
    /// de consulta agregada, para cubrir un caso que exige que el modelo
    /// activamente envuelva una columna personal en una expresión.
    ///
    /// Lo que acota el riesgo: esas columnas solo son legibles con la conexión de
    /// datos personales, que exige permiso <b>y</b> alcance global. Un actor sin
    /// ella no puede construir la expresión aunque quiera — el motor rechaza la
    /// consulta antes de ejecutarla. Queda registrado como TD-009.
    /// </remarks>
    Desconocida,
}
