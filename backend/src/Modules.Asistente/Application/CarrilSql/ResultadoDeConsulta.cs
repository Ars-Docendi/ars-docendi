namespace Modules.Asistente.Application;

/// <summary>
/// Lo que devolvió la ejecución de una consulta generada.
/// </summary>
/// <param name="Columnas">Nombres de las columnas, en el orden en que vinieron.</param>
/// <param name="Filas">Las filas ya recortadas al tope. Nunca incluye la fila sonda.</param>
/// <param name="Truncado">
/// Si hubo más filas que el tope. Es un <b>booleano y no un número</b>: «ves 3 de
/// 124» es un canal de inferencia sobre datos que el usuario no puede ver.
/// </param>
/// <param name="Sensibilidad">
/// La clasificación de cada columna, paralela a <paramref name="Columnas"/>,
/// resuelta contra los identificadores que reporta el motor.
/// </param>
/// <remarks>
/// <b>Por qué <paramref name="Sensibilidad"/> tiene valor por omisión.</b> Es un
/// parámetro agregado después, y omitirlo trata todas las columnas como públicas,
/// que es fail-open. Se acepta únicamente porque el único constructor de
/// producción que lo omite es el del evaluador —que corre contra un fixture
/// sintético, sin datos personales reales— y el que sí importa, el ejecutor, lo
/// pasa siempre. Un test lo fija para que la omisión sea una decisión visible y no
/// un descuido que se propague.
/// </remarks>
public sealed record ResultadoDeConsulta(
    IReadOnlyList<string> Columnas,
    IReadOnlyList<IReadOnlyList<object?>> Filas,
    bool Truncado,
    IReadOnlyList<SensibilidadDeColumna>? Sensibilidad = null)
{
    /// <summary>
    /// Si el resultado no dice nada, en cualquiera de sus dos formas.
    /// </summary>
    /// <remarks>
    /// Reconoce cero filas <b>y</b> la fila única de nulos. Una agregación sobre
    /// un conjunto vacío no devuelve cero filas: devuelve una fila con nulos.
    /// <c>SELECT count(*)</c> sobre nada devuelve una fila con cero —que sí dice
    /// algo—, pero <c>SELECT max(horas)</c> sobre nada devuelve una fila con
    /// <c>NULL</c>. Si el guard solo mirara el conteo, ese caso pasaría como
    /// resultado con datos y la redacción hablaría de un máximo que no existe.
    /// </remarks>
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
