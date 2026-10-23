namespace Modules.Asistente.Application;

/// <summary>
/// La línea con que el asistente se presenta, según el rol del actor (RF-04).
/// </summary>
/// <remarks>
/// <b>POR QUÉ ACÁ SE LEE EL ROL, CUANDO EL RESTO DEL MÓDULO LO EVITA A PROPÓSITO.</b>
/// La migración de las funciones del asistente y <c>useAccesoAlAsistente</c> del
/// frontend dicen los dos lo mismo y con razón: <c>identity.roles</c> NO es un
/// catálogo cerrado —Secretaría crea roles desde la aplicación— así que una lista
/// de roles embebida en el código FALLA ABIERTA, dejando pasar por default a
/// cualquier rol que no conozca. Esa regla protege la AUTORIZACIÓN.
///
/// Acá no se autoriza nada: el rol elige un texto de bienvenida y nada más. El modo
/// de falla es el opuesto y es inocuo — un rol que esta tabla no conoce cae a la
/// presentación genérica, que no promete nada que el asistente no haga. Ninguna
/// entrada de esta tabla puede influir en el alcance, en los permisos, en qué
/// conexión se usa ni en qué ejemplos se ofrecen: todo eso se sigue derivando de
/// los GRANT efectivos y de la matriz de permisos, en vivo.
///
/// <b>UN ACTOR CON VARIOS ROLES RECIBE LA GENÉRICA.</b> No hay tabla de precedencia
/// y no se inventa una: decidir que «secretaria gana a jefe_catedra» sería fabricar
/// una jerarquía que nadie pidió, con el único fin de elegir un saludo. Un texto
/// genérico correcto es mejor que uno específico adivinado. La regla entera es: un
/// solo rol vigente y conocido, su texto; cualquier otro caso, el genérico.
///
/// Los textos están todos en modo consulta —«Preguntá por…»— porque el asistente
/// solo consulta: ninguno puede sugerir que carga, aprueba o cambia nada.
/// </remarks>
internal static class PresentacionPorRol
{
    /// <summary>
    /// La presentación del actor: su ámbito, y las áreas que de verdad alcanza.
    /// </summary>
    /// <param name="perfil">
    /// El perfil resuelto del actor. Se pasa entero y no sus campos sueltos porque
    /// de él salen las TRES cosas que componen la frase: el rol da el ámbito, y los
    /// permisos deciden qué áreas se nombran.
    /// </param>
    /// <remarks>
    /// <b>LAS ÁREAS SE DERIVAN DE PERMISOS, NO DE UNA FRASE POR ROL.</b> La versión
    /// anterior tenía una oración fija por rol —«preguntá por cómo viene el trámite
    /// en todo el Departamento»— y eso decía menos de lo que el asistente hace: no
    /// nombraba las materias, los cargos ni el perfil profesional, así que quien
    /// leía la ayuda no se enteraba de que podía preguntar por ellos.
    ///
    /// Ahora la frase enumera lo que ESTE actor alcanza, y cada cláusula tiene su
    /// condición. El invariante #7 es el que manda: anunciar un área que el actor no
    /// puede consultar es prometer una capacidad que no va a poder ejercer — el
    /// mismo defecto que un botón que no anda, sin botón (BR-asistente-004).
    ///
    /// <b>NO SE NOMBRAN TAREAS NI AULAS, y no es un olvido.</b> El asistente tiene
    /// USAGE sobre <c>identity</c>, <c>designaciones</c> y <c>portal</c>, y sobre
    /// nada más: los schemas de esos dos módulos no se le conceden. Nombrarlos sería
    /// prometer respuestas que el motor va a rechazar. El día que se concedan, entra
    /// su cláusula acá y el manifiesto de privilegios es lo que lo habilita.
    /// </remarks>
    public static string Texto(PerfilDelActor perfil)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        var areas = new List<string>();

        // El trámite es lo que más se pregunta, así que va primero — y sólo si el
        // actor tiene el permiso de dominio: sin él la RLS le devuelve cero filas
        // sobre las cuatro tablas del trámite, y anunciarlo sería prometer vacío.
        //
        // Se mira `VeDesignaciones` y NO `AlcanzaDesignaciones`: el segundo conjuga
        // con el ámbito global, y con él un jefe de cátedra —que ve las de su
        // cátedra— se quedaría sin el anuncio de lo que sí consulta.
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
    /// Cómo se nombra el ámbito del actor dentro de la frase.
    /// </summary>
    /// <remarks>
    /// Es lo único que sigue saliendo del rol, y por el mismo motivo de siempre: el
    /// ámbito real lo impone la RLS y esto es sólo cómo se lo nombra. Un rol que la
    /// tabla no conoce cae a «del sistema», que no promete ningún ámbito — el
    /// alcance exacto lo dice <see cref="PoliticaDeAbstencion.TextoDeAlcance"/>, que
    /// sí se deriva de la base.
    /// </remarks>
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
