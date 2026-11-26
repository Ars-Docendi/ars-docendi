using Modules.Asistente.Application;
using Modules.Aulas.Contracts.Queries;

namespace ArsDocendi.Host.Asistente;

/// <summary>
/// Compone el puerto de vínculos del asistente con Aulas: el identificador de una solicitud de
/// reserva enlaza a su detalle sólo si Aulas dice que el actor puede abrirlo.
/// </summary>
/// <remarks>
/// El criterio (<c>aulas.aprobar</c>, o solicitud propia con <c>aulas.solicitar</c>) vive en
/// Aulas y es el del endpoint de detalle; este adaptador no lo reimplementa.
/// </remarks>
public sealed class VinculosDeAulas(IAulasQueries aulas) : IResolutorDeVinculos
{
    /// <summary>Nombre del recurso en el contrato con el cliente.</summary>
    public const string TipoDeLaSolicitud = "solicitud-reserva-aula";

    public async Task<IReadOnlyDictionary<string, DestinoDelVinculo>> ResolverAsync(
        IReadOnlyCollection<string> candidatos, CancellationToken ct)
    {
        var ids = VinculosDePersonas.IdentificadoresDe(candidatos);

        if (ids.Count == 0)
        {
            return new Dictionary<string, DestinoDelVinculo>(StringComparer.Ordinal);
        }

        var abribles = await aulas.SolicitudesAbriblesAsync(ids.Values.ToList(), ct);

        return ids
            .Where(par => abribles.Contains(par.Value))
            .ToDictionary(
                par => par.Key,
                par => new DestinoDelVinculo(TipoDeLaSolicitud, par.Value.ToString(), EsClave: true),
                StringComparer.Ordinal);
    }
}
