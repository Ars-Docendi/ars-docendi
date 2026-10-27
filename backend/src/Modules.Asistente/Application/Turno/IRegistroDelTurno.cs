namespace Modules.Asistente.Application;

/// <summary>Por qué camino se resolvió el turno.</summary>
public enum CarrilDelTurno
{
    /// <summary>Saludo, agradecimiento o meta-pregunta: cero tokens.</summary>
    SinDatos,

    /// <summary>Terminó pidiendo una aclaración.</summary>
    Aclaracion,

    /// <summary>Generó y ejecutó una consulta.</summary>
    Sql,

    /// <summary>No había modelo disponible.</summary>
    Degradado,

    /// <summary>El turno se cayó por una excepción no prevista, en cualquier paso.</summary>
    Fallo,
}

/// <summary>Todo lo que un turno deja para registrar, antes de separarlo en dos.</summary>
public sealed record TurnoParaRegistrar(
    Guid Actor,
    Guid AnaliticoId,
    DateTimeOffset Cuando,
    CarrilDelTurno Carril,
    EstadoDelTurno Estado,
    int LlamadasAlModelo,
    int TokensDeEntrada,
    int TokensDeSalida,
    int TokensDeCache,
    int LatenciaMs,
    bool HuboReintento,
    bool Truncado,
    string Pregunta,
    string Categoria,
    string Proveedor,
    MotivoDeRechazo? MotivoDeRechazo = null,
    CausaDelTurno? Causa = null,
    string? Modelo = null);

/// <summary>Escribe los dos registros desvinculados (RF-16).</summary>
public interface IRegistroDelTurno
{
    /// <summary>Registra un turno. No propaga errores.</summary>
    Task RegistrarAsync(TurnoParaRegistrar turno, CancellationToken ct);

    /// <summary>
    /// Flags the analytic record <paramref name="analiticoId"/> as replaced by the user (implicit
    /// dissatisfaction, design D4 of asistente-metricas-de-calidad).
    /// </summary>
    Task MarcarReemplazadoAsync(Guid analiticoId, CancellationToken ct);
}
