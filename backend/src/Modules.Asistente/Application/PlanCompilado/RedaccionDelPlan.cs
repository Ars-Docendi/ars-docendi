using System.Globalization;

namespace Modules.Asistente.Application;

/// <summary>
/// Las frases del plan: la interpretación que se muestra, la pregunta explícita de
/// una aclaración y la respuesta por plantilla (D9 de <c>asistente-plan-compilado</c>).
/// </summary>
/// <remarks>
/// <b>Ningún número sale del modelo.</b> La respuesta se arma con el resultado de la
/// consulta y con el plan; si el resultado no tiene la forma que la medida promete,
/// es un error del compilador y revienta en vez de redactar algo plausible.
///
/// Las frases que genera <see cref="PreguntaExplicita"/> están escritas para pasar la
/// puerta y el anclaje: una opción de aclaración que el propio plan rechazara al
/// volver a preguntarla dejaría al usuario en un círculo.
/// </remarks>
internal static class RedaccionDelPlan
{
    private const int NombresEnElTexto = 30;

    private static readonly CultureInfo Castellano = CultureInfo.GetCultureInfo("es-AR");

    /// <summary>La pregunta que corresponde exactamente al plan, en palabras que el plan acepta.</summary>
    public static string PreguntaExplicita(PlanValidado plan, IReadOnlyDictionary<int, EntidadResuelta>? entidades = null)
    {
        var filtros = Frases(plan.Filtros, entidades);

        return plan.Medida switch
        {
            CatalogoDelPlan.Porcentaje =>
                $"¿Qué porcentaje de los docentes{filtros} {Frases(plan.Condiciones, entidades).TrimStart()}?",
            CatalogoDelPlan.Listado => $"¿Quiénes son los docentes{filtros}?",
            _ => $"¿Cuántos docentes hay{filtros}?",
        };
    }

    public static string Responder(
        PlanValidado plan,
        ResultadoDeConsulta resultado,
        IReadOnlyDictionary<int, EntidadResuelta> entidades,
        bool alcanzaTodo)
    {
        var filtros = Frases(plan.Filtros, entidades);
        var ambito = alcanzaTodo ? string.Empty : " (dentro de lo que podés ver)";

        var texto = plan.Medida switch
        {
            CatalogoDelPlan.Conteo => Conteo(Entero(resultado, 0, 0), filtros, ambito),
            CatalogoDelPlan.Porcentaje => Porcentaje(
                Entero(resultado, 0, 0), Entero(resultado, 0, 1), filtros,
                Frases(plan.Condiciones, entidades), ambito),
            CatalogoDelPlan.Listado => Listado(resultado, filtros, ambito),
            _ => throw new InvalidOperationException($"La medida '{plan.Medida}' no tiene plantilla."),
        };

        return texto + Definiciones(plan);
    }

    private static string Conteo(long total, string filtros, string ambito) => total switch
    {
        0 => $"No hay docentes con designación vigente{filtros}{ambito}.",
        1 => $"Hay 1 docente con designación vigente{filtros}{ambito}.",
        _ => $"Hay {total} docentes con designación vigente{filtros}{ambito}.",
    };

    private static string Porcentaje(long cumplen, long total, string filtros, string condiciones, string ambito)
    {
        if (total == 0)
        {
            return $"No hay docentes con designación vigente{filtros}{ambito}, así que el porcentaje no se puede calcular.";
        }

        var porcentaje = Math.Round(100m * cumplen / total, 1, MidpointRounding.AwayFromZero);

        return string.Format(
            Castellano,
            "El {0:0.#} % de los docentes con designación vigente{1} ({2} de {3}){4}{5}.",
            porcentaje, filtros, cumplen, total, condiciones, ambito);
    }

    private static string Listado(ResultadoDeConsulta resultado, string filtros, string ambito)
    {
        if (resultado.Filas.Count == 0)
        {
            return $"No hay docentes con designación vigente{filtros}{ambito}.";
        }

        var nombres = resultado.Filas
            .Take(NombresEnElTexto)
            .Select(fila => $"{fila[0]}, {fila[1]}");

        var cuantos = resultado.Truncado
            ? $"Hay más de {resultado.Filas.Count} docentes"
            : resultado.Filas.Count == 1 ? "Hay 1 docente" : $"Hay {resultado.Filas.Count} docentes";

        var resto = resultado.Filas.Count > NombresEnElTexto ? " (y más en la tabla)" : string.Empty;

        return $"{cuantos} con designación vigente{filtros}{ambito}: {string.Join("; ", nombres)}{resto}.";
    }

    private static long Entero(ResultadoDeConsulta resultado, int fila, int columna) =>
        resultado.Filas.Count > fila && resultado.Filas[fila].Count > columna
            ? Convert.ToInt64(resultado.Filas[fila][columna], CultureInfo.InvariantCulture)
            : throw new InvalidOperationException("El resultado no tiene la forma que la medida promete.");

    private static string Frases(IEnumerable<CondicionValidada> condiciones, IReadOnlyDictionary<int, EntidadResuelta>? entidades)
    {
        var frases = condiciones.Select(condicion => Frase(condicion, entidades)).ToList();
        return frases.Count == 0 ? string.Empty : " " + string.Join(" y ", frases);
    }

    private static string Frase(CondicionValidada condicion, IReadOnlyDictionary<int, EntidadResuelta>? entidades)
    {
        var nombre = entidades is not null && entidades.TryGetValue(condicion.Indice, out var resuelta)
            ? resuelta.Nombre
            : condicion.NombreOriginal;
        var negada = condicion.Operador == "!=";

        return condicion.Campo.Nombre switch
        {
            CatalogoDelPlan.Cargo => (negada ? "sin cargo de " : "con cargo de ")
                + CatalogoDelPlan.Cargos.Single(cargo => cargo.Codigo == condicion.Codigo).Etiqueta,
            CatalogoDelPlan.Carrera => (negada ? "que no dictan en la carrera " : "que dictan en la carrera ") + nombre,
            CatalogoDelPlan.Materia => (negada ? "que no dictan la materia " : "que dictan la materia ") + nombre,
            CatalogoDelPlan.Dedicacion => $"con alguna designación de categoría {Cantidad(condicion)}",
            CatalogoDelPlan.CantidadDeCarreras => $"que dictan en {Cantidad(condicion)} carreras",
            CatalogoDelPlan.CantidadDeMaterias => $"que dictan {Cantidad(condicion)} materias",
            CatalogoDelPlan.AntiguedadDesdeDesignacion =>
                $"con {Cantidad(condicion)} años de antigüedad desde su primera designación",
            CatalogoDelPlan.AntiguedadDeclarada =>
                $"con {Cantidad(condicion)} años de antigüedad según la experiencia declarada en el portal",
            _ => condicion.Campo.Nombre,
        };
    }

    private static string Cantidad(CondicionValidada condicion)
    {
        var numero = condicion.Numero!.Value.ToString(CultureInfo.InvariantCulture);

        return condicion.Operador switch
        {
            ">" => $"más de {numero}",
            ">=" => $"al menos {numero}",
            "<" => $"menos de {numero}",
            "<=" => $"como máximo {numero}",
            _ => $"exactamente {numero}",
        };
    }

    private static string Definiciones(PlanValidado plan)
    {
        var notas = new List<string>();

        bool JuntasEn(IReadOnlyList<CondicionValidada> lista) => lista.Count(c =>
            c.Operador != "!=" && c.Campo.Nombre is CatalogoDelPlan.Cargo or CatalogoDelPlan.Carrera
                or CatalogoDelPlan.Materia or CatalogoDelPlan.Dedicacion) > 1;

        if (JuntasEn(plan.Filtros) || JuntasEn(plan.Condiciones))
        {
            notas.Add("el cargo, la carrera, la materia y la categoría se cumplen en una misma designación");
        }

        if (plan.Todas.Any(c => c.Campo.Nombre == CatalogoDelPlan.AntiguedadDesdeDesignacion))
        {
            notas.Add("la antigüedad se cuenta desde la primera designación registrada");
        }

        if (plan.Todas.Any(c => c.Campo.Nombre == CatalogoDelPlan.AntiguedadDeclarada))
        {
            notas.Add("la antigüedad declarada sale de la experiencia más antigua cargada en el portal; quien no la cargó no cuenta");
        }

        return notas.Count == 0 ? string.Empty : $" Nota: {string.Join("; ", notas)}.";
    }
}
