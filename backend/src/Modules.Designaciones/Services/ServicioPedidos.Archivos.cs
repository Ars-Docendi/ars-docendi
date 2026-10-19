using ArsDocendi.Storage.Contracts;
using Modules.Designaciones.Domain;

namespace Modules.Designaciones.Services;

internal sealed partial class ServicioPedidos
{
    public Task<Pedido> CrearAsync(DatosPedido datos, CancellationToken ct) =>
        ProtegerArchivosAsync(datos.Adjuntos.Select(a => a.ArchivoId),
            token => CrearProtegidoAsync(datos, token), ct);

    public async Task<Pedido> EditarAsync(Guid pedidoId, DatosPedido datos, CancellationToken ct)
    {
        var anterior = await pedidos.ObtenerPorIdAsync(pedidoId, ct);
        var ids = (anterior?.Adjuntos.Select(a => a.ArchivoId) ?? [])
            .Concat(datos.Adjuntos.Select(a => a.ArchivoId));
        return await ProtegerArchivosAsync(ids, token => EditarProtegidoAsync(pedidoId, datos, token), ct);
    }

    public async Task EliminarBorradorAsync(Guid pedidoId, CancellationToken ct)
    {
        var anterior = await pedidos.ObtenerPorIdAsync(pedidoId, ct);
        await ProtegerArchivosAsync(anterior?.Adjuntos.Select(a => a.ArchivoId) ?? [], async token =>
        {
            await EliminarBorradorProtegidoAsync(pedidoId, token);
            return true;
        }, ct);
    }

    private Task<T> ProtegerArchivosAsync<T>(IEnumerable<Guid?> archivoIds,
        Func<CancellationToken, Task<T>> operacion, CancellationToken ct)
    {
        var ids = archivoIds.OfType<Guid>().Distinct().Order().ToArray();
        if (ids.Length == 0) return operacion(ct);
        return (proteccionArchivos ?? almacenamiento as IProteccionArchivos
            ?? throw new InvalidOperationException("Falta la protección transaccional de archivos de Designaciones."))
            .EjecutarAsync(ids, operacion, ct);
    }
}
