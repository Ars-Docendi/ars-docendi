using System.Data.Common;

namespace ArsDocendi.Storage.Contracts;

/// <summary>Los schemas consumidores son la autoridad; un fallo nunca significa cero referencias.</summary>
public interface IConsultasReferenciasArchivos
{
    string Consumidor { get; }
    Task<bool> TieneReferenciasAsync(Guid archivoId, CancellationToken ct);
}

/// <summary>Exclusión transaccional en orden de IDs, sin stream ni I/O remoto en el callback.</summary>
public interface IProteccionArchivos
{
    Task<T> EjecutarAsync<T>(IReadOnlyCollection<Guid> archivoIds, Func<CancellationToken, Task<T>> operacion, CancellationToken ct);
}

/// <summary>Adaptador local: enlista su contexto sobre la conexión/transacción de la operación.</summary>
public interface IParticipanteTransaccionArchivos
{
    Task EnlistarAsync(DbConnection conexion, DbTransaction transaccion, CancellationToken ct);
    Task DesenlistarAsync(CancellationToken ct);
}

public interface IRetencionArchivos
{
    /// <summary>Tras confirmar la desvinculación, conserva si otro consumidor lo referencia.</summary>
    Task<bool> EliminarSiSinReferenciasAsync(Guid archivoId, Guid propietarioId, CancellationToken ct);
}

public interface IConsultasArchivos
{
    Task<IReadOnlyDictionary<Guid, ArchivoDto>> ObtenerPorIdsAsync(IReadOnlyCollection<Guid> archivoIds, CancellationToken ct);
}
