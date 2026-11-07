using Modules.Asistente.Application;

namespace ArsDocendi.Host.Asistente;

/// <summary>
/// Le pregunta a cada autoridad por los candidatos y junta lo que contestan. Un candidato que
/// dos autoridades reconocen se queda con la primera; no pasa con ids ni con números de trámite.
/// </summary>
/// <remarks>
/// Si una autoridad falla, falla el conjunto: el cierre del turno responde sin vínculos antes
/// que con una parte de ellos.
/// </remarks>
public sealed class VinculosCompuestos(IReadOnlyList<IResolutorDeVinculos> autoridades)
    : IResolutorDeVinculos
{
    public async Task<IReadOnlyDictionary<string, DestinoDelVinculo>> ResolverAsync(
        IReadOnlyCollection<string> candidatos, CancellationToken ct)
    {
        var todos = new Dictionary<string, DestinoDelVinculo>(StringComparer.Ordinal);

        foreach (var autoridad in autoridades)
        {
            foreach (var (candidato, destino) in await autoridad.ResolverAsync(candidatos, ct))
            {
                todos.TryAdd(candidato, destino);
            }
        }

        return todos;
    }
}
