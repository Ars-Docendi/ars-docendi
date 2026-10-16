using ArsDocendi.Shared.Persistencia;
using Modules.Asistente.Application;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Escribe <c>asistente.auditoria_administracion</c> (design.md D11 de
/// asistente-administracion-de-uso).
/// </summary>
internal sealed class AuditoriaDeAdministracionReal(CadenaDuena cadena, TimeProvider reloj)
    : IAuditoriaDeAdministracion
{
    public async Task RegistrarAsync(
        Guid actor, string accion, string? antes, string despues, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accion);
        ArgumentNullException.ThrowIfNull(despues);

        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.auditoria_administracion (actor_id, ocurrido_en, accion, antes, despues)
            VALUES (@actor, @ocurrido_en, @accion, @antes, @despues)
            """, conexion);
        comando.Parameters.AddWithValue("actor", actor);
        comando.Parameters.AddWithValue("ocurrido_en", reloj.GetUtcNow());
        comando.Parameters.AddWithValue("accion", accion);
        comando.Parameters.AddWithValue("antes", (object?)antes ?? DBNull.Value);
        comando.Parameters.AddWithValue("despues", despues);

        await comando.ExecuteNonQueryAsync(ct);
    }
}
