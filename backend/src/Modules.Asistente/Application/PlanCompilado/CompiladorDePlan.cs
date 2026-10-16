using System.Globalization;
using System.Text;

namespace Modules.Asistente.Application;

/// <summary>La consulta compilada y los ids que liga.</summary>
internal sealed record ConsultaCompilada(string Sql, IReadOnlyDictionary<string, Guid> Bindings);

/// <summary>
/// Compila un plan validado a una única consulta de lectura (D8 de
/// <c>asistente-plan-compilado</c>).
/// </summary>
/// <remarks>
/// <b>Ningún texto del modelo llega al SQL.</b> Los ids de las entidades van como
/// marcadores <c>$refN</c>, lo único que el ejecutor sabe ligar; los códigos de
/// cargo salen de la lista cerrada del catálogo y los números ya son enteros
/// validados. La fecha de referencia es la del turno, nunca el reloj del servidor:
/// una antigüedad calculada con <c>now()</c> cambiaría de un día para otro sin que
/// cambie ningún dato.
///
/// La población es la de las designaciones vigentes que el actor puede ver: RLS
/// filtra <c>designaciones.designaciones</c>, así que un actor acotado cuenta solo
/// su ámbito sin que el compilador lo sepa.
/// </remarks>
internal static class CompiladorDePlan
{
    private const string Vigentes =
        """
        WITH vigentes AS (
            SELECT d.persona_id, d.materia_id, m.carrera_id, c.codigo AS cargo, ded.codigo AS dedicacion
              FROM designaciones.designaciones d
              JOIN identity.materias m ON m.id = d.materia_id
              JOIN designaciones.cargos c ON c.id = d.cargo_id
              LEFT JOIN designaciones.dedicaciones ded ON ded.id = d.dedicacion_id
             WHERE d.vigente_hasta IS NULL
        ),
        docentes AS (
            SELECT DISTINCT v.persona_id FROM vigentes v
        )
        """;

    public static ConsultaCompilada Compilar(
        PlanValidado plan, IReadOnlyDictionary<int, EntidadResuelta> entidades, DateOnly fecha)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(entidades);

        var bindings = new Dictionary<string, Guid>(StringComparer.Ordinal);

        string Grupo(IReadOnlyList<CondicionValidada> condiciones, string separador) =>
            condiciones.Count == 0
                ? "TRUE"
                : string.Join(separador, Predicados(condiciones, entidades, bindings, fecha));

        var filtros = Grupo(plan.Filtros, "\n   AND ");

        var sql = new StringBuilder(Vigentes);

        switch (plan.Medida)
        {
            case CatalogoDelPlan.Conteo:
                sql.Append($"SELECT count(*) AS total\n  FROM docentes p\n WHERE {filtros}");
                break;

            case CatalogoDelPlan.Porcentaje:
                var condiciones = Grupo(plan.Condiciones, "\n       AND ");
                sql.Append(
                    $"SELECT count(*) FILTER (WHERE {condiciones}) AS cumplen, count(*) AS total\n"
                    + $"  FROM docentes p\n WHERE {filtros}");
                break;

            case CatalogoDelPlan.Listado:
                sql.Append(
                    "SELECT pe.apellido, pe.nombre\n  FROM docentes p\n"
                    + "  JOIN identity.personas pe ON pe.id = p.persona_id\n"
                    + $" WHERE {filtros}\n ORDER BY pe.apellido, pe.nombre");
                break;

            default:
                throw new InvalidOperationException(
                    $"La medida '{plan.Medida}' llegó al compilador sin pasar el validador.");
        }

        return new ConsultaCompilada(sql.ToString(), bindings);
    }

    /// <summary>Los campos que se cumplen sobre una designación y no sobre la persona.</summary>
    private static readonly HashSet<string> DeLaDesignacion = new(StringComparer.Ordinal)
    {
        CatalogoDelPlan.Cargo, CatalogoDelPlan.Carrera, CatalogoDelPlan.Materia, CatalogoDelPlan.Dedicacion,
    };

    /// <summary>
    /// Los predicados de una lista del plan.
    /// </summary>
    /// <remarks>
    /// <b>Las condiciones afirmativas sobre la designación van juntas, en un solo
    /// <c>EXISTS</c></b>: «titulares de Ingeniería Industrial» son quienes tienen una
    /// designación vigente como titular EN una materia de Industrial, no quienes son
    /// titulares en otra carrera y adjuntos en Industrial. Las negadas van cada una
    /// en su <c>NOT EXISTS</c>, y las de la persona —cantidades y antigüedades—
    /// cada una por su lado.
    /// </remarks>
    private static IEnumerable<string> Predicados(
        IReadOnlyList<CondicionValidada> condiciones,
        IReadOnlyDictionary<int, EntidadResuelta> entidades,
        Dictionary<string, Guid> bindings,
        DateOnly fecha)
    {
        var afirmativas = condiciones
            .Where(c => DeLaDesignacion.Contains(c.Campo.Nombre) && c.Operador != "!=")
            .Select(c => SobreLaDesignacion(c, entidades, bindings))
            .ToList();

        if (afirmativas.Count > 0)
        {
            yield return "EXISTS (SELECT 1 FROM vigentes v WHERE v.persona_id = p.persona_id AND "
                + string.Join(" AND ", afirmativas) + ")";
        }

        foreach (var negada in condiciones.Where(c => DeLaDesignacion.Contains(c.Campo.Nombre) && c.Operador == "!="))
        {
            yield return "NOT EXISTS (SELECT 1 FROM vigentes v WHERE v.persona_id = p.persona_id AND "
                + SobreLaDesignacion(negada, entidades, bindings) + ")";
        }

        foreach (var dePersona in condiciones.Where(c => !DeLaDesignacion.Contains(c.Campo.Nombre)))
        {
            yield return SobreLaPersona(dePersona, fecha);
        }
    }

    private static string SobreLaDesignacion(
        CondicionValidada condicion,
        IReadOnlyDictionary<int, EntidadResuelta> entidades,
        Dictionary<string, Guid> bindings) => condicion.Campo.Nombre switch
    {
        CatalogoDelPlan.Cargo => $"v.cargo = '{CodigoDeCargo(condicion)}'",
        CatalogoDelPlan.Carrera => $"v.carrera_id IN ({Marcadores(condicion, entidades, bindings)})",
        CatalogoDelPlan.Materia => $"v.materia_id IN ({Marcadores(condicion, entidades, bindings)})",
        CatalogoDelPlan.Dedicacion => $"v.dedicacion {Comparacion(condicion)}",
        _ => throw new InvalidOperationException(
            $"El campo '{condicion.Campo.Nombre}' no se evalúa sobre la designación."),
    };

    private static string SobreLaPersona(CondicionValidada condicion, DateOnly fecha)
    {
        var referencia = string.Create(CultureInfo.InvariantCulture, $"DATE '{fecha:yyyy-MM-dd}'");

        return condicion.Campo.Nombre switch
        {
            CatalogoDelPlan.CantidadDeCarreras =>
                $"(SELECT count(DISTINCT v.carrera_id) FROM vigentes v WHERE v.persona_id = p.persona_id) {Comparacion(condicion)}",

            CatalogoDelPlan.CantidadDeMaterias =>
                $"(SELECT count(DISTINCT v.materia_id) FROM vigentes v WHERE v.persona_id = p.persona_id) {Comparacion(condicion)}",

            CatalogoDelPlan.AntiguedadDesdeDesignacion =>
                "(SELECT extract(year FROM age(" + referencia + ", min(h.vigente_desde))) "
                + "FROM designaciones.designaciones h WHERE h.persona_id = p.persona_id) "
                + Comparacion(condicion),

            CatalogoDelPlan.AntiguedadDeclarada =>
                "(SELECT extract(year FROM age(" + referencia + ", min(e.desde))) "
                + "FROM portal.experiencias e JOIN portal.perfiles pf ON pf.id = e.perfil_id "
                + "WHERE pf.persona_id = p.persona_id) "
                + Comparacion(condicion),

            _ => throw new InvalidOperationException(
                $"El campo '{condicion.Campo.Nombre}' no tiene compilación."),
        };
    }

    private static string CodigoDeCargo(CondicionValidada condicion) =>
        CatalogoDelPlan.Cargos.Single(cargo => cargo.Codigo == condicion.Codigo).Codigo;

    private static string Comparacion(CondicionValidada condicion)
    {
        var operador = condicion.Operador switch
        {
            "=" or ">" or ">=" or "<" or "<=" => condicion.Operador,
            _ => throw new InvalidOperationException(
                $"El operador '{condicion.Operador}' no compara enteros."),
        };

        return string.Create(CultureInfo.InvariantCulture, $"{operador} {condicion.Numero!.Value}");
    }

    private static string Marcadores(
        CondicionValidada condicion,
        IReadOnlyDictionary<int, EntidadResuelta> entidades,
        Dictionary<string, Guid> bindings)
    {
        var resuelta = entidades.TryGetValue(condicion.Indice, out var entidad)
            ? entidad
            : throw new InvalidOperationException(
                $"La condición {condicion.Indice} llegó al compilador sin su entidad resuelta.");

        var marcadores = new List<string>();

        foreach (var id in resuelta.Ids)
        {
            var marcador = string.Create(CultureInfo.InvariantCulture, $"$ref{bindings.Count + 1}");
            bindings[marcador] = id;
            marcadores.Add(marcador);
        }

        return string.Join(", ", marcadores);
    }
}
