namespace Modules.Asistente.Application;

/// <summary>
/// Tracks how long a feedback token stays live, keyed by the token itself.
/// </summary>
/// <remarks>
/// Same in-memory, no-persistence, short-TTL shape as <see cref="IIdempotencia"/>
/// — the module's existing precedent for exactly this kind of state — but keyed
/// by the token alone and NOT by actor. That is load-bearing and not an
/// oversight: keying by actor would make this a second place that maps a
/// feedback token back to who asked, which is precisely the channel TD-012
/// closes by never giving the analytic row an actor column in the first place.
///
/// A stale or never-issued token has to be rejected the same way (see
/// asistente-retroalimentacion's spec), so this port answers one question —
/// "is this token still live right now" — rather than separately exposing
/// "does it exist" and "has it expired".
/// </remarks>
public interface IValidezDeRetroalimentacion
{
    /// <summary>Records that this token was minted, right now.</summary>
    void Registrar(Guid token, DateTimeOffset mintedAt);

    /// <summary>Whether the token is still within its validity window.</summary>
    bool EsVigente(Guid token, DateTimeOffset ahora);
}
