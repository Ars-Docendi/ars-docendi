using Microsoft.EntityFrameworkCore;

namespace Modules.Designaciones.Infrastructure;

/// <summary>
/// Envuelve un bloque de trabajo en una transacción explícita.
/// <para>
/// Existe porque la materialización de una aprobación toca dos agregados —el pedido
/// y las designaciones vigentes— y la spec exige que un fallo parcial no deje estado
/// inconsistente: si la apertura de la designación nueva falla, el cierre de la
/// anterior tiene que revertirse.
/// </para>
/// <para>
/// Podría apoyarse en que un único <c>SaveChanges</c> ya es atómico, pero eso deja la
/// garantía implícita en el orden de las llamadas. Una transacción explícita la hace
/// legible y mantiene a EF fuera de la capa de servicios.
/// </para>
/// </summary>
internal interface IUnidadDeTrabajo
{
    Task EjecutarEnTransaccionAsync(Func<CancellationToken, Task> trabajo, CancellationToken ct);
}

/// <inheritdoc cref="IUnidadDeTrabajo" />
internal sealed class UnidadDeTrabajo(DesignacionesDbContext db) : IUnidadDeTrabajo
{
    public async Task EjecutarEnTransaccionAsync(
        Func<CancellationToken, Task> trabajo, CancellationToken ct)
    {
        // La estrategia de ejecución de Npgsql puede reintentar; usar su envoltorio
        // en vez de BeginTransaction directo es lo que hace el reintento seguro.
        var estrategia = db.Database.CreateExecutionStrategy();

        await estrategia.ExecuteAsync(async token =>
        {
            await using var transaccion = await db.Database.BeginTransactionAsync(token);
            await trabajo(token);
            await transaccion.CommitAsync(token);
        }, ct);
    }
}
