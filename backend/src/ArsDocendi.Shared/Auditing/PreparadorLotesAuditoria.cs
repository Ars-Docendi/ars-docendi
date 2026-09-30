using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;

namespace ArsDocendi.Shared.Auditing;

public sealed record LoteAuditoriaPreparado(
    long Id,
    string Ambiente,
    long PrimeraSecuencia,
    long UltimaSecuencia,
    int CantidadEventos,
    byte[] HashLote,
    byte[]? HashLoteAnterior,
    Guid Nonce,
    byte[] Manifiesto,
    byte[] HashManifiesto,
    byte[]? Firma = null,
    string? IdClaveFirma = null,
    DateTimeOffset? TestigoPrimarioEn = null,
    DateTimeOffset? TestigoSecundarioEn = null,
    string? EstadoSnapshot = null, long? EstadoCursor = null);

public sealed record ResultadoPreparacionLote(
    LoteAuditoriaPreparado? Lote,
    bool OcupadoPorOtroProceso,
    bool SinNuevosEventos);

/// <summary>Prepara lotes locales, persistentes e idempotentes; no publica ni firma evidencia.</summary>
public sealed class PreparadorLotesAuditoria(NpgsqlDataSource dataSource)
{
    private const int MaximoEventosPorLote = 100_000;
    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public async Task<ResultadoPreparacionLote> PrepararSiguienteAsync(
        string ambiente,
        int maximoEventos,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ambiente);
        if (maximoEventos is < 1 or > MaximoEventosPorLote)
            throw new ArgumentOutOfRangeException(nameof(maximoEventos));

        await using var conexion = await dataSource.OpenConnectionAsync(ct);
        await using var transaccion = await conexion.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);

        await using (var lockComando = new NpgsqlCommand("""
            SELECT pg_try_advisory_xact_lock(
                hashtextextended('arsdocendi.audit-seal:' || @ambiente, 0))
            """, conexion, transaccion))
        {
            lockComando.Parameters.AddWithValue("ambiente", ambiente);
            if ((bool)(await lockComando.ExecuteScalarAsync(ct))! == false)
            {
                await transaccion.RollbackAsync(ct);
                return new(null, OcupadoPorOtroProceso: true, SinNuevosEventos: false);
            }
        }

        // El chequeo y el cierre comparten snapshot: no continuar una cadena local dañada.
        var verificacion = await VerificadorLotesAuditoria.VerificarSnapshotAsync(
            conexion, transaccion, ambiente, ct);
        if (!verificacion.HashesLocalesValidos)
            throw new InvalidDataException("La cadena local requiere reconciliación antes de sellar.");

        var pendiente = await LeerPendienteAsync(conexion, transaccion, ambiente, ct);
        if (pendiente is not null)
        {
            var eventosExistentes = await LeerEventosAsync(
                conexion, transaccion, pendiente.PrimeraSecuencia, pendiente.UltimaSecuencia, ct);
            VerificarLote(pendiente, eventosExistentes, await LeerHashLegacyAsync(conexion, transaccion, ambiente, pendiente.Nonce, ct));
            await transaccion.CommitAsync(ct);
            return new(pendiente, OcupadoPorOtroProceso: false, SinNuevosEventos: false);
        }

        var cursor = await LeerCursorAsync(conexion, transaccion, ct);
        var ultimoCompleto = await LeerUltimoCompletoAsync(conexion, transaccion, ambiente, ct);
        var primera = (ultimoCompleto?.UltimaSecuencia ?? 0) + 1;

        var ultima = Math.Min(cursor, primera + Math.Min(maximoEventos - 1L, cursor - primera));
        var eventos = await LeerEventosAsync(conexion, transaccion, primera, ultima, ct);
        var nonce = Guid.NewGuid();
        var hashPrevio = ultimoCompleto?.HashLote;
        var hash = HasherLotesAuditoria.CalcularHash(
            ambiente, primera, ultima, nonce, eventos, hashPrevio);
        var estado = await CheckpointsEstadoAuditoria.CapturarAsync(conexion, transaccion, ct);
        var manifiesto = CrearManifiesto(
            ambiente, primera, ultima, eventos.Count, nonce, hashPrevio, hash,
            await LeerHashLegacyAsync(conexion, transaccion, ambiente, nonce, ct),
            CheckpointsEstadoAuditoria.Hash(ambiente, nonce, estado), cursor);
        var hashManifiesto = SHA256.HashData(manifiesto);

        var id = await InsertarPendienteAsync(
            conexion, transaccion, ambiente, primera, ultima, eventos.Count,
            nonce, hash, hashPrevio, manifiesto, hashManifiesto, estado, cursor, ct);
        await transaccion.CommitAsync(ct);

        return new(
            new(id, ambiente, primera, ultima, eventos.Count, hash, hashPrevio, nonce, manifiesto, hashManifiesto,
                EstadoSnapshot: estado, EstadoCursor: cursor),
            OcupadoPorOtroProceso: false,
            SinNuevosEventos: false);
    }

    public async Task RegistrarFirmaAsync(
        long loteId,
        string idClave,
        byte[] firma,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idClave);
        ArgumentNullException.ThrowIfNull(firma);
        if (firma.Length == 0) throw new ArgumentException("La firma no puede estar vacía.", nameof(firma));
        await using var conexion = await dataSource.OpenConnectionAsync(ct);
        await using var comando = new NpgsqlCommand("""
            UPDATE audit.seal_batches
               SET signature = COALESCE(signature, @firma),
                   signing_key_id = COALESCE(signing_key_id, @idClave)
             WHERE id = @id
               AND ((signature IS NULL AND signing_key_id IS NULL)
                    OR (signature = @firma AND signing_key_id = @idClave))
            """, conexion);
        comando.Parameters.AddWithValue("id", loteId);
        comando.Parameters.AddWithValue("firma", firma);
        comando.Parameters.AddWithValue("idClave", idClave);
        if (await comando.ExecuteNonQueryAsync(ct) != 1)
            throw new InvalidDataException("No se pudo persistir la firma del lote de forma idempotente.");
    }

    public async Task RegistrarAcuseTestigoAsync(
        long loteId,
        bool esPrimario,
        byte[] hashAceptado,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(hashAceptado);
        if (hashAceptado.Length != 32)
            throw new ArgumentException("El acuse debe identificar un hash SHA-256 de 32 bytes.", nameof(hashAceptado));
        var columna = esPrimario ? "primary_witnessed_at" : "secondary_witnessed_at";
        await using var conexion = await dataSource.OpenConnectionAsync(ct);
        await using var comando = new NpgsqlCommand($"""
            WITH acuse AS (
                UPDATE audit.seal_batches
                   SET {columna} = COALESCE({columna}, clock_timestamp())
                 WHERE id = @id AND manifest_hash = @hash
                   AND signature IS NOT NULL AND signing_key_id IS NOT NULL
                 RETURNING primary_witnessed_at, secondary_witnessed_at
            ), baseline AS (
                UPDATE audit.seal_baseline SET status = 'anchored'
                 WHERE id = 1 AND status = 'observed'
                   AND EXISTS (SELECT 1 FROM acuse
                     WHERE primary_witnessed_at IS NOT NULL AND secondary_witnessed_at IS NOT NULL)
                 RETURNING id
            )
            SELECT count(*) FROM acuse
            """, conexion);
        comando.Parameters.AddWithValue("id", loteId);
        comando.Parameters.AddWithValue("hash", hashAceptado);
        if ((long)(await comando.ExecuteScalarAsync(ct))! != 1)
            throw new InvalidDataException("El testigo no confirmó el digest firmado del lote.");
    }

    private static async Task<long> LeerCursorAsync(
        NpgsqlConnection conexion, NpgsqlTransaction tx, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(
            "SELECT last_seq FROM audit.seal_cursor WHERE singleton_id = TRUE", conexion, tx);
        return (long)(await comando.ExecuteScalarAsync(ct)
            ?? throw new InvalidDataException("No existe el cursor único de sellado."));
    }

    private static async Task<LoteAuditoriaPreparado?> LeerPendienteAsync(
        NpgsqlConnection conexion,
        NpgsqlTransaction tx,
        string ambiente,
        CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand("""
            SELECT id, first_seq, last_seq, event_count, batch_hash,
                   previous_batch_hash, batch_nonce, manifest, manifest_hash,
                   signature, signing_key_id, primary_witnessed_at, secondary_witnessed_at, state_snapshot::text, state_cursor
              FROM audit.seal_batches
             WHERE environment = @ambiente
               AND (primary_witnessed_at IS NULL OR secondary_witnessed_at IS NULL)
             ORDER BY id
             LIMIT 1
            """, conexion, tx);
        comando.Parameters.AddWithValue("ambiente", ambiente);
        await using var lector = await comando.ExecuteReaderAsync(ct);
        if (!await lector.ReadAsync(ct)) return null;
        return new(
            lector.GetInt64(0), ambiente, lector.GetInt64(1), lector.GetInt64(2),
            checked((int)lector.GetInt64(3)), lector.GetFieldValue<byte[]>(4),
            lector.IsDBNull(5) ? null : lector.GetFieldValue<byte[]>(5),
            lector.GetGuid(6), lector.GetFieldValue<byte[]>(7), lector.GetFieldValue<byte[]>(8),
            lector.IsDBNull(9) ? null : lector.GetFieldValue<byte[]>(9),
            lector.IsDBNull(10) ? null : lector.GetString(10),
            lector.IsDBNull(11) ? null : new DateTimeOffset(DateTime.SpecifyKind(lector.GetDateTime(11), DateTimeKind.Utc)),
            lector.IsDBNull(12) ? null : new DateTimeOffset(DateTime.SpecifyKind(lector.GetDateTime(12), DateTimeKind.Utc)),
            lector.IsDBNull(13) ? null : lector.GetString(13), lector.IsDBNull(14) ? null : lector.GetInt64(14));
    }

    private static async Task<(long UltimaSecuencia, byte[] HashLote)?> LeerUltimoCompletoAsync(
        NpgsqlConnection conexion,
        NpgsqlTransaction tx,
        string ambiente,
        CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand("""
            SELECT last_seq, manifest_hash
              FROM audit.seal_batches
             WHERE environment = @ambiente
               AND primary_witnessed_at IS NOT NULL
               AND secondary_witnessed_at IS NOT NULL
             ORDER BY id DESC
             LIMIT 1
            """, conexion, tx);
        comando.Parameters.AddWithValue("ambiente", ambiente);
        await using var lector = await comando.ExecuteReaderAsync(ct);
        return await lector.ReadAsync(ct)
            ? (lector.GetInt64(0), lector.GetFieldValue<byte[]>(1))
            : null;
    }

    internal static async Task<List<EventoAuditoriaSellable>> LeerEventosAsync(
        NpgsqlConnection conexion,
        NpgsqlTransaction tx,
        long primera,
        long ultima,
        CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand("""
            SELECT id, seal_seq, schema_name, table_name, row_pk, action,
                   old_row::text, new_row::text, changed_columns, changed_by,
                   changed_at, request_id, client_ip::text
              FROM audit.change_log
             WHERE seal_seq BETWEEN @primera AND @ultima
             ORDER BY seal_seq
            """, conexion, tx);
        comando.Parameters.AddWithValue("primera", primera);
        comando.Parameters.AddWithValue("ultima", ultima);
        await using var lector = await comando.ExecuteReaderAsync(ct);
        var eventos = new List<EventoAuditoriaSellable>();
        while (await lector.ReadAsync(ct))
        {
            var changedAt = lector.GetDateTime(10);
            var columnas = lector.IsDBNull(8) ? null : lector.GetFieldValue<string[]>(8);
            eventos.Add(new(
                lector.GetInt64(0), lector.GetInt64(1), lector.GetString(2), lector.GetString(3),
                lector.GetString(4), lector.GetString(5),
                lector.IsDBNull(6) ? null : lector.GetString(6),
                lector.IsDBNull(7) ? null : lector.GetString(7),
                columnas,
                lector.IsDBNull(9) ? null : lector.GetGuid(9).ToString("D"),
                new DateTimeOffset(DateTime.SpecifyKind(changedAt, DateTimeKind.Utc)),
                lector.IsDBNull(11) ? null : lector.GetString(11),
                lector.IsDBNull(12) ? null : lector.GetString(12)));
        }
        return eventos;
    }

    internal static async Task<byte[]> LeerHashLegacyAsync(
        NpgsqlConnection conexion, NpgsqlTransaction tx, string ambiente, Guid nonce, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand("""
            SELECT jsonb_build_object('corte', b.legacy_max_id, 'observadoEn', extract(epoch from b.observed_at),
                'eventos', COALESCE((SELECT jsonb_agg((to_jsonb(e) || jsonb_build_object(
                    'changed_at', extract(epoch from e.changed_at))) ORDER BY e.id)
                    FROM audit.change_log e WHERE e.seal_seq IS NULL), '[]'::jsonb))::text
            FROM audit.seal_baseline b WHERE b.id=1
            """, conexion, tx);
        var json = await comando.ExecuteScalarAsync(ct) as string
            ?? throw new InvalidDataException("Falta el corte legacy observado.");
        return HasherLotesAuditoria.CalcularHashJson("ars-docendi:legacy-observado:v1:" + ambiente, nonce, json);
    }

    private static byte[] CrearManifiesto(
        string ambiente,
        long primera,
        long ultima,
        int cantidad,
        Guid nonce,
        byte[]? hashPrevio,
        byte[] hash, byte[] hashLegacy, byte[] hashEstado, long cursorEstado)
    {
        var manifiesto = new ManifiestoLote(
            HasherLotesAuditoria.VersionFormato,
            ambiente,
            primera,
            ultima,
            cantidad,
            nonce.ToString("N"),
            hashPrevio is null ? null : Convert.ToHexString(hashPrevio).ToLowerInvariant(),
            Convert.ToHexString(hash).ToLowerInvariant(),
            Convert.ToHexString(hashLegacy).ToLowerInvariant(),
            Convert.ToHexString(hashEstado).ToLowerInvariant(), cursorEstado, CheckpointsEstadoAuditoria.Cobertura);
        return JsonSerializer.SerializeToUtf8Bytes(manifiesto, OpcionesJson);
    }

    private static async Task<long> InsertarPendienteAsync(
        NpgsqlConnection conexion,
        NpgsqlTransaction tx,
        string ambiente,
        long primera,
        long ultima,
        int cantidad,
        Guid nonce,
        byte[] hash,
        byte[]? hashPrevio,
        byte[] manifiesto,
        byte[] hashManifiesto, string estado, long cursorEstado,
        CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand("""
            INSERT INTO audit.seal_batches
                (environment, first_seq, last_seq, event_count, format_version,
                 batch_nonce, batch_hash, previous_batch_hash, manifest, manifest_hash, state_snapshot, state_cursor)
            VALUES
                (@ambiente, @primera, @ultima, @cantidad, @version,
                 @nonce, @hash, @hashPrevio, @manifiesto, @hashManifiesto, @estado::jsonb, @cursorEstado)
            RETURNING id
            """, conexion, tx);
        comando.Parameters.AddWithValue("ambiente", ambiente);
        comando.Parameters.AddWithValue("primera", primera);
        comando.Parameters.AddWithValue("ultima", ultima);
        comando.Parameters.AddWithValue("cantidad", cantidad);
        comando.Parameters.AddWithValue("version", HasherLotesAuditoria.VersionFormato);
        comando.Parameters.AddWithValue("nonce", nonce);
        comando.Parameters.AddWithValue("hash", hash);
        comando.Parameters.AddWithValue("hashPrevio", (object?)hashPrevio ?? DBNull.Value);
        comando.Parameters.AddWithValue("manifiesto", manifiesto);
        comando.Parameters.AddWithValue("hashManifiesto", hashManifiesto);
        comando.Parameters.AddWithValue("estado", estado);
        comando.Parameters.AddWithValue("cursorEstado", cursorEstado);
        return (long)(await comando.ExecuteScalarAsync(ct))!;
    }

    internal static void VerificarLote(
        LoteAuditoriaPreparado lote,
        IReadOnlyCollection<EventoAuditoriaSellable> eventos, byte[] hashLegacy)
    {
        if (eventos.Count != lote.CantidadEventos)
            throw new InvalidDataException("El lote pendiente ya no tiene la cantidad de eventos original.");
        var hashActual = HasherLotesAuditoria.CalcularHash(
            lote.Ambiente, lote.PrimeraSecuencia, lote.UltimaSecuencia,
            lote.Nonce, eventos, lote.HashLoteAnterior);
        if (!CryptographicOperations.FixedTimeEquals(hashActual, lote.HashLote))
            throw new InvalidDataException("Los eventos del lote pendiente difieren del hash persistido.");
        var manifiestoActual = CrearManifiesto(
            lote.Ambiente, lote.PrimeraSecuencia, lote.UltimaSecuencia,
            lote.CantidadEventos, lote.Nonce, lote.HashLoteAnterior, hashActual, hashLegacy,
            CheckpointsEstadoAuditoria.Hash(lote.Ambiente, lote.Nonce, lote.EstadoSnapshot
                ?? throw new InvalidDataException("Falta checkpoint privado.")),
            lote.EstadoCursor ?? throw new InvalidDataException("Falta cursor de estado."));
        if (!manifiestoActual.AsSpan().SequenceEqual(lote.Manifiesto)
            || !CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(manifiestoActual), lote.HashManifiesto))
            throw new InvalidDataException("El manifiesto pendiente difiere de sus metadatos y digest.");
    }

    private sealed record ManifiestoLote(
        int Version,
        string Ambiente,
        long PrimeraSecuencia,
        long UltimaSecuencia,
        int CantidadEventos,
        string Nonce,
        string? HashAnterior,
        string Hash,
        string HashLegacyObservado, string HashEstadoObservado, long CursorEstado, string CoberturaEstado);
}
