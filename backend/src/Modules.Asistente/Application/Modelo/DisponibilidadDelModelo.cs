namespace Modules.Asistente.Application;

/// <summary>Por qué un turno no puede llamar al modelo.</summary>
/// <remarks>
/// Las tres últimas se agregaron con asistente-administracion-de-uso
/// (design.md D1): todas siguen resolviendo como
/// <see cref="EstadoDelTurno.ServicioDegradado"/> en el contrato HTTP — el
/// "carril es un servicio... antes de tener los cuatro estados" es un
/// invariante deliberado, y cada motivo nuevo es, semánticamente, "no se
/// puede conseguir una respuesta del modelo ahora", que es exactamente lo que
/// ese estado ya significa para el cliente.
/// </remarks>
public enum MotivoSinModelo
{
    /// <summary>Se puede llamar.</summary>
    Ninguno,

    /// <summary>El actor agotó su cupo diario.</summary>
    CuotaAgotada,

    /// <summary>El proveedor viene fallando y el breaker está abierto.</summary>
    ProveedorCaido,

    /// <summary>El gasto mensual estimado de la organización alcanzó su tope.</summary>
    TopeOrganizacionalAgotado,

    /// <summary>El actor ya tiene un turno en curso.</summary>
    TurnoConcurrente,

    /// <summary>El módulo está en modo mantenimiento.</summary>
    Mantenimiento,
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
    /// <remarks>
    /// Asíncrono desde asistente-administracion-de-uso: el cupo, el tope
    /// organizacional y el modo mantenimiento viven en Postgres, a diferencia
    /// del cupo en memoria que este puerto consultaba antes.
    /// </remarks>
    Task<MotivoSinModelo> ConsultarAsync(Guid actor, CancellationToken ct);

    /// <summary>Cuándo vuelve a haber cupo, cuando el motivo es la cuota.</summary>
    Task<DateTimeOffset?> CupoVuelveAAsync(Guid actor, CancellationToken ct);
}
