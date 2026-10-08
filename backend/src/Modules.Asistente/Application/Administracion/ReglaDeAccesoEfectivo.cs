namespace Modules.Asistente.Application;

/// <summary>
/// La regla que decide si un actor tiene ACCESO operativo al asistente
/// (asistente-acceso-granular, design.md D2).
/// </summary>
/// <remarks>
/// A diferencia de <see cref="ReglaDeCupoEfectivo"/>, que es un espejo de la
/// regla que aplica <c>CuotaPersistente</c>, esta clase es la ÚNICA
/// implementación: la llaman tanto <c>AccesoPersistente</c> (el bloqueo real
/// del turno) como el panel de administración (lo que se muestra), así que
/// no hay dos reglas que puedan divergir.
/// <para>
/// El acceso es una capa ADICIONAL al permiso <c>asistente.consultar</c>
/// (D1): sólo puede restringir. Por eso un actor sin ningún rol de sistema
/// tiene acceso —no hay rol del que heredar un "no", y la puerta real sigue
/// siendo el permiso— y un rol ausente de <paramref name="accesoPorRol"/>
/// cuenta como habilitado.
/// </para>
/// </remarks>
internal static class ReglaDeAccesoEfectivo
{
    public const string OrigenRol = "rol";
    public const string OrigenPropio = "propio";

    /// <param name="revocado">Si el actor tiene una revocación propia vigente.</param>
    /// <param name="codigosDeRol">Los roles de sistema vigentes del actor.</param>
    /// <param name="accesoPorRol">
    /// El acceso de CADA rol de sistema, tal como está persistido en
    /// <c>presupuesto_rol.acceso_habilitado</c>.
    /// </param>
    /// <returns>
    /// Si tiene acceso y de dónde sale ese veredicto: <see cref="OrigenPropio"/>
    /// cuando lo decide la revocación del usuario, <see cref="OrigenRol"/> en
    /// cualquier otro caso.
    /// </returns>
    public static (bool Acceso, string Origen) Resolver(
        bool revocado,
        IReadOnlyList<string> codigosDeRol,
        IReadOnlyDictionary<string, bool> accesoPorRol)
    {
        if (revocado)
        {
            return (false, OrigenPropio);
        }

        // «Alguno» y no «todos» (D2): un rol extra no le quita el acceso a
        // quien otro rol se lo da — el acceso se quita explícitamente, por
        // usuario, con la revocación de arriba.
        var alguno = codigosDeRol.Count == 0
            || codigosDeRol.Any(rol => accesoPorRol.GetValueOrDefault(rol, true));

        return (alguno, OrigenRol);
    }
}
