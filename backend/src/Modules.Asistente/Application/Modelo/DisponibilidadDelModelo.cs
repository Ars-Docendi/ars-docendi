namespace Modules.Asistente.Application;

/// <summary>Por qué un turno no puede llamar al modelo.</summary>
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

    /// <summary>
    /// El actor no tiene acceso operativo: se lo quitaron a él o a todos sus roles
    /// (asistente-acceso-granular).
    /// </summary>
    SinAcceso,
}

/// <summary>El veredicto sobre si este turno puede usar el modelo (RF-19).</summary>
public interface IDisponibilidadDelModelo
{
    /// <summary>Resuelve el veredicto para un actor.</summary>
    Task<MotivoSinModelo> ConsultarAsync(Guid actor, CancellationToken ct);

    /// <summary>Cuándo vuelve a haber cupo, cuando el motivo es la cuota.</summary>
    Task<DateTimeOffset?> CupoVuelveAAsync(Guid actor, CancellationToken ct);
}
