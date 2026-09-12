namespace Modules.Asistente.Application;

/// <summary>
/// Un par pregunta-consulta verificado del catálogo.
/// </summary>
/// <param name="Pregunta">La pregunta en español, tal como la escribiría alguien.</param>
/// <param name="Sql">La consulta que la responde. Ejecuta sin error y pasa el validador.</param>
/// <param name="Categoria">La dificultad técnica que ilustra.</param>
public sealed record EjemploSql(string Pregunta, string Sql, string Categoria);

/// <summary>
/// Elige del catálogo los ejemplos más parecidos a la pregunta del turno.
/// </summary>
/// <remarks>
/// Similitud léxica y no embeddings: con un catálogo del orden de decenas de
/// ejemplos, un vector store es un servicio más, un modelo de embeddings más y
/// una llamada de red más por turno para elegir entre pocas opciones. Esta
/// implementación corre en proceso, cuesta cero y es inspeccionable —cuando
/// elige mal, se ve por qué—.
///
/// Los ejemplos elegidos van al prompt de <b>usuario</b>. Ponerlos en el prefijo
/// de sistema haría que cada turno pagara escritura de caché sobre el bloque más
/// grande del prompt, porque cambian con la pregunta.
/// </remarks>
public interface ISelectorDeEjemplos
{
    /// <summary>Huella estable del catálogo, para el sellado de reportes.</summary>
    string Huella { get; }

    /// <summary>Todos los ejemplos del catálogo, en el orden en que están escritos.</summary>
    IReadOnlyList<EjemploSql> Catalogo { get; }

    /// <summary>
    /// Devuelve los ejemplos más parecidos, de mayor a menor parecido.
    /// </summary>
    /// <remarks>
    /// Devuelve vacío cuando ninguno alcanza el parecido mínimo. Mandarle al
    /// modelo los ejemplos menos malos de un catálogo que no viene al caso lo
    /// empuja a forzar la pregunta dentro de una forma que no le corresponde,
    /// que es peor que no darle ninguno.
    /// </remarks>
    IReadOnlyList<EjemploSql> Elegir(string pregunta);
}
