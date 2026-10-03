namespace Modules.Asistente.Application;

/// <summary>
/// Tope de gasto mensual estimado de toda la organización
/// (asistente-presupuesto-persistente, design.md D3/D6 de
/// asistente-administracion-de-uso).
/// </summary>
/// <remarks>
/// Distinto de <see cref="ICuotaDelActor"/> en la unidad (USD estimados, no
/// turnos) y en el alcance (uno solo para todo el sistema, no por actor) —
/// design.md D3: el cupo por actor responde "¿esta persona usa el asistente
/// con equidad respecto de sus pares?"; el tope organizacional responde
/// "¿estamos a punto de recibir una factura inesperada?".
/// </remarks>
public interface IPresupuestoOrganizacional
{
    /// <summary>Si el gasto estimado del mes en curso todavía está por debajo del tope.</summary>
    Task<bool> HayPresupuestoAsync(CancellationToken ct);

    /// <summary>
    /// Acumula el costo estimado de un turno contra el mes en curso.
    /// </summary>
    /// <remarks>
    /// Se llama exactamente una vez por turno que invocó al modelo (mismo
    /// guard que <see cref="ICuotaDelActor.AnotarAsync"/>, design.md D4), con
    /// el precio vigente EN ESE MOMENTO — no con el histórico que el panel de
    /// uso usaría para re-costear la fila después (design.md D6: las dos
    /// cosas calculan el mismo número, en momentos distintos, para
    /// propósitos distintos).
    /// </remarks>
    Task AcumularAsync(
        string? proveedor,
        DateTimeOffset ocurridoEn,
        int tokensDeEntrada,
        int tokensDeSalida,
        int? tokensDeCache,
        CancellationToken ct);
}
