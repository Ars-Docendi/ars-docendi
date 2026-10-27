namespace Modules.Asistente.Application;

/// <summary>
/// El cupo diario de un actor, tal como se le muestra (asistente-cupo-visible, design.md de
/// asistente-administracion-de-uso).
/// </summary>
public sealed record EstadoDelCupoDelActor(int Restante, bool Bloqueado, string? Motivo, DateTimeOffset? VuelveA)
{
    public const string MotivoPresupuestoPropio = "presupuesto_propio";
    public const string MotivoTopeOrganizacional = "tope_organizacional";
    public const string MotivoMantenimiento = "mantenimiento";
    public const string MotivoSinAcceso = "sin_acceso";
}
