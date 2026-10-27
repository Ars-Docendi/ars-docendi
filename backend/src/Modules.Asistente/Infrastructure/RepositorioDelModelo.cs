using ArsDocendi.Shared.Persistencia;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>The stored runtime selection. Absence means server configuration.</summary>
internal sealed record SeleccionDeModelo(
    string Empresa, string Modelo, Guid ActorId, DateTimeOffset ActualizadoEn);

/// <summary>One entry of the repository-defined model catalog.</summary>
internal sealed record ModeloDelCatalogo(string Empresa, string Modelo);

/// <summary>
/// The runtime model selection (<c>asistente.modelo_vigente</c>) and the model catalog
/// (<c>asistente.catalogo_de_modelos</c>) (asistente-modelo-en-caliente, D3/D5).
/// </summary>
internal sealed class RepositorioDelModelo(CadenaDuena cadena, TimeProvider reloj)
{
    /// <summary>The stored selection, or null when the module runs on server configuration.</summary>
    public async Task<SeleccionDeModelo?> LeerSeleccionAsync(CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        await using var comando = new NpgsqlCommand(
            "SELECT empresa, modelo, actor_id, actualizado_en FROM asistente.modelo_vigente WHERE id = 1",
            conexion);

        await using var lector = await comando.ExecuteReaderAsync(ct);
        return await lector.ReadAsync(ct)
            ? new SeleccionDeModelo(
                lector.GetString(0),
                lector.GetString(1),
                lector.GetGuid(2),
                lector.GetFieldValue<DateTimeOffset>(3))
            : null;
    }

    /// <summary>
    /// Stores the selection if, and only if, <paramref name="empresa"/> and <paramref
    /// name="modelo"/> are in the catalog.
    /// </summary>
    public async Task<bool> AplicarSeleccionAsync(
        string empresa, string modelo, Guid actor, CancellationToken ct, string? modeloDelEntorno = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(empresa);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelo);

        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        // The catalog check and the write are one statement: no window between
        // "it was in the catalog" and "it was stored".
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.modelo_vigente (id, empresa, modelo, actor_id, actualizado_en)
            SELECT 1, @empresa, @modelo, @actor, @ahora
             WHERE EXISTS (SELECT 1 FROM asistente.catalogo_de_modelos c
                            WHERE c.empresa = @empresa AND c.modelo = @modelo)
                OR (@entorno::text IS NOT NULL AND @entorno::text = @modelo)
            ON CONFLICT (id) DO UPDATE
                SET empresa = EXCLUDED.empresa, modelo = EXCLUDED.modelo,
                    actor_id = EXCLUDED.actor_id, actualizado_en = EXCLUDED.actualizado_en
            """, conexion);
        comando.Parameters.AddWithValue("empresa", empresa);
        comando.Parameters.AddWithValue("modelo", modelo);
        comando.Parameters.AddWithValue("entorno", (object?)modeloDelEntorno ?? DBNull.Value);
        comando.Parameters.AddWithValue("actor", actor);
        comando.Parameters.AddWithValue("ahora", reloj.GetUtcNow());

        return await comando.ExecuteNonQueryAsync(ct) == 1;
    }

    /// <summary>Deletes the selection: back to server configuration. False if there was none.</summary>
    public async Task<bool> RestablecerAsync(CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        await using var comando = new NpgsqlCommand(
            "DELETE FROM asistente.modelo_vigente WHERE id = 1", conexion);

        return await comando.ExecuteNonQueryAsync(ct) == 1;
    }

    /// <summary>The whole catalog, ordered by company and model.</summary>
    public async Task<IReadOnlyList<ModeloDelCatalogo>> ListarCatalogoAsync(CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        await using var comando = new NpgsqlCommand(
            "SELECT empresa, modelo FROM asistente.catalogo_de_modelos ORDER BY empresa, modelo",
            conexion);

        var modelos = new List<ModeloDelCatalogo>();
        await using var lector = await comando.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
        {
            modelos.Add(new ModeloDelCatalogo(lector.GetString(0), lector.GetString(1)));
        }

        return modelos;
    }
}
