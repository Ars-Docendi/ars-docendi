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
internal static class Sugerencias
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

    /// <summary>
    /// Picks up to <see cref="Cuantas"/> follow-up candidates for an answered
    /// (Respondida) turn: same category as the answer, excluding the catalog
    /// entry whose SQL is the one that just ran.
    /// </summary>
    /// <remarks>
    /// This is a separate function next to <see cref="Para"/> and not a
    /// replacement for it: the rejection path still matches by lexical
    /// similarity to the failed question, because there is no "answer" there to
    /// relate to. This one exists only for a turn that produced one.
    ///
    /// Purely in-process and privilege-blind, on purpose: it has no database
    /// connection and cannot know which of its candidates the current actor can
    /// actually execute. That check belongs to whoever calls this (see
    /// <c>ISugerenciasDeSeguimiento</c> in Infrastructure), which runs it only on
    /// the small candidate set this function already narrowed down — never on
    /// the full catalog.
    ///
    /// Deliberately NOT capped at <see cref="Cuantas"/> here: capping before the
    /// executability check could throw away a candidate that would have passed
    /// in favor of one that later fails, leaving fewer than three suggestions
    /// even when a fourth or fifth category match would have qualified. The cap
    /// applies after filtering by what the actor can run, not before.
    /// </remarks>
    /// <param name="categoria">The answered turn's category (GeneracionDeSql.Categoria).</param>
    /// <param name="sqlEjecutado">
    /// The SQL that produced the answer, so the just-answered question is never
    /// suggested back. Comparison is textual (ordinal), matching how the rest of
    /// this module already compares generated SQL against the catalog.
    /// </param>
    /// <param name="catalogo">The full verified example catalog.</param>
    public static IReadOnlyList<EjemploSql> ParaCategoria(
        string categoria, string? sqlEjecutado, IReadOnlyList<EjemploSql> catalogo)
    {
        ArgumentNullException.ThrowIfNull(categoria);
        ArgumentNullException.ThrowIfNull(catalogo);

        return
        [
            .. catalogo
                .Where(ejemplo => string.Equals(ejemplo.Categoria, categoria, StringComparison.Ordinal))
                .Where(ejemplo => sqlEjecutado is null
                    || !string.Equals(ejemplo.Sql, sqlEjecutado, StringComparison.Ordinal)),
        ];
    }
}
