namespace Modules.Asistente.Application;

/// <summary>El cupo diario default de UN rol de sistema, tal como está persistido ahora.</summary>
public sealed record CupoDeRolVigente(string Rol, int CupoDiarioTurnos);

/// <summary>
/// El override de cupo diario de un actor puntual, vigente ahora
/// (<c>vigente_hasta IS NULL</c>).
/// </summary>
/// <param name="NombreParaMostrar">
/// Resuelto vía <see cref="ArsDocendi.Shared.Identity.IConsultasIdentity.ListarUsuariosAsync"/>
/// — el mismo seam que <c>IConsultasDeUso</c> usa para el panel de uso
/// (design.md D12 de asistente-administracion-de-uso). Nulo si el actor no
/// aparece en esa lista (por ejemplo, una cuenta dada de baja).
/// </param>
public sealed record OverrideDeUsuarioVigente(Guid ActorId, string? NombreParaMostrar, int CupoDiarioTurnos);

/// <summary>
/// El estado persistido de todos los presupuestos: el tope organizacional
/// vigente, el cupo default de cada rol de sistema, y cada override de
/// usuario vigente (tarea 12.8 de sistema-seccion-unificada — cierra el gap
/// de "sólo <c>PUT</c>, nunca <c>GET</c>" que documentaban
/// <c>TopeOrganizacionalCard</c>/<c>EditorDeCupoEnFila</c>).
/// </summary>
/// <remarks>
/// NO incluye el gasto del mes: eso lo calcula el panel de uso
/// (<see cref="IConsultasDeUso"/>) con el mismo mecanismo de costeo
/// versionado, para que ambos números nunca puedan discreparse.
/// </remarks>
public sealed record EstadoDePresupuestos(
    decimal TopeMensualUsd,
    IReadOnlyList<CupoDeRolVigente> CuposPorRol,
    IReadOnlyList<OverrideDeUsuarioVigente> OverridesPorUsuario);

/// <summary>
/// Edita los presupuestos persistentes (default de rol, override de usuario,
/// tope organizacional) y devuelve el par antes/después para que quien llama
/// (el controller) lo audite (asistente-auditoria-de-administracion).
/// </summary>
/// <remarks>
/// El puerto NO audita — eso es responsabilidad del controller, igual que el
/// toggle de mantenimiento (design.md D7): mover el almacenamiento de estos
/// valores no debería arrastrar la auditoría con él.
/// </remarks>
public interface IPresupuestosAdministrables
{
    /// <summary>Edita el cupo diario default de un código de rol de sistema.</summary>
    Task<(int Antes, int Despues)> EditarCupoDeRolAsync(string rol, int cupo, CancellationToken ct);

    /// <summary>
    /// Edita el override de cupo diario de un actor puntual. <c>Antes</c> es
    /// nulo si el actor no tenía override vigente.
    /// </summary>
    Task<(int? Antes, int Despues)> EditarOverrideDeUsuarioAsync(Guid actor, int cupo, CancellationToken ct);

    /// <summary>Edita el tope organizacional de gasto mensual, en USD.</summary>
    Task<(decimal Antes, decimal Despues)> EditarTopeOrganizacionalAsync(decimal tope, CancellationToken ct);

    /// <summary>
    /// Lee el estado vigente de todos los presupuestos: el tope
    /// organizacional, el cupo default de cada rol de sistema, y cada
    /// override de usuario vigente (tarea 12.8 de sistema-seccion-unificada).
    /// </summary>
    Task<EstadoDePresupuestos> ObtenerEstadoAsync(CancellationToken ct);
}
