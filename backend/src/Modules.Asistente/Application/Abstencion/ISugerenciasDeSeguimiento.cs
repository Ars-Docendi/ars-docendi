namespace Modules.Asistente.Application;

/// <summary>
/// Picks the follow-up suggestions offered after a successfully answered
/// (Respondida) turn.
/// </summary>
/// <remarks>
/// A separate port from <see cref="ISelectorDeEjemplos"/> on purpose:
/// <see cref="ISelectorDeEjemplos"/> is a pure, privilege-blind lexical matcher,
/// and this one has to run the actor-scoped executability check
/// (<c>CatalogoDeCapacidades.EjecutableAsync</c> in Infrastructure) that
/// <c>GET /api/asistente/capacidades</c> already applies to its own examples —
/// the one thing this feature must never do is suggest a question the actor
/// cannot run.
/// </remarks>
public interface ISugerenciasDeSeguimiento
{
    /// <summary>
    /// Up to <see cref="Sugerencias.Cuantas"/> follow-up questions in the same
    /// category as the answered turn, that the actor can execute. Empty when
    /// none qualify — never a generic fallback.
    /// </summary>
    /// <param name="actor">The turn's actor, to scope the executability check.</param>
    /// <param name="categoria">The answered turn's category.</param>
    /// <param name="sqlEjecutado">
    /// The SQL that produced the answer, so it is never suggested back.
    /// </param>
    /// <param name="conDatosPersonales">
    /// Whether the check should run against the personal-data-capable
    /// read-only role, matching the connection the turn itself used.
    /// </param>
    Task<IReadOnlyList<string>> ObtenerAsync(
        Guid actor,
        string categoria,
        string? sqlEjecutado,
        bool conDatosPersonales,
        CancellationToken ct);
}
