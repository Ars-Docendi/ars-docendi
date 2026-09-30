using System.Text.Json;
using Npgsql;
namespace ArsDocendi.Shared.Auditing;

/// <summary>Inventario parcial v1: identity.roles, todas las columnas. Snapshots sólo locales.</summary>
public static class CheckpointsEstadoAuditoria
{
    public const string Cobertura = "parcial:identity.roles:*:v1";

    public static async Task<string> CapturarAsync(NpgsqlConnection conexion, NpgsqlTransaction tx, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT COALESCE(jsonb_agg(to_jsonb(r) ORDER BY r.id), '[]'::jsonb)::text FROM identity.roles r
            """, conexion, tx);
        return (string)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public static byte[] Hash(string ambiente, Guid nonce, string snapshot) =>
        HasherLotesAuditoria.CalcularHashJson("ars-docendi:estado-observado:" + Cobertura + ":" + ambiente, nonce, snapshot);

    public static async Task VerificarActualAsync(NpgsqlConnection conexion, NpgsqlTransaction tx,
        string snapshot, long cursor, long cursorActual, CancellationToken ct)
    {
        if (cursor > cursorActual) throw new InvalidDataException("El estado restaurado retrocedió respecto del checkpoint.");
        using var baseline = JsonDocument.Parse(snapshot);
        var filas = baseline.RootElement.EnumerateArray().ToDictionary(
            e => e.GetProperty("id").GetString()!, e => e.GetRawText(), StringComparer.Ordinal);
        // Reproducir TODOS los cambios desde la línea base, validando old_row:
        // una modificación directa seguida por UPDATE legítimo tampoco se blanquea.
        await using (var cmd = new NpgsqlCommand("""
            SELECT row_pk, action, old_row::text, new_row::text FROM audit.change_log
            WHERE schema_name='identity' AND table_name='roles' AND seal_seq > @cursor
            ORDER BY seal_seq
            """, conexion, tx))
        {
            cmd.Parameters.AddWithValue("cursor", cursor);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var id = reader.GetString(0);
                var accion = reader.GetString(1);
                if (accion == "INSERT")
                {
                    if (filas.ContainsKey(id)) throw new InvalidDataException("Alta duplicada respecto del checkpoint.");
                }
                else if (!filas.TryGetValue(id, out var anterior) || reader.IsDBNull(2)
                    || !Equivalentes(anterior, reader.GetString(2)))
                    throw new InvalidDataException("El evento no continúa el estado anclado.");
                if (accion == "DELETE") filas.Remove(id);
                else filas[id] = reader.GetString(3);
            }
        }
        using var actual = JsonDocument.Parse(await CapturarAsync(conexion, tx, ct));
        if (actual.RootElement.GetArrayLength() != filas.Count)
            throw new InvalidDataException("Cantidad de filas distinta del checkpoint más eventos.");
        foreach (var fila in actual.RootElement.EnumerateArray())
            if (!filas.TryGetValue(fila.GetProperty("id").GetString()!, out var esperada)
                || !Equivalentes(esperada, fila.GetRawText()))
                throw new InvalidDataException("Divergencia del estado observado; requiere reconciliación.");
    }

    private static bool Equivalentes(string a, string b) =>
        HasherLotesAuditoria.CalcularHashJson("comparacion-interna", Guid.Empty, a).AsSpan().SequenceEqual(
            HasherLotesAuditoria.CalcularHashJson("comparacion-interna", Guid.Empty, b));
}
