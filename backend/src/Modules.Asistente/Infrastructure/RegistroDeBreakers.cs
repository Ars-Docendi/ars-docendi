using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Asistente.Application;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// One <see cref="BreakerDelProveedor"/> per adapter <c>Nombre</c>
/// (<c>"{empresa}/{modelo}"</c>) (asistente-modelo-en-caliente, D8).
/// </summary>
/// <remarks>
/// Singleton, like the single breaker it replaces. Keying by name means switching
/// away from a failing model does not inherit its open breaker, and switching back
/// does not reset one that is genuinely still open. Entries are never removed: the
/// set of names is bounded by the model catalog plus the environment names.
/// </remarks>
internal sealed class RegistroDeBreakers(
    IOptions<OpcionesAsistente> opciones,
    TimeProvider reloj,
    ILoggerFactory fabricaDeLogs)
{
    private readonly ConcurrentDictionary<string, BreakerDelProveedor> _breakers =
        new(StringComparer.Ordinal);

    /// <summary>The breaker of <paramref name="nombre"/>, created closed on first use.</summary>
    public BreakerDelProveedor Para(string nombre)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        return _breakers.GetOrAdd(
            nombre,
            _ => new BreakerDelProveedor(
                opciones, reloj, fabricaDeLogs.CreateLogger<BreakerDelProveedor>()));
    }
}
