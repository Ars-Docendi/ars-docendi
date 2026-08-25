namespace Modules.Asistente.Application;

/// <summary>En cuál de las tres clases sociales cae un mensaje, si es que cae.</summary>
public enum IntencionSocial
{
    /// <summary>No es social: sigue al carril de datos.</summary>
    Ninguna,

    /// <summary>Un saludo, sin nada más.</summary>
    Saludo,

    /// <summary>
    /// Un agradecimiento o un cierre de conversación, sin nada más.
    /// </summary>
    /// <remarks>
    /// Las despedidas caen acá y no en una clase propia. Son el mismo caso —cortesía
    /// sin pregunta— y se responden bien con el mismo texto; separarlas agregaría una
    /// clase que no cambia ninguna decisión.
    /// </remarks>
    Agradecimiento,

    /// <summary>Una pregunta sobre el asistente mismo, no sobre los datos.</summary>
    Meta,
}

/// <summary>
/// Carril sin datos: resuelve saludo, agradecimiento y meta-pregunta a costo cero
/// de tokens.
/// </summary>
/// <remarks>
/// Sin esto, un «hola» atraviesa el pipeline completo —dos llamadas al modelo, casi
/// tres segundos— para terminar diciéndole al usuario que su mensaje no contiene
/// una consulta.
///
/// <b>Clasificar la intención con el modelo está descartado con evidencia</b>: 60%
/// de F1 en triage de cinco clases, 77,4% en nueve vías. Un clasificador que falla
/// una de cada cuatro veces, cuesta una llamada y corta el flujo es peor que una
/// tabla.
///
/// Clase pura: se ejercita en memoria, sin base y sin red.
/// </remarks>
public static class EnrutadorSocial
{
    /// <summary>
    /// Palabras que forman una apertura o un cierre de cortesía y no aportan
    /// contenido.
    /// </summary>
    /// <remarks>
    /// Incluye piezas ambiguas como «que», «como», «todo» y «bien» a propósito.
    /// Solas no deciden nada: la clasificación no mira si aparecen, mira si queda
    /// algo <b>además</b> de ellas. «¿qué tal?» se vacía entero y es un saludo;
    /// «¿cómo se llama el docente?» deja «se», «llama» y «docente», y sigue de largo.
    /// </remarks>
    private static readonly HashSet<string> Cortesia = new(StringComparer.Ordinal)
    {
        // Saludo
        "hola", "holis", "ola", "buenas", "buen", "buenos", "dia", "dias",
        "tarde", "tardes", "noche", "noches", "hey", "ey", "saludos",
        "que", "tal", "como", "estas", "esta", "andas", "va", "todo", "bien",
        // Cierre y agradecimiento
        "gracias", "muchas", "mil", "muy", "amable", "genial", "perfecto",
        "barbaro", "dale", "ok", "okey", "listo", "buenisimo", "excelente",
        "chau", "adios", "nos", "vemos", "hasta", "luego", "nada", "mas",
        "por", "ahora", "eso", "es", "de", "a", "el", "la", "un", "una",
    };

    /// <summary>Palabras que marcan el mensaje como agradecimiento o cierre.</summary>
    private static readonly HashSet<string> Cierre = new(StringComparer.Ordinal)
    {
        "gracias", "chau", "adios", "listo", "dale", "perfecto", "genial",
        "barbaro", "excelente", "buenisimo", "vemos", "luego", "ok", "okey",
    };

    /// <summary>Palabras que marcan el mensaje como saludo.</summary>
    private static readonly HashSet<string> Saludo = new(StringComparer.Ordinal)
    {
        "hola", "holis", "ola", "buenas", "buen", "buenos", "hey", "ey",
        "saludos", "tal", "andas", "estas",
    };

    /// <summary>
    /// Frases que preguntan por el asistente mismo.
    /// </summary>
    /// <remarks>
    /// Deliberadamente angostas y todas con una referencia al asistente. Una lista
    /// laxa se comería «¿qué carreras hay?», y devolverle al usuario un texto sobre
    /// capacidades cuando pidió la lista de carreras es peor que no tener la clase.
    /// </remarks>
    private static readonly string[] Meta =
    [
        "que podes hacer", "que puedes hacer", "que sabes hacer",
        "que cosas podes hacer", "que cosas puedes hacer",
        "para que servis", "para que sirves", "para que sos", "para que estas",
        "como funcionas", "como te uso", "como se usa",
        "que puedo preguntar", "que puedo preguntarte", "que te puedo preguntar",
        "en que me podes ayudar", "en que podes ayudarme", "en que me puedes ayudar",
        "quien sos", "quien eres", "que sos", "que eres",
    ];

    /// <summary>Palabras que, solas, piden ayuda sobre el asistente.</summary>
    private static readonly HashSet<string> MetaSolas = new(StringComparer.Ordinal)
    {
        "ayuda", "help", "ayudame",
    };

    /// <summary>Clasifica el mensaje crudo del usuario.</summary>
    public static IntencionSocial Clasificar(string mensaje)
    {
        if (string.IsNullOrWhiteSpace(mensaje))
        {
            return IntencionSocial.Ninguna;
        }

        var palabras = NormalizadorLexico.Palabras(mensaje);
        if (palabras.Count == 0)
        {
            return IntencionSocial.Ninguna;
        }

        // La meta-pregunta va primero y por frase, no por ausencia de contenido:
        // «¿qué podés hacer?» tiene contenido, solo que el contenido es el
        // asistente y no el dominio.
        var normalizado = string.Join(' ', palabras);
        if (Meta.Any(frase => normalizado.Contains(frase, StringComparison.Ordinal)))
        {
            return IntencionSocial.Meta;
        }

        // Una sola palabra que pide ayuda va acá arriba y no después de la guarda
        // de contenido: «ayuda» ES contenido, así que la guarda la dejaría pasar.
        // Exigir que sea el mensaje entero es lo que separa «ayuda» de «necesito
        // ayuda con las designaciones de Álgebra».
        if (palabras.Count == 1 && MetaSolas.Contains(palabras[0]))
        {
            return IntencionSocial.Meta;
        }

        // LA GUARDA DE PRECISIÓN. Se quita la cortesía y se mira qué queda. Si
        // queda algo, era una pregunta con una apertura amable y sigue de largo.
        // Decidir por la presencia del saludo en vez de por la ausencia de
        // contenido rompería «hola, ¿cuántos docentes tiene Inglés Nivel IV?».
        var contenido = palabras.Where(palabra => !Cortesia.Contains(palabra)).ToArray();

        if (contenido.Length > 0)
        {
            return IntencionSocial.Ninguna;
        }

        // Sin contenido: es cortesía. El cierre gana sobre el saludo porque «hola,
        // gracias» cierra una conversación, no la abre.
        if (palabras.Any(Cierre.Contains))
        {
            return IntencionSocial.Agradecimiento;
        }

        return palabras.Any(Saludo.Contains) ? IntencionSocial.Saludo : IntencionSocial.Ninguna;
    }

    /// <summary>
    /// La respuesta fija de cada clase.
    /// </summary>
    /// <remarks>
    /// Fijas y no generadas. El small talk generativo está descartado: agrega una
    /// llamada, temperatura y alucinación en el turno donde no hay nada que
    /// averiguar, y vuelve no determinista el eje de evaluación que necesita poder
    /// afirmar que costó cero tokens.
    ///
    /// Ninguna promete nada que el sistema no haga. El catálogo de capacidades
    /// derivado de los privilegios del actor es de otra épica; hasta entonces la
    /// meta-respuesta describe el alcance en general y dice explícitamente que no
    /// modifica nada.
    /// </remarks>
    public static string Responder(IntencionSocial intencion) => intencion switch
    {
        IntencionSocial.Saludo =>
            "Hola. Puedo responder consultas sobre designaciones, docentes, materias, "
            + "pedidos y períodos. ¿Qué necesitás saber?",

        IntencionSocial.Agradecimiento =>
            "De nada. Cuando necesites consultar algo del sistema, escribime.",

        IntencionSocial.Meta =>
            "Respondo preguntas sobre lo que hay cargado en el sistema: designaciones, "
            + "docentes, materias, pedidos de designación y períodos. Solo consulto: "
            + "no modifico nada ni ejecuto acciones. Todo lo que ves está acotado a "
            + "tus permisos.",

        _ => throw new ArgumentOutOfRangeException(
            nameof(intencion), intencion, "No es una intención social."),
    };
}
