namespace Modules.Asistente.Application;

/// <summary>La línea con que el asistente se presenta, según el rol del actor (RF-04).</summary>
internal static class PresentacionPorRol
{
    /// <summary>La presentación del actor: su ámbito, y las áreas que de verdad alcanza.</summary>
    public static string Texto(PerfilDelActor perfil)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        var areas = new List<string>();

        // El trámite es lo que más se pregunta, así que va primero — y sólo si el actor tiene el
        // permiso de dominio: sin él la RLS le devuelve cero filas sobre las cuatro tablas del
        // trámite, y anunciarlo sería prometer vacío.
        if (perfil.VeDesignaciones)
        {
            areas.Add($"los trámites y las designaciones {AmbitoDe(perfil.CodigoDeRol)}");
        }

        // Los catálogos no llevan permiso propio: se le conceden a los dos roles de
        // lectura y no tienen policy. Cualquiera que use el asistente los alcanza.
        areas.Add("las materias, las carreras y los cargos del sistema");

        areas.Add(perfil.VeTrayectoriaAjena
            ? "los perfiles profesionales de los docentes"
            : "tu propio perfil profesional");

        return $"Preguntá por {Enumerar(areas)}.";
    }

    /// <summary>
    /// Cómo se nombra el ámbito del actor dentro de la frase. Es lo único que sigue saliendo del
    /// rol, y por el mismo motivo de siempre: el ámbito real lo impone la RLS y esto es sólo cómo
    /// se lo nombra.
    /// </summary>
    private static string AmbitoDe(string? codigoDeRol) =>
        codigoDeRol is not null && AmbitoPorCodigo.TryGetValue(codigoDeRol, out var ambito)
            ? ambito
            : AmbitoGenerico;

    /// <summary>El ámbito de quien no cae en ninguna entrada de la tabla.</summary>
    public const string AmbitoGenerico = "del sistema";

    private static readonly Dictionary<string, string> AmbitoPorCodigo =
        new(StringComparer.Ordinal)
        {
            ["jefe_catedra"] = "de tu cátedra",
            ["coordinador_carrera"] = "de tu carrera",
            ["secretaria"] = "de todo el Departamento",
            ["decanato"] = "de todo el Departamento",
            ["administrativo"] = "de todo el Departamento",
            ["docente"] = "tuyas",
        };

    /// <summary>«a», «a y b», «a, b y c».</summary>
    private static string Enumerar(IReadOnlyList<string> partes) => partes.Count switch
    {
        1 => partes[0],
        2 => $"{partes[0]} y {partes[1]}",
        _ => $"{string.Join(", ", partes.Take(partes.Count - 1))} y {partes[^1]}",
    };
}
