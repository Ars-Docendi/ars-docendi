using ArsDocendi.Shared.Auditing;
using Npgsql;

namespace ArsDocendi.Host.Auditoria;

public sealed record ResultadoVerificacionAnclada(bool Verificado, bool RequiereReconciliacion,
    long CursorLocal, long CursorRemoto, bool EventosPendientes);

/// <summary>Contrasta hashes locales con la evidencia remota, nunca escribe ni repara.</summary>
public sealed class VerificadorAuditoriaAnclada(NpgsqlDataSource dataSource, VerificadorTestigosRemotos remoto)
{
    public async Task<ResultadoVerificacionAnclada> VerificarAsync(string ambiente, CancellationToken ct)
    {
        await using var conexion = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conexion.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        await using (var lectura = new NpgsqlCommand("SET TRANSACTION READ ONLY", conexion, tx))
            await lectura.ExecuteNonQueryAsync(ct);
        var local = await VerificadorLotesAuditoria.VerificarSnapshotAsync(conexion, tx, ambiente, ct);
        var testigos = await remoto.VerificarAsync(ambiente, local.CursorActual, ct);
        // Verificar toda la cadena histórica, no sólo la firma del último manifiesto remoto.
        await using (var firmas = new NpgsqlCommand("""
            SELECT manifest_hash, signature, signing_key_id
            FROM audit.seal_batches WHERE environment = @ambiente
            ORDER BY id
            """, conexion, tx))
        {
            firmas.Parameters.AddWithValue("ambiente", ambiente);
            await using var lector = await firmas.ExecuteReaderAsync(ct);
            string? claveAnterior = null;
            while (await lector.ReadAsync(ct))
            {
                if (lector.IsDBNull(1) || lector.IsDBNull(2))
                    throw new InvalidDataException("Lote local sin firma histórica verificable.");
                var clave = lector.GetString(2);
                if (claveAnterior is not null && claveAnterior != clave)
                    remoto.VerificarTransicion(ambiente, claveAnterior, clave);
                remoto.VerificarFirma(lector.GetFieldValue<byte[]>(0), lector.GetFieldValue<byte[]>(1), clave);
                claveAnterior = clave;
            }
        }
        await using var comando = new NpgsqlCommand("""
            SELECT manifest_hash FROM audit.seal_batches
            WHERE environment = @ambiente AND last_seq = @cursor
            ORDER BY id DESC LIMIT 1
            """, conexion, tx);
        comando.Parameters.AddWithValue("ambiente", ambiente);
        comando.Parameters.AddWithValue("cursor", testigos.Cursor);
        var hash = await comando.ExecuteScalarAsync(ct) as byte[];
        var coincide = hash is not null && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            hash, Convert.FromHexString(testigos.HashManifiesto));
        var reconciliar = testigos.RequiereReconciliacion || !coincide || !local.HashesLocalesValidos;
        return new(!reconciliar && !local.HayEventosPendientesDeLote
            && local.UltimaSecuenciaVerificada == testigos.Cursor, reconciliar,
            local.CursorActual, testigos.Cursor, local.HayEventosPendientesDeLote);
    }
}
