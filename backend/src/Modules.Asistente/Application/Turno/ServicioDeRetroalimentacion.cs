using Microsoft.Extensions.Logging;

namespace Modules.Asistente.Application;

/// <summary>How a feedback submission was resolved.</summary>
public enum ResultadoDeRetroalimentacion
{
    /// <summary>The vote was recorded.</summary>
    Aceptada,

    /// <summary>
    /// The token is unknown or expired. Deliberately a single outcome for both
    /// cases: see asistente-retroalimentacion's spec — a caller must not be able
    /// to tell "expired" apart from "never existed".
    /// </summary>
    TokenInvalido,
}

/// <summary>
/// Orchestrates one feedback submission: token validity, then the
/// thumbs-up-ignores-any-reason rule, then persistence.
/// </summary>
/// <remarks>
/// Takes NO actor parameter, anywhere on this type — not because it was left
/// out, but because there is nothing here that needs one. That absence is
/// what makes it structurally impossible for this class's own logging to ever
/// pair an actor id with a feedback token in the same log event (design.md D3).
/// </remarks>
public sealed class ServicioDeRetroalimentacion(
    IValidezDeRetroalimentacion validez,
    IRegistroDeRetroalimentacion registro,
    TimeProvider reloj,
    ILogger<ServicioDeRetroalimentacion> log)
{
    public async Task<ResultadoDeRetroalimentacion> RegistrarAsync(
        Guid token, bool voto, IReadOnlyList<string>? razones, string? comentario, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();

        if (!validez.EsVigente(token, ahora))
        {
            log.LogInformation(
                "Se envió retroalimentación con un token desconocido o vencido {Token}.", token);
            return ResultadoDeRetroalimentacion.TokenInvalido;
        }

        // Server-side, never trusting the client to have omitted them: a thumbs-up
        // carries no reason and no comment (asistente-retroalimentacion's spec).
        var razonesFinal = voto ? null : (razones is { Count: > 0 } ? razones : null);
        var comentarioFinal = voto ? null : comentario;

        await registro.GuardarAsync(token, voto, razonesFinal, comentarioFinal, ahora, ct);

        log.LogInformation("Retroalimentación registrada para el token {Token}.", token);

        return ResultadoDeRetroalimentacion.Aceptada;
    }
}
