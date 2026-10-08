using System.Text.Json.Nodes;

namespace Modules.Asistente.Application;

/// <summary>Cómo interpreta el validador el valor de una condición.</summary>
internal enum TipoDeCampo
{
    /// <summary>Un código de <c>designaciones.cargos</c>, de una lista cerrada.</summary>
    Cargo,

    /// <summary>Un nombre que se resuelve en el servidor a uno o más ids.</summary>
    Entidad,

    /// <summary>Un entero dentro del rango del campo.</summary>
    Entero,
}

/// <summary>Un campo que el plan puede filtrar o poner como condición.</summary>
/// <param name="Nombre">Como aparece en el plan.</param>
/// <param name="Terminos">
/// Frases que anclan el campo en la pregunta (D6). Vacío para las entidades, que
/// se anclan por su propio nombre.
/// </param>
internal sealed record CampoDelPlan(
    string Nombre,
    TipoDeCampo Tipo,
    IReadOnlyList<string> Operadores,
    string Descripcion,
    IReadOnlyList<string> Terminos,
    int Minimo = 0,
    int Maximo = 0);

/// <summary>Un cargo del catálogo, con las formas en que lo nombra la gente.</summary>
internal sealed record CargoDelPlan(string Codigo, string Etiqueta, IReadOnlyList<string> Sinonimos);

/// <summary>
/// El catálogo semántico del plan compilado: lo único que el plan puede expresar.
/// </summary>
/// <remarks>
/// <b>Es código y no configuración</b> (D2 de <c>asistente-plan-compilado</c>):
/// cada campo tiene su fragmento de SQL en <see cref="CompiladorDePlan"/> y su
/// test, y de acá salen el esquema de salida, el prompt y las etiquetas de la
/// interpretación, así que no pueden divergir.
///
/// La población es una sola: personas con al menos una designación vigente
/// (<c>vigente_hasta IS NULL</c>) que el actor puede ver.
/// </remarks>
internal static class CatalogoDelPlan
{
    public const string Cargo = "cargo";
    public const string Carrera = "carrera";
    public const string Materia = "materia";
    public const string Dedicacion = "dedicacion";
    public const string CantidadDeCarreras = "cantidad_carreras";
    public const string CantidadDeMaterias = "cantidad_materias";
    public const string AntiguedadDesdeDesignacion = "antiguedad_designacion";
    public const string AntiguedadDeclarada = "antiguedad_declarada";

    public const string Conteo = "conteo";
    public const string Porcentaje = "porcentaje";
    public const string Listado = "listado";

    public static readonly IReadOnlyList<string> Medidas = [Conteo, Porcentaje, Listado];

    private static readonly string[] Igualdad = ["=", "!="];
    private static readonly string[] Comparacion = ["=", ">", ">=", "<", "<="];

    public static readonly IReadOnlyList<string> Operadores = ["=", "!=", ">", ">=", "<", "<="];

    public static readonly IReadOnlyList<CargoDelPlan> Cargos =
    [
        new("titular", "profesor titular", ["titular", "titulares", "profesor titular", "profesores titulares"]),
        new("asociado", "profesor asociado", ["asociado", "asociados", "asociada", "asociadas"]),
        new("adjunto", "profesor adjunto", ["adjunto", "adjuntos", "adjunta", "adjuntas"]),
        new("jtp", "jefe de trabajos prácticos",
            ["jtp", "jtps", "jefe de trabajos practicos", "jefes de trabajos practicos",
             "jefa de trabajos practicos", "jefas de trabajos practicos"]),
        new("ayudante1", "ayudante de primera",
            ["ayudante de primera", "ayudantes de primera", "ayudante 1", "ayudantes 1"]),
        new("ayudante2", "ayudante de segunda",
            ["ayudante de segunda", "ayudantes de segunda", "ayudante 2", "ayudantes 2"]),
    ];

    public static readonly IReadOnlyList<string> PalabrasDeMateria =
        ["materia", "materias", "asignatura", "asignaturas", "catedra", "catedras"];

    public static readonly IReadOnlyList<string> PalabrasDeCarrera = ["carrera", "carreras"];

    public static readonly IReadOnlyList<string> PalabrasDeDedicacion =
        ["categoria", "categorias", "dedicacion", "dedicaciones"];

    /// <summary>Lo que hace que una antigüedad sea «desde la primera designación».</summary>
    public static readonly IReadOnlyList<string> CalificadoresDeDesignacion =
        ["designacion", "designaciones", "designado", "designada",
         "nombramiento", "nombrado", "nombrada", "en la universidad", "en la unlam"];

    /// <summary>Lo que hace que una antigüedad sea la declarada en el portal.</summary>
    public static readonly IReadOnlyList<string> CalificadoresDeDeclarada =
        ["declarada", "declarado", "declaro", "declararon", "portal", "experiencia", "trayectoria"];

    public static readonly IReadOnlyDictionary<string, CampoDelPlan> Campos =
        new Dictionary<string, CampoDelPlan>(StringComparer.Ordinal)
        {
            [Cargo] = new(Cargo, TipoDeCampo.Cargo, Igualdad,
                "Tiene (=) o no tiene (!=) una designación vigente con ese cargo. "
                + "Valor: uno de " + string.Join(", ", Cargos.Select(c => c.Codigo)) + ".", []),
            [Carrera] = new(Carrera, TipoDeCampo.Entidad, Igualdad,
                "Tiene (=) o no tiene (!=) una designación vigente en una materia de esa carrera. "
                + "Valor: el nombre de la carrera tal como aparece en la pregunta.", []),
            [Materia] = new(Materia, TipoDeCampo.Entidad, Igualdad,
                "Tiene (=) o no tiene (!=) una designación vigente en esa materia. "
                + "Valor: el nombre de la materia tal como aparece en la pregunta.", []),
            [Dedicacion] = new(Dedicacion, TipoDeCampo.Entero, Comparacion,
                "Alguna designación vigente con esa categoría de dedicación (1 a 6).",
                PalabrasDeDedicacion, 1, 6),
            [CantidadDeCarreras] = new(CantidadDeCarreras, TipoDeCampo.Entero, Comparacion,
                "Cantidad de carreras distintas en las que tiene designaciones vigentes.",
                PalabrasDeCarrera, 0, 20),
            [CantidadDeMaterias] = new(CantidadDeMaterias, TipoDeCampo.Entero, Comparacion,
                "Cantidad de materias distintas en las que tiene designaciones vigentes.",
                PalabrasDeMateria, 0, 50),
            [AntiguedadDesdeDesignacion] = new(AntiguedadDesdeDesignacion, TipoDeCampo.Entero, Comparacion,
                "Años cumplidos desde su primera designación registrada (vigente o no).",
                ["antiguedad", "anos"], 0, 60),
            [AntiguedadDeclarada] = new(AntiguedadDeclarada, TipoDeCampo.Entero, Comparacion,
                "Años cumplidos desde la experiencia más antigua declarada en el portal docente.",
                ["antiguedad", "anos"], 0, 70),
        };

    /// <summary>Palabras que nombran a la población: sin una de ellas, no es una pregunta del plan.</summary>
    public static readonly IReadOnlyList<string> PalabrasDePoblacion =
        ["docente", "docentes", "profesor", "profesores", "profesora", "profesoras",
         "designado", "designados", "designada", "designadas", "plantel", "dictan", "dicta"];

    /// <summary>
    /// Prefijos de vocabulario de dominio que el catálogo NO expresa. Una pregunta que
    /// los usa sigue por el carril SQL sin gastar una llamada en el plan (D4).
    /// </summary>
    public static readonly IReadOnlyList<string> PrefijosFueraDelCatalogo =
    [
        "pedid", "solicitud", "tramit", "period", "cuatrimestr", "semestr", "lote", "hora",
        "investigacion", "habilidad", "certific", "formacion", "titulo", "doctor", "magister",
        "maestri", "posgrad", "licencia", "renuncia", "jubil", "rechaz", "aprobad", "revision",
        "estado", "prioritari", "historial", "usuario", "permiso", "legajo", "documento",
        "telefono", "correo", "nacimiento", "sueldo", "salario", "promedio", "historic", "pasad",
        "anterior", "agrupad", "desglos", "ranking",
    ];

    /// <summary>Palabras enteras o frases fuera del catálogo que no sirven como prefijo.</summary>
    public static readonly IReadOnlyList<string> FrasesFueraDelCatalogo =
    [
        "dni", "cuil", "mail", "edad", "suma", "baja", "bajas", "alta", "altas", "interes",
        "intereses", "hubo", "habia", "habian", "cada", "por carrera", "por materia", "por cargo",
        "por categoria", "por dedicacion",
    ];

    /// <summary>Busca el cargo por su código o por cualquiera de sus sinónimos.</summary>
    public static CargoDelPlan? CargoDe(string valor)
    {
        var normalizado = TextoDeLaPregunta.Normalizar(valor);

        return Cargos.FirstOrDefault(cargo =>
            string.Equals(cargo.Codigo, normalizado, StringComparison.Ordinal)
            || cargo.Sinonimos.Any(sinonimo => string.Equals(sinonimo, normalizado, StringComparison.Ordinal)));
    }

    /// <summary>
    /// El esquema JSON de la salida estructurada: llama-server lo convierte en
    /// gramática y vLLM en una máscara de xgrammar.
    /// </summary>
    public static string EsquemaJson { get; } = ArmarEsquema();

    private static string ArmarEsquema()
    {
        JsonObject Condicion() => new()
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = new JsonArray("campo", "operador", "valor"),
            ["properties"] = new JsonObject
            {
                ["campo"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray([.. Campos.Keys.Select(campo => (JsonNode)campo)]),
                },
                ["operador"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray([.. Operadores.Select(operador => (JsonNode)operador)]),
                },
                ["valor"] = new JsonObject { ["type"] = "string" },
            },
        };

        var esquema = new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = new JsonArray("expresable", "medida", "filtros", "condiciones"),
            ["properties"] = new JsonObject
            {
                ["expresable"] = new JsonObject { ["type"] = "boolean" },
                ["medida"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray([.. Medidas.Select(medida => (JsonNode)medida)]),
                },
                ["filtros"] = new JsonObject { ["type"] = "array", ["maxItems"] = 4, ["items"] = Condicion() },
                ["condiciones"] = new JsonObject { ["type"] = "array", ["maxItems"] = 4, ["items"] = Condicion() },
            },
        };

        return esquema.ToJsonString();
    }
}
