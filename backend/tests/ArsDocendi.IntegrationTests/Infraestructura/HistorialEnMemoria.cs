using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Infraestructura;

/// <summary>
/// Guarda lo que se le mandó a registrar al historial, sin tocar la base.
/// </summary>
/// <remarks>
/// Espejo de <see cref="RegistroEnMemoria"/> para <see cref="IRegistroDeHistorial"/>:
/// default de <c>BancoDelAsistente.Armar</c> para los tests que no ejercitan
/// el historial y no necesitan pagar una escritura real por turno. Reproduce
/// el minteo/actualización de <see cref="HiloConversacional.HiloHistorico"/>
/// que el escritor real hace, así que un test que sí mire esa propiedad ve el
/// mismo comportamiento con o sin base.
/// </remarks>
public sealed class HistorialEnMemoria : IRegistroDeHistorial
{
    private readonly List<TurnoParaHistorial> _turnos = [];

    /// <summary>Todo lo que se mandó a registrar, en orden.</summary>
    public IReadOnlyList<TurnoParaHistorial> Turnos => _turnos;

    public Task RegistrarTurnoAsync(
        HiloConversacional conversacion, TurnoParaHistorial turno, CancellationToken ct)
    {
        conversacion.HiloHistorico ??= Guid.NewGuid();
        _turnos.Add(turno);
        return Task.CompletedTask;
    }
}
