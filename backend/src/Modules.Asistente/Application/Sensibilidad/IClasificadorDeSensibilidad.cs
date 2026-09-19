namespace Modules.Asistente.Application;

/// <summary>
/// Resuelve la clasificación de sensibilidad de una columna de resultado a partir
/// de los identificadores que reporta el motor.
/// </summary>
/// <remarks>
/// <b>Por qué identificadores del motor y no nombres.</b> Lo obvio sería comparar
/// los nombres de <see cref="ResultadoDeConsulta.Columnas"/> contra el manifiesto.
/// No funciona: esos nombres son los alias que eligió la consulta generada, no los
/// de las tablas. Un <c>SELECT p.documento AS codigo</c> produce una columna
/// llamada <c>codigo</c>, y un enmascarador que comparara nombres la dejaría pasar
/// entera. No es hipotético: el modelo pone alias en español por su cuenta todo el
/// tiempo, y ninguna instrucción del prompt puede garantizar que no lo haga.
///
/// PostgreSQL emite, para cada columna que sea una referencia directa a una columna
/// de tabla, el identificador de la tabla y el número de atributo. Ese par
/// identifica el origen <b>sin importar el alias</b>, y no lo elige la consulta: lo
/// dice el motor.
/// </remarks>
public interface IClasificadorDeSensibilidad
{
    /// <summary>
    /// Clasifica una columna del resultado.
    /// </summary>
    /// <param name="oidDeTabla">
    /// Identificador de la tabla de origen, o cero si el motor no lo reportó.
    /// </param>
    /// <param name="numeroDeAtributo">
    /// Número de atributo de la columna en esa tabla, o cero si no lo reportó.
    /// </param>
    /// <returns>
    /// La sensibilidad de la columna. Cuando el motor no reporta origen —una
    /// columna calculada— devuelve <see cref="ClasificacionDeSensibilidad.Desconocida"/>.
    /// </returns>
    SensibilidadDeColumna Clasificar(uint oidDeTabla, short numeroDeAtributo);

    /// <summary>Deja la resolución lista, consultando el catálogo si hace falta.</summary>
    Task PrepararAsync(CancellationToken ct);
}

/// <summary>Lo que el enmascarador necesita saber de una columna del resultado.</summary>
/// <param name="Clasificacion">En cuál de las categorías cae.</param>
/// <param name="Etiqueta">
/// Cómo nombrar el dato en el marcador. Presente solo para las
/// <see cref="ClasificacionDeSensibilidad.SensibleValor"/>.
/// </param>
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
