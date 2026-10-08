using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Persistencia;
using Modules.Asistente.Application;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// <see cref="IPresupuestosAdministrables"/> sobre Postgres (design.md D6/D11
/// de asistente-administracion-de-uso).
/// </summary>
internal sealed class PresupuestosAdministrablesReal(
    CadenaDuena cadena, TimeProvider reloj, IConsultasIdentity identidad)
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

    public async Task<int?> RestablecerOverrideDeUsuarioAsync(Guid actor, CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        // Cierra la vigencia sin abrir otra (D5): la historia del override
        // queda entera y el actor vuelve a heredar el cupo de su rol.
        await using var cerrar = new NpgsqlCommand(
            """
            UPDATE asistente.presupuesto_usuario
               SET vigente_hasta = @ahora
             WHERE actor_id = @actor AND vigente_hasta IS NULL
            RETURNING cupo_diario_turnos
            """, conexion);
        cerrar.Parameters.AddWithValue("actor", actor);
        cerrar.Parameters.AddWithValue("ahora", reloj.GetUtcNow());

        return await cerrar.ExecuteScalarAsync(ct) is int antes ? antes : null;
    }

    public async Task<(bool Antes, bool Despues)> EditarAccesoDeRolAsync(
        string rol, bool habilitado, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rol);

        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        await using var leer = new NpgsqlCommand(
            "SELECT acceso_habilitado FROM asistente.presupuesto_rol WHERE rol_code = @rol", conexion);
        leer.Parameters.AddWithValue("rol", rol);
        var antes = await leer.ExecuteScalarAsync(ct) is not bool valor || valor;

        // Un rol sin fila todavía nace con cupo 0 (sin tope), el mismo default
        // que la siembra de 006 — cambiar el acceso no inventa un cupo.
        await using var escribir = new NpgsqlCommand(
            """
            INSERT INTO asistente.presupuesto_rol (rol_code, cupo_diario_turnos, acceso_habilitado, actualizado_en)
            VALUES (@rol, 0, @habilitado, now())
            ON CONFLICT (rol_code) DO UPDATE
                SET acceso_habilitado = @habilitado, actualizado_en = now()
            """, conexion);
        escribir.Parameters.AddWithValue("rol", rol);
        escribir.Parameters.AddWithValue("habilitado", habilitado);
        await escribir.ExecuteNonQueryAsync(ct);

        return (antes, habilitado);
    }

    public async Task<(bool RevocadoAntes, bool RevocadoDespues)> EditarAccesoDeUsuarioAsync(
        Guid actor, bool habilitado, Guid administrador, CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        await using var leer = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM asistente.acceso_usuario_revocado WHERE actor_id = @actor)",
            conexion);
        leer.Parameters.AddWithValue("actor", actor);
        var revocadoAntes = (bool)(await leer.ExecuteScalarAsync(ct))!;

        await using var escribir = habilitado
            ? new NpgsqlCommand(
                "DELETE FROM asistente.acceso_usuario_revocado WHERE actor_id = @actor", conexion)
            : new NpgsqlCommand(
                """
                INSERT INTO asistente.acceso_usuario_revocado (actor_id, revocado_por, revocado_en)
                VALUES (@actor, @administrador, @ahora)
                ON CONFLICT (actor_id) DO NOTHING
                """, conexion);
        escribir.Parameters.AddWithValue("actor", actor);
        if (!habilitado)
        {
            escribir.Parameters.AddWithValue("administrador", administrador);
            escribir.Parameters.AddWithValue("ahora", reloj.GetUtcNow());
        }

        await escribir.ExecuteNonQueryAsync(ct);

        return (revocadoAntes, !habilitado);
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

    public async Task<EstadoDePresupuestos> ObtenerEstadoAsync(CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        var tope = await TopeVigenteAsync(conexion, ct);
        var cuposPorRol = await CuposPorRolAsync(conexion, ct);
        var overrides = await OverridesDeUsuarioAsync(conexion, ct);
        var revocados = await AccesosRevocadosAsync(conexion, ct);

        // Sólo resuelve nombres si hace falta — mismo criterio de "no pedir lo
        // que no se va a usar" que ya sigue ConsultasDeUso (design.md D12).
        var nombres = overrides.Count == 0
            ? new Dictionary<Guid, string>()
            : (await identidad.ListarUsuariosAsync(ct)).ToDictionary(u => u.Id, u => u.NombreParaMostrar);

        var overridesConNombre = overrides
            .Select(o => new OverrideDeUsuarioVigente(o.ActorId, nombres.GetValueOrDefault(o.ActorId), o.Cupo))
            .ToList();

        return new EstadoDePresupuestos(tope, cuposPorRol, overridesConNombre, revocados);
    }

    private static async Task<decimal> TopeVigenteAsync(NpgsqlConnection conexion, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(
            """
            SELECT tope_mensual_usd FROM asistente.tope_organizacional
             WHERE vigente_desde <= now()
             ORDER BY vigente_desde DESC
             LIMIT 1
            """, conexion);

        var valor = await comando.ExecuteScalarAsync(ct);
        return valor is null or DBNull ? 0m : (decimal)valor;
    }

    private static async Task<List<CupoDeRolVigente>> CuposPorRolAsync(
        NpgsqlConnection conexion, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(
            "SELECT rol_code, cupo_diario_turnos, acceso_habilitado FROM asistente.presupuesto_rol ORDER BY rol_code",
            conexion);

        var cupos = new List<CupoDeRolVigente>();
        await using var lector = await comando.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
        {
            cupos.Add(new CupoDeRolVigente(lector.GetString(0), lector.GetInt32(1), lector.GetBoolean(2)));
        }

        return cupos;
    }

    private static async Task<List<(Guid ActorId, int Cupo)>> OverridesDeUsuarioAsync(
        NpgsqlConnection conexion, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(
            """
            SELECT actor_id, cupo_diario_turnos
              FROM asistente.presupuesto_usuario
             WHERE vigente_hasta IS NULL
             ORDER BY actor_id
            """, conexion);

        var overrides = new List<(Guid, int)>();
        await using var lector = await comando.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
        {
            overrides.Add((lector.GetGuid(0), lector.GetInt32(1)));
        }

        return overrides;
    }

    private static async Task<List<Guid>> AccesosRevocadosAsync(NpgsqlConnection conexion, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(
            "SELECT actor_id FROM asistente.acceso_usuario_revocado ORDER BY actor_id", conexion);

        var revocados = new List<Guid>();
        await using var lector = await comando.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
        {
            revocados.Add(lector.GetGuid(0));
        }

        return revocados;
    }
}
