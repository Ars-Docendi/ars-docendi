using Modules.Asistente.Application;
using Modules.Asistente.Contracts;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// <see cref="IConsultaDeMantenimiento"/> sobre <see cref="IDisponibilidadDelModulo"/>
/// (sistema-seccion-unificada, design.md D7, ARS-155).
/// </summary>
internal sealed class ConsultaDeMantenimiento(IDisponibilidadDelModulo disponibilidad)
    : IConsultaDeMantenimiento
{
    public async Task<EstadoDeMantenimientoPublico> ConsultarAsync(CancellationToken ct)
    {
        var estado = await disponibilidad.ConsultarAsync(ct);
        return new EstadoDeMantenimientoPublico(estado.Activo);
    }
}
