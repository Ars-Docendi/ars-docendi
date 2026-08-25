namespace Modules.Asistente.Application;

/// <summary>Por qué un turno no puede llamar al modelo.</summary>
public enum MotivoSinModelo
{
    /// <summary>Se puede llamar.</summary>
    Ninguno,

    /// <summary>El actor agotó su cupo de la ventana.</summary>
    CuotaAgotada,

    /// <summary>El proveedor viene fallando y el breaker está abierto.</summary>
    ProveedorCaido,
}

/// <summary>
/// El veredicto sobre si este turno puede usar el modelo (RF-19).
/// </summary>
/// <remarks>
/// Se consulta <b>una sola vez</b>, antes de empezar el turno, y la capa
/// conversacional lo respeta paso a paso en vez de tratarlo como una excepción que
/// corta todo.
///
/// La diferencia importa: cinco de los ocho pasos del pipeline no necesitan
/// proveedor. Si la falta de modelo abortara el turno, el saludo dejaría de
/// resolverse a cero tokens justo cuando es lo único que queda en pie, y una
/// pregunta ambigua dejaría de devolver su menú aunque el menú salga de una
/// consulta a la base.
/// </remarks>
public interface IDisponibilidadDelModelo
{
    /// <summary>Resuelve el veredicto para un actor.</summary>
    MotivoSinModelo Consultar(Guid actor);

    /// <summary>Cuándo vuelve a haber cupo, cuando el motivo es la cuota.</summary>
    DateTimeOffset? CupoVuelveA(Guid actor);
}
