namespace Modules.Asistente.Application;

/// <summary>
/// Persists (or updates) the feedback for one already-answered turn.
/// </summary>
/// <remarks>
/// Deliberately takes no actor parameter — there is none to take. The row this
/// writes is keyed only by <paramref name="analiticoId"/>: see
/// <c>asistente.retroalimentacion_turno</c> in
/// <c>003_asistente_retroalimentacion.sql</c> and TD-012.
/// </remarks>
public interface IRegistroDeRetroalimentacion
{
    /// <summary>
    /// Upserts the vote for <paramref name="analiticoId"/>: one row per turn,
    /// last vote wins, no history of prior votes kept.
    /// </summary>
    /// <param name="razon">
    /// One of <see cref="RazonesDeRetroalimentacion.Todas"/>, or null. Callers
    /// must already have applied the "thumbs-up carries no reason" rule before
    /// calling this — this method persists exactly what it is given.
    /// </param>
    Task GuardarAsync(
        Guid analiticoId, bool voto, string? razon, DateTimeOffset ahora, CancellationToken ct);
}
