namespace ArsDocendi.Shared.Persistencia;

/// <summary>Contrato puro de operaciones one-shot; no expone DbContexts.</summary>
public interface IMigradorModulo
{
    string Contexto { get; }
    void ValidarRecursos();
    Task<EstadoMigracionesModulo> ConsultarAsync(CancellationToken ct);
    Task<string> GenerarScriptAsync(CancellationToken ct);
    Task MigrarAsync(CancellationToken ct);
}

public sealed record EstadoMigracionesModulo(
    string Contexto,
    IReadOnlyList<string> Disponibles,
    IReadOnlyList<string> Aplicadas,
    IReadOnlyList<string> Pendientes);
