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
    ICuotaDelActor cuota, BreakerDelProveedor breaker) : IDisponibilidadDelModelo
{
    public MotivoSinModelo Consultar(Guid actor)
    {
        if (!cuota.HayCupo(actor))
        {
            return MotivoSinModelo.CuotaAgotada;
        }

        return breaker.Estado == EstadoDelBreaker.Abierto
            ? MotivoSinModelo.ProveedorCaido
            : MotivoSinModelo.Ninguno;
    }

    public DateTimeOffset? CupoVuelveA(Guid actor) => cuota.CupoVuelveA(actor);
}
