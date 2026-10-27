namespace Modules.Asistente.Application;

/// <summary>
/// Ejecuta la consulta generada contra una conexión de solo lectura, acotada al actor del turno.
/// </summary>
public interface IEjecutorDeConsulta
{
    /// <summary>
    /// Ejecuta la consulta en una transacción nueva de solo lectura, con el actor fijado
    /// transaction-local.
    /// </summary>
    Task<ResultadoDeConsulta> EjecutarAsync(
        string sql,
        Guid actor,
        bool conDatosPersonales,
        CancellationToken ct,
        IReadOnlyDictionary<string, Guid>? marcadores = null);
}

/// <summary>Lo que el carril necesita saber del actor antes de decidir nada.</summary>
public sealed record PerfilDelActor(
    bool EsGlobal,
    bool VeDatosPersonales,
    bool VeLaConsulta = false,
    string? CodigoDeRol = null,
    bool AlcanzaDesignaciones = false,
    bool VeTrayectoriaAjena = false,
    bool VeDesignaciones = false);

/// <summary>Resuelve el alcance y el acceso a datos personales del actor.</summary>
public interface IPerfilDelActor
{
    /// <summary>Resuelve el perfil del actor del turno.</summary>
    Task<PerfilDelActor> ObtenerAsync(Guid actor, CancellationToken ct);
}

/// <summary>
/// El identificador que se quiso usar como actor no corresponde a ningún usuario activo del
/// sistema.
/// </summary>
internal sealed class ActorNoResuelto(Guid actor, Exception? causa = null)
    : Exception(
        $"El identificador '{actor}' no corresponde a ningún usuario activo del sistema.",
        causa)
{
    /// <summary>El identificador que no resolvió.</summary>
    public Guid Actor { get; } = actor;
}
