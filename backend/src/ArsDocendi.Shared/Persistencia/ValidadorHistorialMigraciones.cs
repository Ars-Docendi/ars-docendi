namespace ArsDocendi.Shared.Persistencia;

/// <summary>Una historia válida es un prefijo completo del inventario disponible.</summary>
public static class ValidadorHistorialMigraciones
{
    public static void Validar(IReadOnlyList<string> disponibles, IReadOnlyList<string> aplicadas)
    {
        if (disponibles.Distinct(StringComparer.Ordinal).Count() != disponibles.Count
            || aplicadas.Count > disponibles.Count
            || !aplicadas.SequenceEqual(disponibles.Take(aplicadas.Count), StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Historial de migraciones incompatible: contiene IDs desconocidos, repetidos o discontinuos. No se modificó la base.");
    }
}
