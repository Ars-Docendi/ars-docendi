namespace Modules.Asistente.Application;

/// <summary>
/// Arma el rechazo cooperativo: qué proponerle a alguien cuya pregunta no se pudo
/// responder (RF-05).
/// </summary>
/// <remarks>
/// Las sugerencias salen del <b>catálogo de ejemplos verificados</b> y no del
/// modelo, por dos motivos que se refuerzan:
///
/// 1. Pedírselas al modelo costaría una llamada más justo en el camino donde el
///    sistema ya decidió que no puede responder.
/// 2. Produciría preguntas que <b>no se sabe si funcionan</b>. Las del catálogo
///    tienen su consulta al lado, ejecutan sin error y pasan el validador: son, por
///    construcción, cosas que el asistente sabe hacer.
///
/// Una sugerencia que no funciona es peor que ninguna: convierte un rechazo honesto
/// en dos rechazos, y el segundo con la pregunta que el propio sistema propuso.
/// </remarks>
public static class Sugerencias
{
    /// <summary>Cuántas se ofrecen como mucho.</summary>
    /// <remarks>
    /// Tres. Una lista larga en un rechazo se lee como un menú y compite con la
    /// explicación de por qué no se pudo responder.
    /// </remarks>
    public const int Cuantas = 3;

    /// <summary>
    /// Elige qué proponer después de un rechazo.
    /// </summary>
    /// <remarks>
    /// Primero por parecido léxico con la pregunta que falló, con el mismo selector
    /// que arma el prompt. Cuando ninguna se parece lo suficiente, el selector
    /// devuelve vacío a propósito, y entonces se toman las primeras del catálogo:
    /// una sugerencia genérica pero ejecutable es mejor que ninguna, y el requisito
    /// pide que <b>siempre</b> haya al menos una.
    /// </remarks>
    public static IReadOnlyList<string> Para(string pregunta, ISelectorDeEjemplos ejemplos)
    {
        ArgumentNullException.ThrowIfNull(ejemplos);

        var parecidos = ejemplos.Elegir(pregunta ?? string.Empty);

        var elegidos = parecidos.Count > 0
            ? parecidos
            : ejemplos.Catalogo;

        return [.. elegidos.Take(Cuantas).Select(ejemplo => ejemplo.Pregunta)];
    }
}
