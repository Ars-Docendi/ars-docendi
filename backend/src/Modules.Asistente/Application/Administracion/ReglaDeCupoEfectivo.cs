namespace Modules.Asistente.Application;

/// <summary>
/// La regla que decide el cupo diario EFECTIVO de un actor a partir de su override puntual (si
/// existe) y el cupo default de sus roles de sistema (tarea «rol y cupo efectivo por usuario» de
/// sistema-seccion-unificada).
/// </summary>
internal static class ReglaDeCupoEfectivo
{
    /// <param name="overrideDeUsuario">
    /// El cupo persistido en <c>presupuesto_usuario</c> para este actor, o
    /// <c>null</c> si no tiene ninguno vigente.
    /// </param>
    /// <param name="codigosDeRol">Los roles de sistema vigentes del actor.</param>
    /// <param name="cuposPorRol">
    /// El cupo diario default de CADA rol de sistema, tal como está persistido
    /// en <c>presupuesto_rol</c> (no sólo los roles de este actor).
    /// </param>
    /// <returns>
    /// El cupo efectivo y su origen (<c>"override"</c> o <c>"rol"</c>), o
    /// <c>(null, null)</c> cuando el actor no tiene override ni ningún rol de
    /// sistema del que heredar un default.
    /// </returns>
    public static (int? Cupo, string? Origen) Resolver(
        int? overrideDeUsuario,
        IReadOnlyList<string> codigosDeRol,
        IReadOnlyDictionary<string, int> cuposPorRol)
    {
        if (overrideDeUsuario is { } cupoDeOverride)
        {
            return (cupoDeOverride, "override");
        }

        if (codigosDeRol.Count == 0)
        {
            return (null, null);
        }

        var activados = codigosDeRol
            .Select(rol => cuposPorRol.GetValueOrDefault(rol, 0))
            .Where(cupo => cupo > 0)
            .ToList();

        return (activados.Count == 0 ? 0 : activados.Min(), "rol");
    }
}
