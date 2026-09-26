namespace Modules.Asistente.Application;

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
}
