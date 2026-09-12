using System.Globalization;

namespace Modules.Asistente.Application;

/// <summary>
/// Cuánta gente cargó un dato del portal, para no confundir el vacío con el hecho.
/// </summary>
/// <param name="Tabla">Tabla de portal, sin esquema.</param>
/// <param name="ConDato">Cuántas personas tienen al menos una fila ahí.</param>
/// <param name="Total">Cuántas personas hay en el padrón.</param>
public sealed record CoberturaDeUnDato(string Tabla, long ConDato, long Total)
{
    /// <summary>Cómo se le cuenta al usuario, en una oración.</summary>
    public string Frase() => string.Create(
        CultureInfo.InvariantCulture,
        $"De {Total} docentes del padrón, {ConDato} cargaron {CoberturaDelPortal.ComoSeLlama(Tabla)}.");
}

/// <summary>
/// Qué tablas del portal tocó una consulta, y cómo se declara su cobertura.
/// </summary>
/// <remarks>
/// <b>EL MODO DE FALLA QUE ESTA PIEZA EXISTE PARA EVITAR.</b> El portal se llena
/// solo si los docentes lo llenan, y el design spec del módulo dice que «el problema
/// del Departamento es que los docentes no cargan nada». Entonces estas dos son la
/// MISMA CONSULTA y significan lo contrario:
///
/// <list type="bullet">
///   <item>«Ningún docente sabe Python»</item>
///   <item>«Nadie cargó sus habilidades»</item>
/// </list>
///
/// Sin el denominador, la segunda se dice como la primera, y eso es una afirmación
/// falsa sobre personas reales. No es un problema de privacidad ni de permisos: es
/// el sistema afirmando más de lo que sabe, que es exactamente lo que la métrica
/// primaria del proyecto —corrección con abstención— existe para impedir.
///
/// <b>Es distinto del límite de alcance.</b> <see cref="PerfilDelActor.AlcanzaTodo"/>
/// cubre «hay filas que no ves»; esto cubre «nadie las cargó». Un resultado vacío
/// puede ser por cualquiera de las dos, y quien pregunta necesita saber por cuál.
/// </remarks>
internal static class CoberturaDelPortal
{
    /// <summary>Las tablas de portal cuya ausencia de datos hay que declarar.</summary>
    /// <remarks>
    /// Sólo las que el docente carga. <c>perfiles</c> no está porque su cobertura es
    /// la de tener perfil, no la de tener un dato, y eso no explica un resultado
    /// vacío de una pregunta concreta.
    /// </remarks>
    private static readonly Dictionary<string, string> Nombres = new(StringComparer.Ordinal)
    {
        ["educaciones"] = "su formación",
        ["certificaciones"] = "sus certificaciones",
        ["experiencias"] = "su experiencia laboral",
        ["docente_habilidades"] = "sus habilidades e intereses",
    };

    /// <summary>Si la tabla es una de las que se declaran.</summary>
    /// <remarks>
    /// La usa quien interpola el nombre en una consulta: un identificador no se
    /// puede parametrizar, así que la única defensa es que salga de acá.
    /// </remarks>
    public static bool EsDeclarable(string tabla) => Nombres.ContainsKey(tabla);

    /// <summary>Cómo se nombra el dato de una tabla al hablarle a una persona.</summary>
    internal static string ComoSeLlama(string tabla) =>
        Nombres.TryGetValue(tabla, out var nombre) ? nombre : "ese dato";

    /// <summary>
    /// Las tablas de portal que la consulta toca.
    /// </summary>
    /// <remarks>
    /// <b>Se detecta sobre el texto de la consulta, y eso alcanza acá por una razón
    /// del motor y no por confianza.</b> El <c>search_path</c> del rol de lectura no
    /// incluye <c>portal</c>, así que una consulta que llegue a esas tablas SIN
    /// escribir el esquema no se ejecuta: falla con «relation does not exist» antes
    /// de devolver una fila. Toda consulta que efectivamente leyó portal lo nombró.
    ///
    /// Y el error posible es el inofensivo: nombrar la tabla en un comentario o en un
    /// literal declararía una cobertura de más —contexto sobrante— y nunca de menos,
    /// que es la dirección en la que este mecanismo tiene que fallar.
    /// </remarks>
    public static IReadOnlyList<string> TablasQueToca(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return [];
        }

        return [.. Nombres.Keys
            .Where(tabla => sql.Contains($"portal.{tabla}", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// La cobertura que se le declara al usuario cuando la respuesta salió vacía.
    /// </summary>
    /// <remarks>
    /// Se elige la MÁS ESCASA y no se enumeran todas: es la que explica el vacío. Si
    /// la pregunta cruzó formación con certificaciones y nadie cargó certificaciones,
    /// decir además cuánta formación hay no aclara nada y alarga una respuesta que ya
    /// es una mala noticia.
    ///
    /// Devuelve nulo cuando no hay nada que declarar —la consulta no tocó portal, o
    /// el padrón está vacío y el cociente no significa nada—.
    /// </remarks>
    public static CoberturaDeUnDato? LaQueExplicaElVacio(
        IReadOnlyList<CoberturaDeUnDato> coberturas)
    {
        ArgumentNullException.ThrowIfNull(coberturas);

        return coberturas
            .Where(cobertura => cobertura.Total > 0)
            .OrderBy(cobertura => cobertura.ConDato)
            .ThenBy(cobertura => cobertura.Tabla, StringComparer.Ordinal)
            .FirstOrDefault();
    }
}

/// <summary>Cuenta cuánta gente cargó cada dato del portal.</summary>
/// <remarks>
/// Corre con la conexión de lectura del actor, así que la RLS se aplica: el
/// numerador cuenta perfiles que ESE actor alcanza. Es lo correcto — declararle una
/// cobertura calculada sobre filas que no puede ver le diría cuánta gente hay
/// detrás de una puerta cerrada.
/// </remarks>
public interface IConsultorDeCobertura
{
    /// <summary>La cobertura de cada tabla pedida, para el actor del turno.</summary>
    Task<IReadOnlyList<CoberturaDeUnDato>> ObtenerAsync(
        IReadOnlyList<string> tablas, Guid actor, CancellationToken ct);
}
