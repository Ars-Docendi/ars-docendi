using ArsDocendi.Host.Administracion;
using ArsDocendi.Shared.Aplicacion;
using Modules.Asistente.Application;

namespace ArsDocendi.Host.Asistente;

/// <summary>
/// Compone el puerto de vínculos del asistente con Personas: el nombre de un docente enlaza a
/// <c>/personas/:id</c> sólo si el actor puede abrir esa persona.
/// </summary>
/// <remarks>
/// Reproduce el criterio de <c>PersonasController.Obtener</c> con las mismas piezas:
/// <see cref="AlcancePersonas"/> (<c>personas.ver</c> = todas; <c>personas.ver_ambito</c> = las
/// designadas en las materias de su jefatura; sin ninguno, nada) y el ámbito del servicio.
/// </remarks>
public sealed class VinculosDePersonas(
    AlcancePersonas alcance,
    ServicioAdministracionPersonas personas,
    IHttpContextAccessor contexto) : IResolutorDeVinculos
{
    /// <summary>Nombre del recurso en el contrato con el cliente.</summary>
    public const string TipoDeLaPersona = "persona";

    private static readonly IReadOnlyDictionary<string, DestinoDelVinculo> Nada =
        new Dictionary<string, DestinoDelVinculo>(StringComparer.Ordinal);

    public async Task<IReadOnlyDictionary<string, DestinoDelVinculo>> ResolverAsync(
        IReadOnlyCollection<string> candidatos, CancellationToken ct)
    {
        var usuario = contexto.HttpContext?.User;
        var ids = IdentificadoresDe(candidatos);

        if (usuario is null || ids.Count == 0)
        {
            return Nada;
        }

        IReadOnlySet<Guid>? materias;

        try
        {
            materias = await alcance.MateriasVisiblesAsync(usuario, ct);
        }
        catch (ExcepcionAplicacion)
        {
            // Sin permiso de personas: ni se pregunta.
            return Nada;
        }

        var abribles = await personas.PersonasEnAmbitoAsync(ids.Values.ToList(), materias, ct);

        return ids
            .Where(par => abribles.Contains(par.Value))
            .ToDictionary(
                par => par.Key,
                par => new DestinoDelVinculo(TipoDeLaPersona, par.Value.ToString(), EsClave: true),
                StringComparer.Ordinal);
    }

    /// <summary>Los candidatos con forma de uuid, por su texto original.</summary>
    internal static Dictionary<string, Guid> IdentificadoresDe(IEnumerable<string> candidatos) =>
        candidatos
            .Select(c => (Texto: c, Valido: Guid.TryParse(c, out var id), Id: id))
            .Where(c => c.Valido)
            .GroupBy(c => c.Texto, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);
}
