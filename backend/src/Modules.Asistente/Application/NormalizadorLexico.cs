using System.Globalization;
using System.Text;

namespace Modules.Asistente.Application;

/// <summary>
/// Convierte una pregunta en el conjunto de términos con que se la compara.
/// </summary>
/// <remarks>
/// Es la única pieza del selector que decide qué cuenta como «parecido». Hace
/// tres cosas y ninguna más: baja a minúsculas y saca acentos, descarta las
/// palabras vacías del español, y unifica los sinónimos del dominio.
///
/// Los sinónimos son la parte que no es genérica. En el Departamento «profesor»,
/// «docente» y «agente» nombran lo mismo, y «trámite», «pedido» y «solicitud»
/// también. Sin unificarlos, dos preguntas idénticas escritas por dos personas
/// distintas seleccionarían ejemplos distintos.
/// </remarks>
internal static class NormalizadorLexico
{
    /// <summary>
    /// Palabras vacías del español. Se descartan porque aparecen en casi toda
    /// pregunta: si contaran, dos preguntas sin nada en común pero largas se
    /// parecerían más que dos cortas sobre el mismo tema.
    /// </summary>
    private static readonly HashSet<string> PalabrasVacias = new(StringComparer.Ordinal)
    {
        "a", "al", "algo", "algun", "alguna", "algunas", "alguno", "algunos", "ante",
        "aqui", "asi", "aun", "cada", "como", "con", "contra", "cual", "cuales",
        "cuando", "de", "del", "desde", "donde", "dos", "e", "el", "ella", "ellas",
        "ellos", "en", "entre", "era", "es", "esa", "esas", "ese", "eso", "esos",
        "esta", "estan", "estas", "este", "esto", "estos", "fue", "fueron", "ha",
        "hace", "hacia", "han", "hasta", "hay", "la", "las", "le", "les", "lo",
        "los", "mas", "me", "mi", "muy", "ni", "no", "nos", "o", "otra", "otras",
        "otro", "otros", "para", "pero", "por", "porque", "que", "quien", "quienes",
        "se", "segun", "ser", "si", "sin", "sobre", "son", "su", "sus", "tambien",
        "tiene", "tienen", "todo", "todos", "un", "una", "unas", "uno", "unos",
        "y", "ya",
    };

    /// <summary>
    /// Sinónimos del dominio, ya normalizados. La clave es como lo escribe la
    /// gente; el valor, el término canónico con que se compara.
    /// </summary>
    private static readonly Dictionary<string, string> Sinonimos = new(StringComparer.Ordinal)
    {
        ["profesor"] = "docente",
        ["profesores"] = "docente",
        ["profesora"] = "docente",
        ["profesoras"] = "docente",
        ["docentes"] = "docente",
        ["agente"] = "docente",
        ["agentes"] = "docente",
        ["persona"] = "docente",
        ["personas"] = "docente",

        ["asignatura"] = "materia",
        ["asignaturas"] = "materia",
        ["catedra"] = "materia",
        ["catedras"] = "materia",
        ["materias"] = "materia",

        ["carreras"] = "carrera",
        ["plan"] = "carrera",

        ["tramite"] = "pedido",
        ["tramites"] = "pedido",
        ["solicitud"] = "pedido",
        ["solicitudes"] = "pedido",
        ["solicito"] = "pedido",
        ["solicitaron"] = "pedido",
        ["solicitados"] = "pedido",
        ["pedidos"] = "pedido",
        ["novedad"] = "pedido",
        ["novedades"] = "pedido",

        ["nombramiento"] = "designacion",
        ["nombramientos"] = "designacion",
        ["designaciones"] = "designacion",
        ["designado"] = "designacion",
        ["designados"] = "designacion",
        ["designada"] = "designacion",
        ["designadas"] = "designacion",

        ["cargos"] = "cargo",
        ["categoria"] = "cargo",
        ["jerarquia"] = "cargo",

        ["periodos"] = "periodo",
        ["ciclo"] = "periodo",
        ["cuatrimestre"] = "periodo",

        ["vigente"] = "activo",
        ["vigentes"] = "activo",
        ["actual"] = "activo",
        ["actuales"] = "activo",
        ["activas"] = "activo",
        ["activos"] = "activo",
        ["activa"] = "activo",

        ["cuantas"] = "cuantos",
        ["cantidad"] = "cuantos",
        ["total"] = "cuantos",

        ["horas"] = "hora",
        ["roles"] = "rol",
        ["permisos"] = "permiso",
        ["estados"] = "estado",
        ["planteles"] = "plantel",
    };

    /// <summary>Convierte el texto en su conjunto de términos comparables.</summary>
    public static IReadOnlySet<string> Terminos(string texto)
    {
        var terminos = new HashSet<string>(StringComparer.Ordinal);

        foreach (var crudo in Trocear(texto))
        {
            var termino = SinAcentos(crudo);
            if (termino.Length < 2 || PalabrasVacias.Contains(termino))
            {
                continue;
            }

            terminos.Add(Sinonimos.TryGetValue(termino, out var canonico) ? canonico : termino);
        }

        return terminos;
    }

    /// <summary>
    /// Las palabras del texto en orden, en minúscula y sin acentos, sin descartar
    /// ninguna.
    /// </summary>
    /// <remarks>
    /// Es lo que <see cref="Terminos"/> tiene antes de tirar las palabras vacías y
    /// de aplicar sinónimos. Lo necesita el enrutador social, que decide
    /// justamente por lo que queda después de sacar la apertura de cortesía: si
    /// arrancara de un conjunto ya sin palabras vacías, «¿qué tal?» llegaría vacío
    /// desde el principio y cualquier pregunta corta se confundiría con un saludo.
    /// </remarks>
    public static IReadOnlyList<string> Palabras(string texto) =>
        [.. Trocear(texto).Select(SinAcentos)];

    /// <summary>
    /// Parte el texto en palabras. Todo lo que no sea letra o dígito separa,
    /// así que los signos de interrogación y la puntuación no dejan rastro.
    /// </summary>
    private static IEnumerable<string> Trocear(string texto)
    {
        var palabra = new StringBuilder();

        foreach (var caracter in texto)
        {
            if (char.IsLetterOrDigit(caracter))
            {
                palabra.Append(char.ToLowerInvariant(caracter));
                continue;
            }

            if (palabra.Length > 0)
            {
                yield return palabra.ToString();
                palabra.Clear();
            }
        }

        if (palabra.Length > 0)
        {
            yield return palabra.ToString();
        }
    }

    /// <summary>
    /// Saca los acentos descomponiendo y descartando las marcas diacríticas.
    /// </summary>
    /// <remarks>
    /// La eñe cae también: al descomponerla queda «n» más una tilde combinante,
    /// que es marca diacrítica. Es lo buscado —quien escribe «diseño» y quien
    /// escribe «diseno» pregunta lo mismo— y ninguno de los términos del dominio
    /// colisiona con otro al plegarla.
    /// </remarks>
    private static string SinAcentos(string palabra)
    {
        var descompuesta = palabra.Normalize(NormalizationForm.FormD);
        var limpia = new StringBuilder(descompuesta.Length);

        foreach (var caracter in descompuesta)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            {
                limpia.Append(caracter);
            }
        }

        return limpia.ToString().Normalize(NormalizationForm.FormC);
    }
}
