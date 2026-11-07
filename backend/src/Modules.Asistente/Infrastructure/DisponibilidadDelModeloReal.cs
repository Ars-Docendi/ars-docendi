using Modules.Asistente.Application;

namespace Modules.Asistente.Infrastructure;

/// <summary>Compone las dos razones por las que un turno puede quedarse sin modelo.</summary>
internal sealed class DisponibilidadDelModeloReal(
    IAccesoAlAsistente acceso,
    ICuotaDelActor cuota,
    IPresupuestoOrganizacional presupuestoOrganizacional,
    IDisponibilidadDelModulo disponibilidadDelModulo,
    IBreakerDelTurno breaker)
    : IDisponibilidadDelModelo
{
    public async Task<MotivoSinModelo> ConsultarAsync(Guid actor, CancellationToken ct)
    {
        // Antes que la cuota (asistente-acceso-granular, design.md D4): a un
        // actor sin acceso no hay cupo que contarle.
        if (!await acceso.TieneAccesoAsync(actor, ct))
        {
            return MotivoSinModelo.SinAcceso;
        }

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

        // El mantenimiento SIEMPRE se reporta acá, sin excepción para nadie (design.md D8): el
        // bypass del admin es decisión del LLAMADOR (CapaConversacional, que conoce los permisos
        // del actor), no de este puerto de almacenamiento — que no sabe nada de permisos.
        var mantenimiento = await disponibilidadDelModulo.ConsultarAsync(ct);
        if (mantenimiento.Activo)
        {
            return MotivoSinModelo.Mantenimiento;
        }

        // El breaker del adaptador que ESTE turno resolvió (asistente-modelo-en-
        // caliente, D8): uno abierto para otro modelo no cuenta.
        return await breaker.EstadoAsync(ct) == EstadoDelBreaker.Abierto
            ? MotivoSinModelo.ProveedorCaido
            : MotivoSinModelo.Ninguno;
    }

    public Task<DateTimeOffset?> CupoVuelveAAsync(Guid actor, CancellationToken ct) =>
        cuota.CupoVuelveAAsync(actor, ct);
}
