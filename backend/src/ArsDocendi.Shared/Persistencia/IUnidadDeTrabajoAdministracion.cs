using System.Transactions;

namespace ArsDocendi.Shared.Persistencia;

/// <summary>
/// Coordina casos administrativos que escriben más de un DbContext sobre la
/// misma base PostgreSQL. Los contextos se enlistan en la transacción ambiente.
/// </summary>
public interface IUnidadDeTrabajoAdministracion
{
    Task<T> EjecutarAsync<T>(Func<CancellationToken, Task<T>> trabajo, CancellationToken ct);
}

internal sealed class UnidadDeTrabajoAdministracion : IUnidadDeTrabajoAdministracion
{
    public async Task<T> EjecutarAsync<T>(
        Func<CancellationToken, Task<T>> trabajo,
        CancellationToken ct)
    {
        using var alcance = new TransactionScope(
            TransactionScopeOption.Required,
            new TransactionOptions
            {
                IsolationLevel = IsolationLevel.ReadCommitted,
                Timeout = TimeSpan.FromSeconds(30),
            },
            TransactionScopeAsyncFlowOption.Enabled);
        var resultado = await trabajo(ct);
        alcance.Complete();
        return resultado;
    }
}
