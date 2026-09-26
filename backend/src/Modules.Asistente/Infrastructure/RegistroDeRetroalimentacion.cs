using ArsDocendi.Shared.Persistencia;
using Modules.Asistente.Application;
using Npgsql;
using NpgsqlTypes;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Writes to <c>asistente.retroalimentacion_turno</c>.
/// </summary>
/// <remarks>
/// Takes <see cref="CadenaDuena"/> and not a read-only connection, for the same
/// reason <see cref="RegistroDelTurno"/> does: the two read-only roles have the
/// whole <c>asistente</c> schema revoked, so they could not write here even if
/// asked to.
/// </remarks>
internal sealed class RegistroDeRetroalimentacion(CadenaDuena cadena) : IRegistroDeRetroalimentacion
{
    // ON CONFLICT (analitico_id) DO UPDATE: one row per turn, last vote wins, no
    // history kept. This is the whole of design.md D4, expressed as SQL rather
    // than as a code sketch. `razon` (the retired single-reason column) is
    // deliberately never written here anymore — see 003_asistente_retroalimentacion.sql.
    private const string Upsert = """
        INSERT INTO asistente.retroalimentacion_turno (analitico_id, voto, razones, comentario, actualizado_en)
        VALUES (@analitico_id, @voto, @razones, @comentario, @actualizado_en)
        ON CONFLICT (analitico_id) DO UPDATE
        SET voto = EXCLUDED.voto,
            razones = EXCLUDED.razones,
            comentario = EXCLUDED.comentario,
            actualizado_en = EXCLUDED.actualizado_en
        """;

    public async Task GuardarAsync(
        Guid analiticoId,
        bool voto,
        IReadOnlyList<string>? razones,
        string? comentario,
        DateTimeOffset ahora,
        CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        await using var comando = new NpgsqlCommand(Upsert, conexion);
        comando.Parameters.AddWithValue("analitico_id", analiticoId);
        comando.Parameters.AddWithValue("voto", voto);
        var parametroRazones = comando.Parameters.Add(
            "razones", NpgsqlDbType.Array | NpgsqlDbType.Text);
        parametroRazones.Value = (object?)razones?.ToArray() ?? DBNull.Value;
        comando.Parameters.AddWithValue("comentario", (object?)comentario ?? DBNull.Value);
        comando.Parameters.AddWithValue("actualizado_en", ahora);

        await comando.ExecuteNonQueryAsync(ct);
    }
}
