using ArsDocendi.Shared.Persistencia;
using Modules.Asistente.Application;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// <see cref="ICandadoDelTurno"/> sobre el advisory lock de Postgres
/// (<see cref="CandadoDelTurno"/>).
/// </summary>
internal sealed class CandadoDelTurnoReal(CadenaDuena cadena) : ICandadoDelTurno
{
    public async Task<IAsyncDisposable?> IntentarAsync(Guid actor, CancellationToken ct) =>
        await CandadoDelTurno.IntentarAsync(cadena, actor, ct);
}
