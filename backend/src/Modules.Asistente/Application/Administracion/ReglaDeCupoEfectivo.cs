namespace Modules.Asistente.Application;

/// <summary>
/// La regla que decide el cupo diario EFECTIVO de un actor a partir de su
/// override puntual (si existe) y el cupo default de sus roles de sistema
/// (tarea «rol y cupo efectivo por usuario» de sistema-seccion-unificada).
/// </summary>
/// <remarks>
/// Es un espejo, a propósito, de la regla que ya aplica
/// <c>CuotaPersistente.CupoEfectivoAsync</c> (asistente-presupuesto-persistente,
/// design.md D2/D4 de asistente-administracion-de-uso) — ESTA clase no la
/// reemplaza ni la llama, porque <c>CuotaPersistente</c> resuelve sus propios
/// ingredientes contra Postgres en el camino de aplicación del cupo (el que
/// bloquea un turno), y ese camino no debía ganar una dependencia nueva sólo
/// para que el panel de administración pueda MOSTRAR el mismo número. Que el
/// valor mostrado coincida con el aplicado depende, en cambio, de que las dos
/// implementaciones sigan exactamente la misma regla documentada acá y allá —
/// <c>ReglaDeCupoEfectivoTests</c> (unitarios) y una prueba de integración que
/// compara este resultado contra <c>ICuotaDelActor.CupoRestanteAsync</c> real
/// para un actor sin turnos hoy existen exactamente para detectar esa deriva.
/// <para>
/// La regla, en las palabras de <c>CuotaPersistente</c>: un override vigente
/// en <c>presupuesto_usuario</c> GANA siempre, más chico o más grande que el
/// default de rol. Si no hay override, se toma el MÍNIMO cupo entre los roles
/// cuyo default está ACTIVADO (mayor que cero), ignorando los roles en cero —
/// un cero significa "este rol no impone tope", no "el tope más bajo
/// posible". Un actor sin ningún rol de sistema no tiene NADA que atribuirle
/// al rol —a diferencia de la aplicación real del cupo, que en ese caso
/// devuelve 0 (sin tope) porque necesita SIEMPRE un número para decidir si
/// bloquea, el panel de administración prefiere decir "no se puede resolver"
/// (<c>Origen: null</c>) antes que mostrar «sin tope · del rol» sugiriendo un
/// rol que no existe.
/// </para>
/// </remarks>
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
