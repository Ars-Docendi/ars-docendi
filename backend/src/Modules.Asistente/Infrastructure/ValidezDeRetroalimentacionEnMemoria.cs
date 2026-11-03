using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Modules.Asistente.Application;

namespace Modules.Asistente.Infrastructure;

/// <summary>In-memory feedback token store, keyed by the token alone.</summary>
internal sealed class ValidezDeRetroalimentacionEnMemoria(IOptions<OpcionesAsistente> opciones)
    : IValidezDeRetroalimentacion
{
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _mintedAt = new();

    public void Registrar(Guid token, DateTimeOffset mintedAt)
    {
        // La purga al escribir: un token que nadie vuelve a consultar nunca pasa
        // por EsVigente, y sin esto quedaría para siempre.
        var vigencia = TimeSpan.FromMinutes(opciones.Value.VigenciaDeRetroalimentacionMinutos);
        foreach (var (vencido, minted) in _mintedAt)
        {
            if (mintedAt - minted > vigencia)
            {
                _mintedAt.TryRemove(vencido, out _);
            }
        }

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

    public void Revocar(Guid token)
    {
        _mintedAt.TryRemove(token, out _);
    }
}
