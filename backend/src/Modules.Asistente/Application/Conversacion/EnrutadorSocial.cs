namespace Modules.Asistente.Application;

/// <summary>En cuál de las tres clases sociales cae un mensaje, si es que cae.</summary>
public enum IntencionSocial
{
    /// <summary>No es social: sigue al carril de datos.</summary>
    Ninguna,

    /// <summary>Un saludo, sin nada más.</summary>
    Saludo,

    /// <summary>Un agradecimiento o un cierre de conversación, sin nada más.</summary>
    Agradecimiento,

    /// <summary>Una pregunta sobre el asistente mismo, no sobre los datos.</summary>
    Meta,
}

/// <summary>
/// Carril sin datos: resuelve saludo, agradecimiento y meta-pregunta a costo cero de tokens.
/// </summary>
internal static class EnrutadorSocial
{
    /// <summary>
    /// Palabras que forman una apertura o un cierre de cortesía y no aportan contenido.
    /// </summary>
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
        // Palabras función, que no son cortesía ni contenido. Están en este mismo bucket porque
        // cumplen el mismo papel: se sacan antes de mirar qué quedó.
        "lo", "los", "las", "en", "y", "o", "con", "al", "del",
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

    /// <summary>Palabras con las que se habla <b>del asistente</b> y no del dominio.</summary>
    private static readonly HashSet<string> Meta = new(StringComparer.Ordinal)
    {
        // Segunda persona, en tuteo y en voseo: de quién se habla.
        "podes", "puedes", "podrias", "podras", "sabes", "sabras",
        "servis", "sirves", "funcionas", "funciona", "sos", "eres", "estas",
        "ayudas", "ayudar", "ayudame", "ayudas", "hacer", "hace", "haces",
        "hacerte", "hacerlo", "dar", "das", "darme", "decir", "decime",
        "preguntar", "preguntarte", "responder", "respondes", "contestar",
        "usar", "uso", "usa", "usarte", "realizar", "realizas", "ofreces",
        "ofrecer", "brindas", "brindar", "manejas", "manejar", "conoces",
        // Pronombres y referencias al asistente o a quien pregunta.
        "te", "vos", "tu", "ti", "me", "mi", "conmigo", "vos", "usted",
        "yo", "puedo", "podria", "quiero", "quien", "para", "con",
        // Sustantivos de capacidad, nunca del dominio.
        "ayuda", "help", "cosas", "tipo", "tipos", "clase", "informacion",
        "info", "datos", "preguntas", "consultas", "temas", "funciones",
        "utilidad", "capaz", "posible", "sirve", "servir",
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

        // LA GUARDA DE PRECISIÓN, una sola y compartida. Se saca la cortesía y se mira qué queda;
        // las tres clases se deciden sobre ese resto.
        var contenido = palabras.Where(palabra => !Cortesia.Contains(palabra)).ToArray();

        // META: queda algo, y ese algo habla del asistente y no del dominio. El «queda algo» no es
        // una formalidad: sin él, un «hola» —que se vacía entero— cumpliría la condición por
        // vacuidad y todo saludo sería una meta-pregunta.
        if (contenido.Length > 0 && Array.TrueForAll(contenido, Meta.Contains))
        {
            return IntencionSocial.Meta;
        }

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

    /// <summary>La respuesta fija de cada clase.</summary>
    public static string Responder(IntencionSocial intencion) => intencion switch
    {
        IntencionSocial.Saludo =>
            "Hola. Puedo responder consultas sobre designaciones, docentes, materias, "
            + "pedidos y períodos. ¿Qué necesitás saber?",

        IntencionSocial.Agradecimiento =>
            "De nada. Cuando necesites consultar algo del sistema, escribime.",

        IntencionSocial.Meta => throw new ArgumentOutOfRangeException(
            nameof(intencion),
            intencion,
            "La meta-pregunta no tiene texto fijo: la responde el catálogo de "
            + "capacidades, derivado de los privilegios efectivos del actor. Un texto "
            + "escrito acá sería una promesa sobre capacidades que nadie verifica.'"),

        _ => throw new ArgumentOutOfRangeException(
            nameof(intencion), intencion, "No es una intención social."),
    };
}
