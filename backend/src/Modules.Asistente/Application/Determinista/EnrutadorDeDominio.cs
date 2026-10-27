using Microsoft.Extensions.Logging;

namespace Modules.Asistente.Application;

/// <summary>Decide, para cada turno, si la pregunta corresponde al carril determinista.</summary>
public sealed class EnrutadorDeDominio(
    ResolutorDeIntenciones resolutor, ILogger<EnrutadorDeDominio> log)
{
    /// <summary>La intención que cubriría la pregunta, o nulo si sigue al carril SQL.</summary>
    public async Task<IntencionResuelta?> DecidirAsync(string pregunta, CancellationToken ct)
    {
        var resuelta = await resolutor.ResolverAsync(pregunta, ct);

        if (resuelta is null)
        {
            return null;
        }

        log.LogInformation(
            "El turno corresponde a la intención {Intencion}, con destino {Destino}.",
            resuelta.Intencion.Nombre,
            resuelta.Destino);

        return resuelta;
    }
}
