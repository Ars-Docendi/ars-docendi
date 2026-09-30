using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace ArsDocendi.Shared.Auditing;

public sealed record ResultadoEstadoAuditoria(
    bool CoincideConAuditoria, bool CoberturaIntegralBase, string Modo,
    long Cursor, string Digest, Guid Nonce, IReadOnlyList<string> TablasCubiertas,
    long FilasSinEvidencia, long Divergencias);

/// <summary>
/// Inventario v1 deliberadamente parcial: identity.roles, todas sus columnas.
/// Snapshot consistente, sólo lectura. El digest es estado observado, no ancla externa.
/// La concordancia con auditoría local no prueba autenticidad de esa auditoría.
/// </summary>
public sealed class VerificadorEstadoAuditoria(NpgsqlDataSource dataSource)
{
    public async Task<ResultadoEstadoAuditoria> VerificarAsync(
        string ambiente, Guid nonce, long? despuesDe, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ambiente);
        if (nonce == Guid.Empty) throw new ArgumentException("Se requiere nonce.", nameof(nonce));
        if (despuesDe < 0) throw new ArgumentOutOfRangeException(nameof(despuesDe));
        await using var conexion = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conexion.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await using (var soloLectura = new NpgsqlCommand("SET TRANSACTION READ ONLY", conexion, tx))
            await soloLectura.ExecuteNonQueryAsync(ct);
        await using var cursorCmd = new NpgsqlCommand("SELECT last_seq FROM audit.seal_cursor WHERE singleton_id", conexion, tx);
        var cursor = (long)(await cursorCmd.ExecuteScalarAsync(ct))!;
        if (despuesDe > cursor) throw new InvalidDataException("Cursor de estado retrocedido; reconciliar.");
        // row_pk sólo vive dentro de PostgreSQL. No se devuelve ni publica por fila.
        await using var comando = new NpgsqlCommand("""
            WITH ultimo AS (
                SELECT DISTINCT ON (row_pk) row_pk, action, new_row, seal_seq
                FROM audit.change_log WHERE schema_name = 'identity' AND table_name = 'roles'
                ORDER BY row_pk, seal_seq DESC NULLS LAST, id DESC
            ), actual AS (
                SELECT id::text AS clave, to_jsonb(r) AS fila FROM identity.roles r
            )
            SELECT a.fila::text,
                   u.row_pk IS NULL AS sin_evidencia,
                   CASE WHEN u.row_pk IS NULL THEN TRUE
                        WHEN u.action = 'DELETE' THEN a.clave IS NOT NULL
                        ELSE a.clave IS NULL OR a.fila IS DISTINCT FROM u.new_row END AS divergencia
            FROM actual a FULL JOIN ultimo u ON a.clave = u.row_pk
            WHERE @desde::bigint IS NULL OR u.seal_seq > @desde
            ORDER BY coalesce(a.clave, u.row_pk) COLLATE "C"
            """, conexion, tx);
        comando.Parameters.AddWithValue("desde", NpgsqlTypes.NpgsqlDbType.Bigint, (object?)despuesDe ?? DBNull.Value);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        // JSONB textual de PostgreSQL 18, versionado explícitamente. No publicar hashes por fila.
        Anexar(hash, JsonSerializer.Serialize(new { dominio = "ARS-STATE-PG18-v1", ambiente, nonce,
            tabla = "identity.roles", columnas = "*", desde = despuesDe, cursor }));
        long sinEvidencia = 0, divergencias = 0;
        await using (var lector = await comando.ExecuteReaderAsync(ct))
        {
            while (await lector.ReadAsync(ct))
            {
                if (!lector.IsDBNull(0)) Anexar(hash, lector.GetString(0));
                if (lector.GetBoolean(1)) sinEvidencia++;
                if (lector.GetBoolean(2)) divergencias++;
            }
        }
        await tx.CommitAsync(ct);
        return new(divergencias == 0 && sinEvidencia == 0, false,
            despuesDe is null ? "completo-inventario" : "incremental", cursor,
            Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(), nonce,
            ["identity.roles"], sinEvidencia, divergencias);
    }

    private static void Anexar(IncrementalHash hash, string valor)
    {
        var bytes = Encoding.UTF8.GetBytes(valor);
        Span<byte> longitud = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(longitud, bytes.Length);
        hash.AppendData(longitud);
        hash.AppendData(bytes);
    }
}
