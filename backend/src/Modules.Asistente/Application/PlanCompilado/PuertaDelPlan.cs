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

    private static readonly string[] PalabrasQuePiden =
        ["que", "cual", "cuales", "cuantos", "cuantas", "lista", "listado", "listame", "listar",
         "nombres", "nombrame", "mostrame", "decime", "dame"];

    private static readonly string[] PalabrasDePaso =
        ["es", "son", "el", "la", "los", "las", "de", "del", "porcentaje", "proporcion",
         "cantidad", "numero", "total", "parte", "fraccion"];

    public static DecisionDeLaPuerta Evaluar(TextoDeLaPregunta texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        if (TieneVocabularioFueraDelCatalogo(texto) || !NombraLaPoblacion(texto) || PideOtraEntidad(texto))
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

    // «¿Qué asignaturas se dictan en Ingeniería Industrial?» nombra una carrera y usa un
    // verbo de la población, pero pide materias. Sin esto el modelo arma un listado de
    // docentes, el validador lo rechaza y el turno se abstiene de algo que el carril SQL
    // responde. Se mira la palabra que sigue a cada interrogativo, salteando artículos y
    // «porcentaje de»: si es una entidad que no son personas, la pregunta no es del plan.
    private static bool PideOtraEntidad(TextoDeLaPregunta texto)
    {
        var palabras = texto.Palabras;

        for (var i = 0; i < palabras.Count; i++)
        {
            if (!PalabrasQuePiden.Contains(palabras[i], StringComparer.Ordinal))
            {
                continue;
            }

            var siguiente = i + 1;
            while (siguiente < palabras.Count
                   && PalabrasDePaso.Contains(palabras[siguiente], StringComparer.Ordinal))
            {
                siguiente++;
            }

            if (siguiente < palabras.Count
                && CatalogoDelPlan.PalabrasDeOtraEntidad.Contains(palabras[siguiente], StringComparer.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

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
