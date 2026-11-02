namespace ArsDocendi.Storage.Infrastructure;

public interface IAntivirusArchivos
{
    Task<ResultadoAntivirus> AnalizarAsync(Stream contenido, CancellationToken ct);
}

public sealed record ResultadoAntivirus(bool Limpio, bool Disponible, string? Motivo);
