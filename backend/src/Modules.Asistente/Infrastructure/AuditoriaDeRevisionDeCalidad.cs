using ArsDocendi.Shared.Persistencia;
using Modules.Asistente.Application;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// <see cref="IAuditoriaDeRevisionDeCalidad"/>: one row in
/// <c>asistente.auditoria_revision_de_calidad</c> per successful read of the problem list (design
/// D5 of asistente-metricas-de-calidad).
/// </summary>
internal sealed class AuditoriaDeRevisionDeCalidad(CadenaDuena cadena) : IAuditoriaDeRevisionDeCalidad
{
    public async Task RegistrarAsync(Guid lector, string filtros, int filas, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filtros);
        ArgumentOutOfRangeException.ThrowIfNegative(filas);

        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.auditoria_revision_de_calidad (lector_id, ocurrido_en, filtros, filas)
            VALUES (@lector, now(), @filtros, @filas)
            """, conexion);
        comando.Parameters.AddWithValue("lector", lector);
        comando.Parameters.AddWithValue("filtros", filtros);
        comando.Parameters.AddWithValue("filas", filas);

        await comando.ExecuteNonQueryAsync(ct);
    }
}
