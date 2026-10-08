namespace Modules.Asistente.Application;

/// <summary>Lo que decide la puerta antes de gastar una llamada en el plan.</summary>
internal enum DecisionDeLaPuerta
{
    /// <summary>La pregunta no es del plan: sigue por el carril SQL.</summary>
    NoAplica,

    /// <summary>Nombra la antigüedad sin decir cuál: se aclara sin llamar al modelo.</summary>
    AclararAntiguedad,

    /// <summary>Su vocabulario cae entero dentro del catálogo: se pide el plan.</summary>
    Candidata,
}

/// <summary>
/// La puerta léxica del plan compilado (D4 de <c>asistente-plan-compilado</c>).
/// </summary>
/// <remarks>
/// Determinista y sin acceso a la base: decide con la pregunta sola. Su trabajo
/// es no gastar una llamada en una pregunta que el catálogo no puede expresar, y
/// no dejar que el modelo elija en silencio entre dos definiciones.
/// </remarks>
internal static class PuertaDelPlan
{
    private static readonly string[] AyudanteConOrden =
        ["ayudante de primera", "ayudantes de primera", "ayudante de segunda", "ayudantes de segunda",
         "ayudante 1", "ayudantes 1", "ayudante 2", "ayudantes 2"];

    public static DecisionDeLaPuerta Evaluar(TextoDeLaPregunta texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        if (TieneVocabularioFueraDelCatalogo(texto) || !NombraLaPoblacion(texto))
        {
            return DecisionDeLaPuerta.NoAplica;
        }

        return NombraLaAntiguedadSinCalificar(texto)
            ? DecisionDeLaPuerta.AclararAntiguedad
            : DecisionDeLaPuerta.Candidata;
    }

    private static bool TieneVocabularioFueraDelCatalogo(TextoDeLaPregunta texto) =>
        CatalogoDelPlan.PrefijosFueraDelCatalogo.Any(texto.TienePalabraQueEmpiezaCon)
        || texto.ContieneAlguna(CatalogoDelPlan.FrasesFueraDelCatalogo)
        || texto.NombraUnAnio()
        // «Ayudantes» a secas son dos cargos, y el plan no tiene disyunción.
        || (texto.ContieneAlguna(["ayudante", "ayudantes"]) && !texto.ContieneAlguna(AyudanteConOrden));

    private static bool NombraLaPoblacion(TextoDeLaPregunta texto) =>
        texto.ContieneAlguna(CatalogoDelPlan.PalabrasDePoblacion)
        || CatalogoDelPlan.Cargos.Any(cargo => texto.ContieneAlguna(cargo.Sinonimos));

    private static bool NombraLaAntiguedadSinCalificar(TextoDeLaPregunta texto)
    {
        if (!texto.Contiene("antiguedad"))
        {
            return false;
        }

        var deDesignacion = texto.ContieneAlguna(CatalogoDelPlan.CalificadoresDeDesignacion);
        var declarada = texto.ContieneAlguna(CatalogoDelPlan.CalificadoresDeDeclarada);

        // Las dos a la vez tampoco deciden nada: el plan no puede saber a cuál
        // apunta la condición.
        return deDesignacion == declarada;
    }

    /// <summary>Las dos lecturas de la antigüedad, como preguntas que la puerta ya acepta.</summary>
    public static IReadOnlyList<OpcionDeAclaracion> OpcionesDeAntiguedad(string pregunta)
    {
        var sinCierre = pregunta.Trim().TrimEnd('?', '.', ' ');

        return
        [
            new OpcionDeAclaracion(
                "Desde su primera designación en la UNLaM",
                $"{sinCierre}, contando la antigüedad desde la primera designación?"),
            new OpcionDeAclaracion(
                "Según la experiencia declarada en el portal docente",
                $"{sinCierre}, contando la antigüedad según la experiencia declarada en el portal?"),
        ];
    }
}
