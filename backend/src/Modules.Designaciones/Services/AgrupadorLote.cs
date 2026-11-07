using ArsDocendi.Shared.Identity;

namespace Modules.Designaciones.Services;

/// <summary>
/// Agrupa las filas del lote por docente: el cargo y la dedicación son únicos por docente, así
/// que todas sus materias van en un solo registro, con los códigos unidos por guion y las horas
/// sumadas. Si un docente tuviera combinaciones distintas (por ejemplo, un cambio en una sola
/// materia), cada combinación queda en su propia fila: nunca se mezclan valores distintos.
/// El orden de entrada se conserva (primera aparición de cada grupo).
/// </summary>
internal static class AgrupadorLote
{
    public static FilaPropuestaLote[] Propuesta(
        IEnumerable<FilaPropuestaLote> filas, IReadOnlyDictionary<Guid, Materia> materias) =>
        filas
            .GroupBy(f => (f.PersonaId, f.CargoAnterior, f.CargoPropuesto, f.Departamento))
            .Select(g =>
            {
                var primera = g.First();
                return primera with
                {
                    Materia = Codigos(g.Select(f => f.MateriaId), materias),
                    Horas = Suma(g.Select(f => f.Horas)),
                    Observacion = Unir(g.Select(f => f.Observacion), " / "),
                    NumeroPedido = Unir(g.Select(f => f.NumeroPedido), ", ") ?? string.Empty,
                };
            })
            .ToArray();

    public static FilaAltaLote[] Altas(
        IEnumerable<FilaAltaLote> filas, IReadOnlyDictionary<Guid, Materia> materias) =>
        filas
            .GroupBy(f => (f.PersonaId, f.CargoDedicacion))
            .Select(g => g.First() with
            {
                Materia = Codigos(g.Select(f => f.MateriaId), materias),
                NumeroPedido = Unir(g.Select(f => f.NumeroPedido), ", ") ?? string.Empty,
            })
            .ToArray();

    public static FilaBajaLote[] Bajas(
        IEnumerable<FilaBajaLote> filas, IReadOnlyDictionary<Guid, Materia> materias) =>
        filas
            .GroupBy(f => (f.PersonaId, f.CargoAnterior))
            .Select(g => g.First() with
            {
                Materia = Codigos(g.Select(f => f.MateriaId), materias),
                Horas = Suma(g.Select(f => f.Horas)),
                Motivo = Unir(g.Select(f => f.Motivo), " / "),
                NumeroPedido = Unir(g.Select(f => f.NumeroPedido), ", ") ?? string.Empty,
            })
            .ToArray();

    private static string? Codigos(IEnumerable<Guid> materiaIds, IReadOnlyDictionary<Guid, Materia> materias) =>
        Unir(materiaIds.Select(id => materias.TryGetValue(id, out var m) ? m.Codigo : null), "-");

    private static int? Suma(IEnumerable<int?> horas)
    {
        var conValor = horas.Where(h => h is not null).ToArray();
        return conValor.Length == 0 ? null : conValor.Sum();
    }

    private static string? Unir(IEnumerable<string?> valores, string separador)
    {
        var distintos = valores
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return distintos.Length == 0 ? null : string.Join(separador, distintos);
    }
}
