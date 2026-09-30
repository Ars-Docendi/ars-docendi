using System.Data;
using ArsDocendi.Shared.Auditing;
using Npgsql;

namespace ArsDocendi.Host.Auditoria;

/// <summary>
/// Compara el último sello persistido con ambos custodios ANTES de crear o enviar otro.
/// Permite que un acuse parcial se reintente, pero nunca acepta un cursor remoto
/// que la base restaurada desconoce. No sustituye el aislamiento real de custodios.
/// </summary>
public sealed class CompuertaPublicacionAuditoria(
    NpgsqlDataSource datos, VerificadorTestigosRemotos testigos)
{
    public void VerificarFirma(byte[] hash, byte[] firma, string clave) =>
        testigos.VerificarFirma(hash, firma, clave);

    public async Task ValidarAsync(string ambiente, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ambiente);
        await using var conexion = await datos.OpenConnectionAsync(ct);
        await using var tx = await conexion.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await using (var lectura = new NpgsqlCommand("SET TRANSACTION READ ONLY", conexion, tx))
            await lectura.ExecuteNonQueryAsync(ct);
        var local = await VerificadorLotesAuditoria.VerificarSnapshotAsync(conexion, tx, ambiente, ct);
        if (!local.HashesLocalesValidos)
            throw new InvalidDataException("La cadena local no es íntegra; no publicar.");

        var lotes = new List<LoteLocal>();
        await using (var cmd = new NpgsqlCommand("""
            SELECT first_seq, last_seq, manifest_hash,
                   primary_witnessed_at IS NOT NULL AND secondary_witnessed_at IS NOT NULL,
                   signature, signing_key_id,
                   primary_witnessed_at IS NOT NULL, secondary_witnessed_at IS NOT NULL
              FROM audit.seal_batches WHERE environment = @ambiente ORDER BY id
            """, conexion, tx))
        {
            cmd.Parameters.AddWithValue("ambiente", ambiente);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                lotes.Add(new(reader.GetInt64(0), reader.GetInt64(1), reader.GetFieldValue<byte[]>(2),
                    reader.GetBoolean(3), reader.IsDBNull(4) ? null : reader.GetFieldValue<byte[]>(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetBoolean(6), reader.GetBoolean(7)));
        }
        var completo = lotes.LastOrDefault(l => l.Completo);
        var pendientes = lotes.Where(l => !l.Completo).ToArray();
        if (pendientes.Length > 1 || (pendientes.Length == 1 && lotes[^1] != pendientes[0]))
            throw new InvalidDataException("Más de un sello pendiente o cadena local inconsistente.");
        var pendiente = pendientes.SingleOrDefault();
        if (completo is null)
        {
            await using var cmd = new NpgsqlCommand("SELECT status FROM audit.seal_baseline WHERE id=1", conexion, tx);
            if (await cmd.ExecuteScalarAsync(ct) as string != "observed")
                throw new InvalidDataException("Se perdió una cadena previamente anclada; reconciliar.");
        }

        string? claveAnterior = null;
        foreach (var lote in lotes.Where(l => l.Completo))
        {
            if (lote.Firma is null || lote.IdClave is null)
                throw new InvalidDataException("Sello completo sin firma verificable.");
            if (claveAnterior is not null && claveAnterior != lote.IdClave)
                testigos.VerificarTransicion(ambiente, claveAnterior, lote.IdClave);
            testigos.VerificarFirma(lote.Hash, lote.Firma, lote.IdClave);
            claveAnterior = lote.IdClave;
        }
        if (pendiente?.Firma is not null && pendiente.IdClave is not null)
        {
            if (claveAnterior is not null && claveAnterior != pendiente.IdClave)
                testigos.VerificarTransicion(ambiente, claveAnterior, pendiente.IdClave);
            testigos.VerificarFirma(pendiente.Hash, pendiente.Firma, pendiente.IdClave);
        }

        var remoto = await testigos.LeerParaPublicarAsync(ambiente, ct);
        if (completo is null && pendiente is null)
        {
            if (remoto.Primario is not null || remoto.Secundario is not null)
                throw new InvalidDataException("La base no está en génesis o existen sellos remotos; reconciliar.");
            return;
        }
        // Un custodio puede haber aceptado el pendiente antes de que se registre
        // su acuse local. Sólo se admiten el último completo o ESE pendiente.
        ValidarUno(remoto.Primario, completo, pendiente, local.CursorActual, pendiente?.PrimarioConfirmado == true);
        ValidarUno(remoto.Secundario, completo, pendiente, local.CursorActual, pendiente?.SecundarioConfirmado == true);
        if (pendiente is null && (remoto.Primario is null || remoto.Secundario is null))
            throw new InvalidDataException("Falta un sello remoto ya registrado como completo.");
        await tx.CommitAsync(ct);
    }

    private static void ValidarUno(EvidenciaTestigoRemoto? remoto, LoteLocal? completo,
        LoteLocal? pendiente, long cursor, bool pendienteConfirmado)
    {
        if (remoto is null)
        {
            if (completo is not null || pendienteConfirmado)
                throw new InvalidDataException("Desapareció un sello remoto confirmado.");
            return;
        }
        if (remoto.UltimaSecuencia > cursor)
            throw new InvalidDataException("Backup atrasado respecto del custodio; reconciliar antes de publicar.");
        if (Coincide(pendiente, remoto))
        {
            if (pendiente!.Firma is null || pendiente.IdClave is null)
                throw new InvalidDataException("Testigo adelantado a un lote local sin firma recuperable.");
            return;
        }
        if (pendienteConfirmado || !Coincide(completo, remoto))
            throw new InvalidDataException("Sello remoto desconocido para la base local; reconciliar.");
    }

    private static bool Coincide(LoteLocal? lote, EvidenciaTestigoRemoto e) =>
        lote is not null && lote.Primera == e.PrimeraSecuencia && lote.Ultima == e.UltimaSecuencia
        && Convert.ToHexString(lote.Hash).Equals(e.HashManifiesto, StringComparison.OrdinalIgnoreCase);

    private sealed record LoteLocal(long Primera, long Ultima, byte[] Hash, bool Completo,
        byte[]? Firma, string? IdClave, bool PrimarioConfirmado, bool SecundarioConfirmado);
}
