using Modules.Asistente.Application;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Compone las dos razones por las que un turno puede quedarse sin modelo.
/// </summary>
/// <remarks>
/// El orden importa poco para el resultado y mucho para el mensaje: la cuota se
/// mira primero porque es lo único que el usuario puede entender y esperar. «Se cayó
/// el proveedor» no le dice a nadie qué hacer; «alcanzaste tu límite, volvés a tener
/// consultas a las 15:40» sí.
/// </remarks>
internal sealed class DisponibilidadDelModeloReal(
    ICuotaDelActor cuota,
    IPresupuestoOrganizacional presupuestoOrganizacional,
    IDisponibilidadDelModulo disponibilidadDelModulo,
    BreakerDelProveedor breaker)
    : IDisponibilidadDelModelo
{
    public async Task<MotivoSinModelo> ConsultarAsync(Guid actor, CancellationToken ct)
    {
        if (!await cuota.HayCupoAsync(actor, ct))
        {
            return MotivoSinModelo.CuotaAgotada;
        }

        // Mismo choque que la cuota (design.md D3/D4 de
        // asistente-administracion-de-uso): un tope organizacional agotado
        // bloquea a TODOS los actores, incluso a uno que todavía tiene su
        // propio cupo disponible.
        if (!await presupuestoOrganizacional.HayPresupuestoAsync(ct))
        {
            return MotivoSinModelo.TopeOrganizacionalAgotado;
        }

        // El mantenimiento SIEMPRE se reporta acá, sin excepción para nadie
        // (design.md D8): el bypass del admin es decisión del LLAMADOR
        // (CapaConversacional, que conoce los permisos del actor), no de
        // este puerto de almacenamiento — que no sabe nada de permisos.
        var mantenimiento = await disponibilidadDelModulo.ConsultarAsync(ct);
        if (mantenimiento.Activo)
        {
            return MotivoSinModelo.Mantenimiento;
        }

        return breaker.Estado == EstadoDelBreaker.Abierto
            ? MotivoSinModelo.ProveedorCaido
            : MotivoSinModelo.Ninguno;
    }

    public Task<DateTimeOffset?> CupoVuelveAAsync(Guid actor, CancellationToken ct) =>
        cuota.CupoVuelveAAsync(actor, ct);
}
