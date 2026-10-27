using Microsoft.Extensions.Options;
using Modules.Asistente.Application;

namespace Modules.Asistente.Infrastructure;

/// <summary>Caché de idempotencia por actor y clave, con expiración corta.</summary>
internal sealed class IdempotenciaEnMemoria(
    IOptions<OpcionesAsistente> opciones, TimeProvider reloj) : IIdempotencia
{
    private readonly Dictionary<(Guid Actor, string Clave), Entrada> _entradas = [];
    private readonly Lock _candado = new();

    public ResultadoDelTurno? Recordar(Guid actor, string clave)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clave);

        lock (_candado)
        {
            Purgar();

            return _entradas.TryGetValue((actor, clave), out var entrada)
                ? entrada.Resultado
                : null;
        }
    }

    public void Guardar(Guid actor, string clave, ResultadoDelTurno resultado)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clave);
        ArgumentNullException.ThrowIfNull(resultado);

        lock (_candado)
        {
            Purgar();

            _entradas[(actor, clave)] = new Entrada(
                resultado,
                reloj.GetUtcNow() + TimeSpan.FromMinutes(opciones.Value.VigenciaDeIdempotenciaMinutos));
        }
    }

    private void Purgar()
    {
        var ahora = reloj.GetUtcNow();

        foreach (var par in _entradas.Where(e => e.Value.Vence <= ahora).ToList())
        {
            _entradas.Remove(par.Key);
        }
    }

    private readonly record struct Entrada(ResultadoDelTurno Resultado, DateTimeOffset Vence);
}
