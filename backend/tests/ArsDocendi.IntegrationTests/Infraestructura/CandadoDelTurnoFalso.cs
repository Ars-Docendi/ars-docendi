using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Infraestructura;

/// <summary>
/// Fake en memoria de <see cref="ICandadoDelTurno"/>, para
/// <see cref="BancoDelAsistente"/>: un <see cref="HashSet{T}"/> de actores con
/// turno en curso en vez del advisory lock de Postgres.
/// </summary>
internal sealed class CandadoDelTurnoFalso : ICandadoDelTurno
{
    private readonly HashSet<Guid> _tomados = [];

    public Task<IAsyncDisposable?> IntentarAsync(Guid actor, CancellationToken ct)
    {
        lock (_tomados)
        {
            if (!_tomados.Add(actor))
            {
                return Task.FromResult<IAsyncDisposable?>(null);
            }
        }

        return Task.FromResult<IAsyncDisposable?>(new Liberador(this, actor));
    }

    private void Liberar(Guid actor)
    {
        lock (_tomados)
        {
            _tomados.Remove(actor);
        }
    }

    private sealed class Liberador(CandadoDelTurnoFalso dueno, Guid actor) : IAsyncDisposable
    {
        private bool _liberado;

        public ValueTask DisposeAsync()
        {
            if (!_liberado)
            {
                _liberado = true;
                dueno.Liberar(actor);
            }

            return ValueTask.CompletedTask;
        }
    }
}
