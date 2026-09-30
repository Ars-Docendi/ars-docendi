using System.Data;
using System.Security.Cryptography;
using Npgsql;

namespace ArsDocendi.Shared.Auditing;

public sealed record EstadoVerificacionLotes(
    bool HashesLocalesValidos,
    bool DobleCustodiaConfirmada,
    bool HayEventosPendientesDeLote,
    long UltimaSecuenciaVerificada,
    long CursorActual,
    IReadOnlyList<string> Observaciones);

/// <summary>
/// Verifica hashes y continuidad locales. No consulta los testigos remotos ni
/// valida firmas sin una clave pública confiable; nunca informa doble custodia
/// como verificada sólo porque haya timestamps en PostgreSQL.
/// </summary>
public sealed class VerificadorLotesAuditoria(NpgsqlDataSource dataSource)
{
    public async Task<EstadoVerificacionLotes> VerificarAsync(string ambiente, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ambiente);
        await using var conexion = await dataSource.OpenConnectionAsync(ct);
        await using var transaccion = await conexion.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);

        var resultado = await VerificarSnapshotAsync(conexion, transaccion, ambiente, ct);
        await transaccion.CommitAsync(ct);
        return resultado;
    }

    public static async Task<EstadoVerificacionLotes> VerificarSnapshotAsync(
        NpgsqlConnection conexion, NpgsqlTransaction transaccion, string ambiente, CancellationToken ct)
    {
        var cursor = await LeerCursorAsync(conexion, transaccion, ct);
        var lotes = await LeerLotesAsync(conexion, transaccion, ambiente, ct);
        var observaciones = new List<string>();
        var siguienteEsperado = 1L;
        byte[]? hashPrevio = null;
        var ultimaSecuencia = 0L;

        foreach (var lote in lotes)
        {
            if (lote.VersionFormato != HasherLotesAuditoria.VersionFormato)
            {
                observaciones.Add($"Lote {lote.Id}: versión de formato no soportada.");
                break;
            }
            if (lote.PrimeraSecuencia != siguienteEsperado)
            {
                observaciones.Add($"Lote {lote.Id}: discontinuidad de cursor.");
                break;
            }
            if (!HashIguales(lote.HashLoteAnterior, hashPrevio))
            {
                observaciones.Add($"Lote {lote.Id}: referencia al lote previo inválida.");
                break;
            }

            try
            {
                var eventos = await PreparadorLotesAuditoria.LeerEventosAsync(
                    conexion, transaccion, lote.PrimeraSecuencia, lote.UltimaSecuencia, ct);
                var lotePreparado = new LoteAuditoriaPreparado(
                    lote.Id, ambiente, lote.PrimeraSecuencia, lote.UltimaSecuencia,
                    lote.CantidadEventos, lote.HashLote, lote.HashLoteAnterior,
                    lote.Nonce, lote.Manifiesto, lote.HashManifiesto,
                    EstadoSnapshot: lote.EstadoSnapshot, EstadoCursor: lote.EstadoCursor);
                PreparadorLotesAuditoria.VerificarLote(lotePreparado, eventos,
                    await PreparadorLotesAuditoria.LeerHashLegacyAsync(conexion, transaccion, ambiente, lote.Nonce, ct));
                await CheckpointsEstadoAuditoria.VerificarActualAsync(conexion, transaccion,
                    lote.EstadoSnapshot!, lote.EstadoCursor!.Value, cursor, ct);
            }
            catch (InvalidDataException)
            {
                observaciones.Add($"Lote {lote.Id}: eventos o manifiesto no coinciden con el digest.");
                break;
            }

            hashPrevio = lote.HashManifiesto;
            siguienteEsperado = lote.UltimaSecuencia + 1;
            ultimaSecuencia = lote.UltimaSecuencia;
        }

        if (cursor < ultimaSecuencia)
            observaciones.Add("El cursor retrocedió respecto del último lote local.");
        var hashLocalValido = observaciones.Count == 0;
        var pendientes = cursor >= siguienteEsperado;
        if (pendientes)
            observaciones.Add($"Hay eventos auditados pendientes de lote desde la secuencia {siguienteEsperado}.");
        return new(
            hashLocalValido,
            DobleCustodiaConfirmada: false,
            HayEventosPendientesDeLote: pendientes,
            ultimaSecuencia,
            cursor,
            observaciones);
    }

    private static async Task<long> LeerCursorAsync(
        NpgsqlConnection conexion, NpgsqlTransaction tx, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(
            "SELECT last_seq FROM audit.seal_cursor WHERE singleton_id = TRUE", conexion, tx);
        return (long)(await comando.ExecuteScalarAsync(ct)
            ?? throw new InvalidDataException("No existe el cursor único de sellado."));
    }

    private static async Task<List<LotePersistido>> LeerLotesAsync(
        NpgsqlConnection conexion,
        NpgsqlTransaction tx,
        string ambiente,
        CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand("""
            SELECT id, first_seq, last_seq, event_count, format_version, batch_nonce,
                   batch_hash, previous_batch_hash, manifest, manifest_hash,
                   signature, signing_key_id, primary_witnessed_at, secondary_witnessed_at, state_snapshot::text, state_cursor
              FROM audit.seal_batches
             WHERE environment = @ambiente
             ORDER BY id
            """, conexion, tx);
        comando.Parameters.AddWithValue("ambiente", ambiente);
        await using var lector = await comando.ExecuteReaderAsync(ct);
        var lotes = new List<LotePersistido>();
        while (await lector.ReadAsync(ct))
        {
            lotes.Add(new(
                lector.GetInt64(0), lector.GetInt64(1), lector.GetInt64(2),
                checked((int)lector.GetInt64(3)), lector.GetInt16(4), lector.GetGuid(5),
                lector.GetFieldValue<byte[]>(6), lector.IsDBNull(7) ? null : lector.GetFieldValue<byte[]>(7),
                lector.GetFieldValue<byte[]>(8), lector.GetFieldValue<byte[]>(9),
                lector.IsDBNull(10) ? null : lector.GetFieldValue<byte[]>(10),
                lector.IsDBNull(11) ? null : lector.GetString(11),
                lector.IsDBNull(12) ? null : lector.GetDateTime(12),
                lector.IsDBNull(13) ? null : lector.GetDateTime(13),
                lector.IsDBNull(14) ? null : lector.GetString(14), lector.IsDBNull(15) ? null : lector.GetInt64(15)));
        }
        return lotes;
    }

    private static bool HashIguales(byte[]? izquierda, byte[]? derecha) =>
        izquierda is null ? derecha is null
        : derecha is not null && CryptographicOperations.FixedTimeEquals(izquierda, derecha);

    private sealed record LotePersistido(
        long Id,
        long PrimeraSecuencia,
        long UltimaSecuencia,
        int CantidadEventos,
        short VersionFormato,
        Guid Nonce,
        byte[] HashLote,
        byte[]? HashLoteAnterior,
        byte[] Manifiesto,
        byte[] HashManifiesto,
        byte[]? Firma,
        string? IdClaveFirma,
        DateTime? PrimarioEn,
        DateTime? SecundarioEn, string? EstadoSnapshot, long? EstadoCursor);
}
