namespace ArsDocendi.Storage.Infrastructure;

public interface IProveedorObjetos
{
    Task<string> CrearUrlSubidaAsync(string bucket, string clave, int expiracionSegundos, CancellationToken ct);
    Task<ObjetoRemoto?> ObtenerAsync(string bucket, string clave, CancellationToken ct);
    Task<IReadOnlyList<ObjetoListado>> ListarAsync(string bucket, string prefijo, CancellationToken ct);
    Task EliminarAsync(string bucket, string clave, CancellationToken ct);
}

public sealed record ObjetoRemoto(long TamanoBytes, string? Mime, Stream Contenido);
public sealed record ObjetoListado(string Clave, DateTimeOffset UltimaModificacion);
