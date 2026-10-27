namespace Modules.Asistente.Application;

/// <summary>Tracks how long a feedback token stays live, keyed by the token itself.</summary>
public interface IValidezDeRetroalimentacion
{
    /// <summary>Records that this token was minted, right now.</summary>
    void Registrar(Guid token, DateTimeOffset mintedAt);

    /// <summary>Whether the token is still within its validity window.</summary>
    bool EsVigente(Guid token, DateTimeOffset ahora);

    /// <summary>
    /// Stops accepting this token, right now — same outcome as an unknown or expired one
    /// (asistente-edicion-de-la-ultima-pregunta: replacing a turn revokes its feedback token).
    /// </summary>
    void Revocar(Guid token);
}
