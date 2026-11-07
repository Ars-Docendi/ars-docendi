namespace Modules.Aulas.Contracts.Queries;

public interface IAulasQueries
{
    /// <summary>
    /// De las solicitudes de reserva dadas, las que el actor actual puede abrir en su detalle:
    /// todas las existentes con <c>aulas.aprobar</c>, o las propias con <c>aulas.solicitar</c>.
    /// Es el mismo criterio que el endpoint de detalle; el resto no se distingue de una inexistente.
    /// </summary>
    Task<IReadOnlySet<Guid>> SolicitudesAbriblesAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct);
}
