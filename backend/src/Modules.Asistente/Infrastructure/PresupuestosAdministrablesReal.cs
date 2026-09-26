using ArsDocendi.Shared.Persistencia;
using Modules.Asistente.Application;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// <see cref="IPresupuestosAdministrables"/> sobre Postgres (design.md D6/D11
/// de asistente-administracion-de-uso).
/// </summary>
internal sealed class PresupuestosAdministrablesReal(CadenaDuena cadena, TimeProvider reloj)
    : IPresupuestosAdministrables
{
    public async Task<(int Antes, int Despues)> EditarCupoDeRolAsync(
        string rol, int cupo, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rol);

        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        await using var leer = new NpgsqlCommand(
            "SELECT cupo_diario_turnos FROM asistente.presupuesto_rol WHERE rol_code = @rol", conexion);
        leer.Parameters.AddWithValue("rol", rol);
        var antes = (int)(await leer.ExecuteScalarAsync(ct) ?? 0);

        await using var escribir = new NpgsqlCommand(
            """
            INSERT INTO asistente.presupuesto_rol (rol_code, cupo_diario_turnos, actualizado_en)
            VALUES (@rol, @cupo, now())
            ON CONFLICT (rol_code) DO UPDATE
                SET cupo_diario_turnos = @cupo, actualizado_en = now()
            """, conexion);
        escribir.Parameters.AddWithValue("rol", rol);
        escribir.Parameters.AddWithValue("cupo", cupo);
        await escribir.ExecuteNonQueryAsync(ct);

        return (antes, cupo);
    }

    public async Task<(int? Antes, int Despues)> EditarOverrideDeUsuarioAsync(
        Guid actor, int cupo, CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);
        await using var transaccion = await conexion.BeginTransactionAsync(ct);

        await using var leer = new NpgsqlCommand(
            """
            SELECT cupo_diario_turnos FROM asistente.presupuesto_usuario
             WHERE actor_id = @actor AND vigente_hasta IS NULL
            """, conexion, transaccion);
        leer.Parameters.AddWithValue("actor", actor);
        var antes = await leer.ExecuteScalarAsync(ct) is int valor ? valor : (int?)null;

        var ahora = reloj.GetUtcNow();

        // Cierra la vigencia anterior (si había) y abre una nueva — nunca se
        // edita en el lugar (design.md D6, mismo criterio que tabla_de_precios).
        await using var cerrar = new NpgsqlCommand(
            """
            UPDATE asistente.presupuesto_usuario
               SET vigente_hasta = @ahora
             WHERE actor_id = @actor AND vigente_hasta IS NULL
            """, conexion, transaccion);
        cerrar.Parameters.AddWithValue("actor", actor);
        cerrar.Parameters.AddWithValue("ahora", ahora);
        await cerrar.ExecuteNonQueryAsync(ct);

        await using var abrir = new NpgsqlCommand(
            """
            INSERT INTO asistente.presupuesto_usuario (actor_id, cupo_diario_turnos, vigente_desde, vigente_hasta)
            VALUES (@actor, @cupo, @ahora, NULL)
            """, conexion, transaccion);
        abrir.Parameters.AddWithValue("actor", actor);
        abrir.Parameters.AddWithValue("cupo", cupo);
        abrir.Parameters.AddWithValue("ahora", ahora);
        await abrir.ExecuteNonQueryAsync(ct);

        await transaccion.CommitAsync(ct);

        return (antes, cupo);
    }

    public async Task<(decimal Antes, decimal Despues)> EditarTopeOrganizacionalAsync(
        decimal tope, CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        await using var leer = new NpgsqlCommand(
            """
            SELECT tope_mensual_usd FROM asistente.tope_organizacional
             WHERE vigente_desde <= now()
             ORDER BY vigente_desde DESC
             LIMIT 1
            """, conexion);
        var antes = (decimal)(await leer.ExecuteScalarAsync(ct) ?? 0m);

        await using var escribir = new NpgsqlCommand(
            "INSERT INTO asistente.tope_organizacional (tope_mensual_usd, vigente_desde) VALUES (@tope, now())",
            conexion);
        escribir.Parameters.AddWithValue("tope", tope);
        await escribir.ExecuteNonQueryAsync(ct);

        return (antes, tope);
    }
}
