using Modules.Asistente.Application;
using Modules.Asistente.Contracts;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// <see cref="IConsultaDeMantenimiento"/> sobre <see cref="IDisponibilidadDelModulo"/>
/// (sistema-seccion-unificada, design.md D7, ARS-155).
/// </summary>
/// <remarks>
/// Envuelve el puerto interno y descarta <c>Razon</c> a propósito: quien sólo
/// tiene <c>sistema.estado.ver</c> no tiene por qué enterarse de la razón ni,
/// indirectamente, de quién la escribió — esos datos siguen detrás de
/// <c>asistente.consultar</c> (design.md D7, alternativa (a) rechazada).
/// </remarks>
internal sealed class ConsultaDeMantenimiento(IDisponibilidadDelModulo disponibilidad)
    : IConsultaDeMantenimiento
{
    public async Task<EstadoDeMantenimientoPublico> ConsultarAsync(CancellationToken ct)
    {
        var estado = await disponibilidad.ConsultarAsync(ct);
        return new EstadoDeMantenimientoPublico(estado.Activo);
    }
}
