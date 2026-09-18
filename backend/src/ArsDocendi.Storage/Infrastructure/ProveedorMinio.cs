using Minio;
using Minio.DataModel.Args;

namespace ArsDocendi.Storage.Infrastructure;

public sealed class ProveedorMinio(IMinioClient cliente) : IProveedorObjetos
{
    public Task<string> CrearUrlSubidaAsync(string bucket, string clave, int expiracionSegundos, CancellationToken ct) =>
        cliente.PresignedPutObjectAsync(new PresignedPutObjectArgs()
            .WithBucket(bucket)
            .WithObject(clave)
            .WithExpiry(expiracionSegundos));

    public async Task<ObjetoRemoto?> ObtenerAsync(string bucket, string clave, CancellationToken ct)
    {
        try
        {
            var memoria = new MemoryStream();
            var stat = await cliente.StatObjectAsync(new StatObjectArgs().WithBucket(bucket).WithObject(clave), ct);
            await cliente.GetObjectAsync(new GetObjectArgs()
                .WithBucket(bucket)
                .WithObject(clave)
                .WithCallbackStream(stream => stream.CopyTo(memoria)), ct);
            memoria.Position = 0;
            return new ObjetoRemoto(stat.Size, stat.ContentType, memoria);
        }
        catch (Minio.Exceptions.ObjectNotFoundException)
        {
            return null;
        }
        catch (Minio.Exceptions.MinioException ex) when (ex.Message.Contains("NoSuchKey", StringComparison.OrdinalIgnoreCase)
                                                        || ex.Message.Contains("not exist", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<ObjetoListado>> ListarAsync(string bucket, string prefijo, CancellationToken ct)
    {
        var resultado = new List<ObjetoListado>();
        var argumentos = new ListObjectsArgs()
            .WithBucket(bucket)
            .WithPrefix(prefijo)
            .WithRecursive(true);
        await foreach (var objeto in cliente.ListObjectsEnumAsync(argumentos, ct))
        {
            if (!objeto.IsDir)
            {
                var modificado = objeto.LastModifiedDateTime ?? DateTime.UtcNow;
                resultado.Add(new ObjetoListado(objeto.Key, new DateTimeOffset(modificado.ToUniversalTime())));
            }
        }
        return resultado;
    }

    public Task EliminarAsync(string bucket, string clave, CancellationToken ct) =>
        cliente.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(bucket).WithObject(clave), ct);
}
