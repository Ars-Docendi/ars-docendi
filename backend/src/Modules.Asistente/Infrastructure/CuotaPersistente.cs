using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Logging;
using Modules.Asistente.Application;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Cupo diario de turnos por actor, persistido en Postgres
/// (asistente-presupuesto-persistente, design.md D2/D4 de
/// asistente-administracion-de-uso).
/// </summary>
/// <remarks>
/// Pide <see cref="CadenaDuena"/> y no una de solo lectura: los dos roles del
/// asistente tienen el schema <c>asistente</c> revocado entero (definición
/// §3.4), y esta clase lee <c>presupuesto_rol</c>/<c>presupuesto_usuario</c> y
/// cuenta filas de <c>registro_operativo</c> — ninguna de las dos cosas la
/// puede hacer un rol de solo lectura.
///
/// <b>De dónde sale el cupo EFECTIVO de un actor, y por qué (decidido acá,
/// design.md lo deja abierto).</b> Primero se busca un override vigente en
/// <c>presupuesto_usuario</c> (<c>vigente_hasta IS NULL</c>): si existe, GANA
/// siempre, más chico o más grande que el default de rol (tarea 3.4). Si no
/// hay override, se resuelven los roles de sistema vigentes del actor
/// (<see cref="IConsultasIdentity.ObtenerCodigosDeRolesDeSistemaAsync"/>) y se
/// toma el MÍNIMO cupo entre los roles cuyo default está <b>activado</b>
/// (mayor que cero), ignorando los roles cuyo default es cero. La regla
/// "mínimo entre los activados" y no "el mayor gana" es deliberada: un cero
/// significa "este rol no impone tope", no "el tope más bajo posible" — si
/// significara lo segundo, cualquier actor con un rol sin tope arrastraría a
/// todos sus otros roles a cero por accidente de ordenamiento numérico, que es
/// exactamente lo opuesto de lo que un control de costo tiene que hacer. Un
/// actor donde TODOS sus roles están en cero (el default de la siembra) queda
/// sin tope, que es el comportamiento esperado hasta que el Departamento
/// confirme números reales.
/// </remarks>
internal sealed class CuotaPersistente(
    CadenaDuena cadena,
    IConsultasIdentity identidad,
    TimeProvider reloj,
    DetectorDeUmbrales detector,
    ILogger<CuotaPersistente> log)
    : ICuotaDelActor
{
    public async Task<bool> HayCupoAsync(Guid actor, CancellationToken ct)
    {
        var cupo = await CupoEfectivoAsync(actor, ct);
        if (cupo <= 0)
        {
            return true;
        }

        return await TurnosDeHoyAsync(actor, ct) < cupo;
    }

    public async Task AnotarAsync(Guid actor, CancellationToken ct)
    {
        // Ver el XML-doc de ICuotaDelActor.AnotarAsync: el consumo se deriva
        // contando registro_operativo, así que no hay nada que ESCRIBIR acá.
        // Lo que sí corresponde acá (tarea 9.7) es el aviso de umbral: para
        // este momento, la fila que este turno ya escribió (vía
        // IRegistroDelTurno, que corre ANTES en el mismo finally) ya cuenta.
        var cupo = await CupoEfectivoAsync(actor, ct);
        if (cupo <= 0)
        {
            return;
        }

        var usados = await TurnosDeHoyAsync(actor, ct);
        detector.RegistrarSiCorresponde(
            log, "usuario", actor.ToString(), FechaDeHoy(reloj.GetUtcNow()), 100.0 * usados / cupo);
    }

    public async Task<DateTimeOffset?> CupoVuelveAAsync(Guid actor, CancellationToken ct)
    {
        var cupo = await CupoEfectivoAsync(actor, ct);
        if (cupo <= 0)
        {
            return null;
        }

        if (await TurnosDeHoyAsync(actor, ct) < cupo)
        {
            return null;
        }

        return InicioDelProximoDiaUtc(reloj.GetUtcNow());
    }

    public async Task<int> CupoRestanteAsync(Guid actor, CancellationToken ct)
    {
        var cupo = await CupoEfectivoAsync(actor, ct);
        if (cupo <= 0)
        {
            return int.MaxValue;
        }

        var usados = await TurnosDeHoyAsync(actor, ct);
        return Math.Max(0, cupo - usados);
    }

    /// <summary>El principio del día calendario UTC de <paramref name="momento"/>.</summary>
    private static DateTimeOffset InicioDelDiaUtc(DateTimeOffset momento) =>
        new(DateOnly.FromDateTime(momento.UtcDateTime).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static DateTimeOffset InicioDelProximoDiaUtc(DateTimeOffset momento) =>
        InicioDelDiaUtc(momento).AddDays(1);

    /// <summary>La clave de período del detector de umbrales: el día calendario UTC.</summary>
    private static string FechaDeHoy(DateTimeOffset momento) =>
        DateOnly.FromDateTime(momento.UtcDateTime).ToString("yyyy-MM-dd");

    /// <summary>
    /// Turnos consumidos hoy: filas de <c>registro_operativo</c> del actor, del día
    /// calendario UTC en curso, que efectivamente invocaron al modelo (D4).
    /// </summary>
    /// <remarks>
    /// Contar acá y no en una tabla propia es la decisión que design.md deja
    /// abierta en la tarea 3.2, tomada a favor de <c>registro_operativo</c>
    /// directo: una segunda fuente de verdad (un contador propio) podría
    /// desincronizarse del registro real, y <c>registro_operativo</c> ya es
    /// la fuente autorizada de "cuántos turnos hizo este actor".
    /// </remarks>
    private async Task<int> TurnosDeHoyAsync(Guid actor, CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        await using var comando = new NpgsqlCommand(
            """
            SELECT count(*)
              FROM asistente.registro_operativo
             WHERE actor_id = @actor
               AND llamadas_al_modelo > 0
               AND ocurrido_en >= @inicio_del_dia
            """, conexion);

        comando.Parameters.AddWithValue("actor", actor);
        comando.Parameters.AddWithValue("inicio_del_dia", InicioDelDiaUtc(reloj.GetUtcNow()));

        return (int)(long)(await comando.ExecuteScalarAsync(ct))!;
    }

    private async Task<int> CupoEfectivoAsync(Guid actor, CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        var deOverride = await CupoDeOverrideAsync(conexion, actor, ct);
        if (deOverride is { } cupo)
        {
            return cupo;
        }

        var codigosDeRol = await identidad.ObtenerCodigosDeRolesDeSistemaAsync(actor, ct);
        if (codigosDeRol.Count == 0)
        {
            return 0;
        }

        return await CupoMinimoDeRolesActivadosAsync(conexion, codigosDeRol, ct);
    }

    private static async Task<int?> CupoDeOverrideAsync(
        NpgsqlConnection conexion, Guid actor, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(
            """
            SELECT cupo_diario_turnos
              FROM asistente.presupuesto_usuario
             WHERE actor_id = @actor
               AND vigente_hasta IS NULL
             ORDER BY vigente_desde DESC
             LIMIT 1
            """, conexion);
        comando.Parameters.AddWithValue("actor", actor);

        var valor = await comando.ExecuteScalarAsync(ct);
        return valor is null ? null : (int)valor;
    }

    private static async Task<int> CupoMinimoDeRolesActivadosAsync(
        NpgsqlConnection conexion, IReadOnlyList<string> codigosDeRol, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(
            """
            SELECT min(cupo_diario_turnos)
              FROM asistente.presupuesto_rol
             WHERE rol_code = ANY(@roles)
               AND cupo_diario_turnos > 0
            """, conexion);
        comando.Parameters.AddWithValue("roles", codigosDeRol.ToArray());

        var valor = await comando.ExecuteScalarAsync(ct);
        return valor is null or DBNull ? 0 : (int)valor;
    }
}
