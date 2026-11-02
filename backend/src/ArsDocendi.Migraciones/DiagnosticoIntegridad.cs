using ArsDocendi.Shared.Persistencia;

namespace ArsDocendi.Migraciones;

/// <summary>Compone lecturas locales; ausencia o fallo de un consumidor nunca equivale a cero hallazgos.</summary>
public static class DiagnosticoIntegridad
{
    private static readonly string[] ContextosRequeridos = ["identity", "storage", "designaciones", "portal", "tareas"];

    public static async Task<IReadOnlyList<HallazgoIntegridad>> LeerAsync(
        IReadOnlyList<IDiagnosticoIntegridadModulo> lectores, CancellationToken ct)
    {
        var contextos = lectores.Select(lector => lector.Contexto).ToArray();
        if (contextos.Distinct(StringComparer.OrdinalIgnoreCase).Count() != contextos.Length
            || !ContextosRequeridos.All(requerido => contextos.Contains(requerido, StringComparer.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Composición de diagnóstico incompleta o duplicada.");

        var registros = new List<RegistroIntegridad>();
        foreach (var lector in lectores) registros.AddRange(await lector.LeerAsync(ct));
        var destinos = registros.Where(r => r.Clase == "destino")
            .Select(r => (r.Tipo, r.Id)).ToHashSet();
        var incompatibles = registros.Where(r => r.Clase == "hallazgo"
            || (r.Clase == "referencia" && r.DestinoId.HasValue
                && !destinos.Contains((r.TipoDestino!, r.DestinoId.Value))));
        return incompatibles.GroupBy(r => r.Clase == "referencia" ? r.Tipo + "-huerfana" : r.Tipo)
            .OrderBy(grupo => grupo.Key, StringComparer.Ordinal)
            .Select(grupo => new HallazgoIntegridad(grupo.Key, grupo.LongCount(),
                grupo.Select(r => r.Id).Distinct().Order().Take(10).ToArray())).ToArray();
    }
}
