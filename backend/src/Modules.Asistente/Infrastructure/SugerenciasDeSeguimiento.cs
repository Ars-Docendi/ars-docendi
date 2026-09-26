using Modules.Asistente.Application;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Picks follow-up suggestions for an answered turn: category match
/// (<see cref="Sugerencias.ParaCategoria"/>) then the same executability check
/// <see cref="CatalogoDeCapacidades"/> already applies to its own examples.
/// </summary>
/// <remarks>
/// Runs <c>EXPLAIN</c> only on the category-filtered, already-small candidate
/// set — never on the full catalog — the same bound
/// <c>CatalogoDeCapacidades.ElegirEjemplosAsync</c> relies on.
/// </remarks>
internal sealed class SugerenciasDeSeguimiento(
    AperturaDeLectura apertura, ISelectorDeEjemplos ejemplos) : ISugerenciasDeSeguimiento
{
    public async Task<IReadOnlyList<string>> ObtenerAsync(
        Guid actor,
        string categoria,
        string? sqlEjecutado,
        bool conDatosPersonales,
        CancellationToken ct)
    {
        var candidatos = Sugerencias.ParaCategoria(categoria, sqlEjecutado, ejemplos.Catalogo);

        if (candidatos.Count == 0)
        {
            // No wasted connection when there is nothing of this category to
            // even try — the common case for the categories with few catalog
            // entries.
            return [];
        }

        await using var conexion = await apertura.AbrirAsync(conDatosPersonales, ct);

        var elegidas = new List<string>();

        foreach (var candidato in candidatos)
        {
            if (elegidas.Count == Sugerencias.Cuantas)
            {
                break;
            }

            if (await CatalogoDeCapacidades.EjecutableAsync(conexion, actor, candidato.Sql, ct))
            {
                elegidas.Add(candidato.Pregunta);
            }
        }

        return elegidas;
    }
}
