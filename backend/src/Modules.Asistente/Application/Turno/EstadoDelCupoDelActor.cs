namespace Modules.Asistente.Application;

/// <summary>
/// El cupo diario de un actor, tal como se le muestra (asistente-cupo-visible,
/// design.md de asistente-administracion-de-uso).
/// </summary>
/// <param name="Restante">
/// Turnos que le quedan hoy. <see cref="int.MaxValue"/> si el cupo está
/// desactivado.
/// </param>
/// <param name="Bloqueado">Si el actor está bloqueado ahora mismo.</param>
/// <param name="Motivo">
/// Uno de <see cref="MotivoPresupuestoPropio"/>, <see cref="MotivoTopeOrganizacional"/>,
/// <see cref="MotivoMantenimiento"/> o <see cref="MotivoSinAcceso"/>. Nulo si no está bloqueado.
/// </param>
/// <param name="VuelveA">Cuándo se destraba, si se sabe.</param>
public sealed record EstadoDelCupoDelActor(int Restante, bool Bloqueado, string? Motivo, DateTimeOffset? VuelveA)
{
    public const string MotivoPresupuestoPropio = "presupuesto_propio";
    public const string MotivoTopeOrganizacional = "tope_organizacional";
    public const string MotivoMantenimiento = "mantenimiento";
    public const string MotivoSinAcceso = "sin_acceso";
}
