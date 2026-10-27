namespace Modules.Asistente.Application;

/// <summary>
/// Tope de gasto mensual estimado de toda la organización (asistente-presupuesto-persistente,
/// design.md D3/D6 de asistente-administracion-de-uso).
/// </summary>
public interface IPresupuestoOrganizacional
{
    /// <summary>Si el gasto estimado del mes en curso todavía está por debajo del tope.</summary>
    Task<bool> HayPresupuestoAsync(CancellationToken ct);

    /// <summary>Acumula el costo estimado de un turno contra el mes en curso.</summary>
    Task AcumularAsync(
        string? proveedor,
        DateTimeOffset ocurridoEn,
        int tokensDeEntrada,
        int tokensDeSalida,
        int? tokensDeCache,
        CancellationToken ct);
}
