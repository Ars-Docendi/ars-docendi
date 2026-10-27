namespace Modules.Asistente.Application;

/// <summary>Persists (or updates) the feedback for one already-answered turn.</summary>
public interface IRegistroDeRetroalimentacion
{
    /// <summary>
    /// Upserts the vote for <paramref name="analiticoId"/>: one row per turn, last vote wins, no
    /// history of prior votes kept.
    /// </summary>
    Task GuardarAsync(
        Guid analiticoId,
        bool voto,
        IReadOnlyList<string>? razones,
        string? comentario,
        DateTimeOffset ahora,
        CancellationToken ct);
}
