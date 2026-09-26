using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Modules.Asistente.Application;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// In-memory feedback token store, keyed by the token alone.
/// </summary>
/// <remarks>
/// A <see cref="ConcurrentDictionary{TKey, TValue}"/> and not the
/// lock-guarded <see cref="Dictionary{TKey, TValue}"/> pattern
/// <see cref="IdempotenciaEnMemoria"/> uses: that one is keyed by
/// <c>(actor, clave)</c> and already needs a lock to purge safely, while this
/// one is keyed by the token alone and the task list asks for exactly this
/// shape. Purging opportunistically on write, same idea as
/// <see cref="IdempotenciaEnMemoria"/>: at this scale a background sweep buys
/// nothing a bounded dictionary of short-lived entries doesn't already give
/// for free.
/// </remarks>
internal sealed class ValidezDeRetroalimentacionEnMemoria(IOptions<OpcionesAsistente> opciones)
    : IValidezDeRetroalimentacion
{
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _mintedAt = new();

    public void Registrar(Guid token, DateTimeOffset mintedAt)
    {
        _mintedAt[token] = mintedAt;
    }

    public bool EsVigente(Guid token, DateTimeOffset ahora)
    {
        if (!_mintedAt.TryGetValue(token, out var minted))
        {
            return false;
        }

        var vigencia = TimeSpan.FromMinutes(opciones.Value.VigenciaDeRetroalimentacionMinutos);

        if (ahora - minted > vigencia)
        {
            // Purge it: an expired token is never coming back to life, and there
            // is no reason to keep paying for the entry.
            _mintedAt.TryRemove(token, out _);
            return false;
        }

        return true;
    }
}
