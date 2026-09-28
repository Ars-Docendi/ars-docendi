using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace ArsDocendi.Storage.Infrastructure;

public sealed class ProveedorSeaweedFs(IAmazonS3 cliente) : IProveedorObjetos
{
    public async Task SubirAsync(
        string bucket,
        string clave,
        Stream contenido,
        string mime,
        long tamanoBytes,
        CancellationToken ct)
    {
        await cliente.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = clave,
            InputStream = contenido,
            ContentType = mime,
            AutoCloseStream = false,
            AutoResetStreamPosition = false,
            Headers = { ContentLength = tamanoBytes },
        }, ct);
    }

    public async Task<ObjetoRemoto?> ObtenerAsync(string bucket, string clave, CancellationToken ct)
    {
        try
        {
            using var respuesta = await cliente.GetObjectAsync(new GetObjectRequest
            {
                BucketName = bucket,
                Key = clave,
            }, ct);
            var memoria = new MemoryStream();
            await respuesta.ResponseStream.CopyToAsync(memoria, ct);
            memoria.Position = 0;
            return new ObjetoRemoto(respuesta.ContentLength, respuesta.Headers.ContentType, memoria);
        }
        catch (AmazonS3Exception ex) when (EsObjetoInexistente(ex))
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<ObjetoListado>> ListarAsync(string bucket, string prefijo, CancellationToken ct)
    {
        var resultado = new List<ObjetoListado>();
        string? token = null;
        do
        {
            var respuesta = await cliente.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = bucket,
                Prefix = prefijo,
                ContinuationToken = token,
            }, ct);
            foreach (var objeto in respuesta.S3Objects)
            {
                var modificado = objeto.LastModified ?? DateTime.UtcNow;
                resultado.Add(new ObjetoListado(objeto.Key, new DateTimeOffset(modificado.ToUniversalTime())));
            }
            token = respuesta.IsTruncated == true ? respuesta.NextContinuationToken : null;
        } while (token is not null);

        return resultado;
    }

    public async Task EliminarAsync(string bucket, string clave, CancellationToken ct)
    {
        try
        {
            await cliente.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = bucket,
                Key = clave,
            }, ct);
        }
        catch (AmazonS3Exception ex) when (EsObjetoInexistente(ex))
        {
            // La eliminación del estado remoto es idempotente para el contrato local.
        }
    }

    private static bool EsObjetoInexistente(AmazonS3Exception ex) =>
        ex.StatusCode == HttpStatusCode.NotFound
        || string.Equals(ex.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase)
        || string.Equals(ex.ErrorCode, "NoSuchBucket", StringComparison.OrdinalIgnoreCase)
        || string.Equals(ex.ErrorCode, "NotFound", StringComparison.OrdinalIgnoreCase);
}
