namespace Modules.Asistente.Application;

/// <summary>Los siete casos de abstención (RF-17, §3.3 de la definición).</summary>
internal static class PoliticaDeAbstencion
{
    private static readonly TimeZoneInfo ZonaDeLaInstitucion =
        TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    /// <summary>
    /// Si para este actor, <b>en esta consulta</b>, cero filas significa que no hay filas.
    /// </summary>
    public static bool AlcanzaTodo(PerfilDelActor perfil, bool laConsultaTocaPortal)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        return perfil.AlcanzaDesignaciones
            && (!laConsultaTocaPortal || perfil.VeTrayectoriaAjena);
    }

    /// <summary>Si un resultado vacío justifica gastar el reintento de generación.</summary>
    public static bool ConvieneReintentar(ResultadoDeConsulta resultado, bool alcanzaTodo)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        return resultado.EstaVacio && alcanzaTodo;
    }

    /// <summary>Texto de un resultado vacío.</summary>
    public static string TextoDeResultadoVacio(
        bool alcanzaTodo, CoberturaDeUnDato? cobertura = null)
    {
        var texto = alcanzaTodo
            ? "No encontré ningún registro que responda esa pregunta."
            : "No encontré nada dentro de lo que podés consultar. Puede que el dato exista "
              + "y esté fuera de tu alcance; en ese caso vas a necesitar pedírselo a quien "
              + "tenga acceso al ámbito correspondiente.";

        // LA SEGUNDA RAZÓN POR LA QUE UN RESULTADO PUEDE VENIR VACÍO, y hasta acá el sistema sólo
        // contaba la primera.
        return cobertura is null ? texto : $"{texto} {cobertura.Frase()}";
    }

    /// <summary>
    /// Aclaración que acompaña al razonamiento de un turno que no devolvió filas.
    /// </summary>
    public const string AclaracionDeRazonamientoSinFilas =
        "Eso es lo que busqué, no lo que encontré: la búsqueda no devolvió ningún resultado.";

    /// <summary>El razonamiento de un turno que terminó sin filas.</summary>
    public static string RazonamientoDeResultadoVacio(string razonamiento) =>
        // Sin razonamiento no hay nada que aclarar, y una aclaración suelta sería
        // ruido: el usuario ya leyó que no se encontró nada dos renglones arriba.
        string.IsNullOrWhiteSpace(razonamiento)
            ? razonamiento
            : $"{razonamiento.TrimEnd()} {AclaracionDeRazonamientoSinFilas}";

    /// <summary>Demostrativos que dejan una referencia sin resolver.</summary>
    internal static readonly HashSet<string> Demostrativos = new(StringComparer.Ordinal)
    {
        "eso", "esa", "ese", "esos", "esas", "esto", "estos", "estas",
        "aquel", "aquella", "aquellos", "aquellas",
        "dicho", "dicha", "dichos", "dichas",
        "mismo", "misma", "mismos", "mismas",
        "anterior", "anteriores", "ultimo", "ultima", "ultimos", "ultimas",
    };

    /// <summary>Si la pregunta todavía apunta a algo que no nombra.</summary>
    public static bool HayReferenciaSinResolver(string pregunta) =>
        !string.IsNullOrWhiteSpace(pregunta)
        && NormalizadorLexico.Palabras(pregunta).Any(Demostrativos.Contains);

    /// <summary>Texto de un seguimiento cuya referencia no se pudo resolver.</summary>
    public const string TextoReferenciaSinResolver =
        "No pude entender a qué te referís. Nombralo y vuelvo a intentar: "
        + "por ejemplo, en lugar de «esa materia», la materia por su nombre.";

    /// <summary>Texto de una pregunta que el esquema no cubre (caso 1).</summary>
    public const string TextoNoContestable =
        "No puedo responder eso con la información que tengo disponible.";

    /// <summary>Texto de una consulta rechazada por el validador (caso 5).</summary>
    public const string TextoRechazadaPorValidador =
        "No pude armar una consulta segura para esa pregunta. Probá formulándola de otra manera.";

    /// <summary>Texto de una lectura que el motor rechazó por falta de privilegio.</summary>
    public const string TextoSinAccesoALosDatos =
        "No tenés acceso a esa información con tus permisos actuales.";

    /// <summary>Texto de un rechazo del motor que no es de permisos.</summary>
    public const string TextoErrorAlConsultar =
        "No pude completar esa consulta. Probá formulándola de otra manera.";

    /// <summary>Texto de proveedor caído (caso 6).</summary>
    public const string TextoServicioDegradado =
        "El asistente no está disponible en este momento. Volvé a intentar más tarde.";

    /// <summary>
    /// Texto de un turno que no consiguió lugar para llamar al modelo (asistente-proveedor-local,
    /// design.md D4).
    /// </summary>
    public const string TextoProveedorSaturado =
        "El asistente está atendiendo muchas consultas en este momento. Probá de nuevo en unos segundos.";

    /// <summary>Texto de cuota agotada (caso 6, la otra mitad).</summary>
    public static string TextoCuotaAgotada(DateTimeOffset? vuelveA) => vuelveA is { } cuando
        ? "Alcanzaste tu límite de consultas por ahora. Volvés a tener disponibles a las "
            + $"{TimeZoneInfo.ConvertTime(cuando, ZonaDeLaInstitucion):HH:mm}."
        : "Alcanzaste tu límite de consultas por ahora. Probá de nuevo más tarde.";

    /// <summary>
    /// Texto de tope organizacional agotado (design.md D1/D3 de asistente-administracion-de-uso).
    /// </summary>
    public const string TextoTopeOrganizacionalAgotado =
        "El asistente alcanzó el límite de uso de la organización para este mes. "
        + "Va a estar disponible de nuevo el próximo mes.";

    /// <summary>
    /// Texto de un actor sin acceso operativo al asistente (asistente-acceso-granular).
    /// </summary>
    public const string TextoSinAcceso =
        "No tenés acceso al asistente en este momento. Si lo necesitás, pedíselo al área que lo administra.";

    /// <summary>
    /// Texto de un turno rechazado por ya tener otro en curso (asistente-turno-exclusivo-del-actor,
    /// design.md D5).
    /// </summary>
    public const string TextoTurnoConcurrente =
        "Ya tenés una consulta en curso. Esperá a que termine antes de mandar otra.";

    /// <summary>
    /// Texto de modo mantenimiento activo (asistente-modo-mantenimiento, design.md D7/D8).
    /// </summary>
    public static string TextoMantenimiento(string? razon) => string.IsNullOrWhiteSpace(razon)
        ? "El asistente está en mantenimiento. Volvé a intentar más tarde."
        : $"El asistente está en mantenimiento: {razon}";

    /// <summary>Los límites del asistente, tal como se los cuenta al usuario (RF-04).</summary>
    public static readonly IReadOnlyList<string> LimitesDelAsistente =
    [
        "No modifica nada: solo consulta.",
        "Solo lee este sistema: no Guaraní, ni planillas, ni otras fuentes.",
        "Solo ve lo que tu rol ya puede ver: si algo no aparece, puede existir fuera de tu alcance.",
        "No inventa: si no puede responder, lo dice.",
    ];

    /// <summary>Cómo se le describe al actor el alcance de lo que ve.</summary>
    public static string TextoDeAlcance(bool esGlobal) => esGlobal
        ? "Ves los datos de todo el Departamento."
        : "Ves los datos de tu ámbito: las materias y carreras que tenés asignadas.";

    /// <summary>Reglas que se agregan al prompt de redacción según el caso del turno.</summary>
    public static IReadOnlyList<string> ReglasDeRedaccion(
        bool alcanzaTodo,
        bool truncado,
        IReadOnlyList<CoberturaDeUnDato>? cobertura = null)
    {
        var reglas = new List<string>();

        foreach (var declarada in cobertura ?? [])
        {
            // El dato del portal es AUTODECLARADO y opcional. Sin esta regla, el modelo narra las
            // filas que ve como si fueran el Departamento entero: «hay tres docentes con doctorado»
            // cuando lo cierto es «tres de los catorce que cargaron su formación».
            reglas.Add(
                $"{declarada.Frase()} Es un dato que cada docente carga sobre sí mismo y "
                + "la mayoría no lo hizo. NO presentes lo que ves como el total del "
                + "Departamento, y NO afirmes que nadie cumple una condición: decí "
                + "sobre cuántos se sabe.");
        }

        if (!alcanzaTodo)
        {
            reglas.Add(
                "El usuario ve solo una parte del Departamento. NO afirmes que algo no existe "
                + "ni que no hay más casos: lo que estás viendo es lo que él puede ver, no todo "
                + "lo que hay. Encuadrá la respuesta en su alcance.");
        }

        if (truncado)
        {
            reglas.Add(
                "El resultado se recortó. NO afirmes ningún total ni ningún conteo: "
                + "decí que hay más y mostrá lo que llegó. NUNCA digas cuántos quedaron afuera.");
        }

        return reglas;
    }
}
