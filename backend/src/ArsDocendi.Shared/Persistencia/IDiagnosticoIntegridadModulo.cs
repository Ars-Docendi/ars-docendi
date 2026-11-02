namespace ArsDocendi.Shared.Persistencia;

/// <summary>Lectura local de integridad; no expone conexiones ni habilita saneamiento.</summary>
public interface IDiagnosticoIntegridadModulo
{
    string Contexto { get; }
    Task<IReadOnlyList<RegistroIntegridad>> LeerAsync(CancellationToken ct);
}

/// <summary>Solo categorías constantes e identificadores técnicos; nunca valores de negocio.</summary>
public sealed record RegistroIntegridad(
    string Clase,
    string Tipo,
    Guid Id,
    string? TipoDestino,
    Guid? DestinoId);

public sealed record HallazgoIntegridad(string Categoria, long Cantidad, IReadOnlyList<Guid> Ejemplos);
