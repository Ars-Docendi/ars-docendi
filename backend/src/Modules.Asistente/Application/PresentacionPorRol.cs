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
public static class PresentacionPorRol
{
    /// <summary>
    /// La presentación de quien no cae en ninguna entrada de la tabla.
    /// </summary>
    /// <remarks>
    /// Nombra las tres áreas del dominio que cualquier rol con acceso al asistente
    /// puede consultar, sin prometer ningún ámbito: el alcance lo dice
    /// <see cref="PoliticaDeAbstencion.TextoDeAlcance"/>, que sí se deriva de la base.
    /// </remarks>
    public const string Generica =
        "Preguntá por las designaciones, los pedidos y los períodos del sistema.";

    /// <summary>
    /// Los códigos de rol que el sistema siembra, con su presentación.
    /// </summary>
    /// <remarks>
    /// Son los códigos de <c>identity.roles</c>, que <c>es_sistema</c> protege de
    /// renombres. Un rol creado por Secretaría no está acá y cae al genérico, que
    /// es el comportamiento correcto y no un agujero: ver las notas de la clase.
    /// </remarks>
    private static readonly Dictionary<string, string> PorCodigo = new(StringComparer.Ordinal)
    {
        ["jefe_catedra"] =
            "Preguntá por las designaciones y los pedidos de tu cátedra: quién está designado, "
            + "en qué materia y en qué estado quedó cada trámite.",
        ["coordinador_carrera"] =
            "Preguntá por los pedidos de tu carrera: qué hay pendiente de revisión, en qué "
            + "estado está cada trámite y quién quedó designado.",
        ["secretaria"] =
            "Preguntá por cualquier cátedra del Departamento: designaciones, pedidos, períodos "
            + "y cómo viene el trámite en cada carrera.",
        ["decanato"] =
            "Preguntá por cómo viene el trámite en todo el Departamento: qué llegó a la "
            + "aprobación final, qué quedó pendiente y quién está designado.",
        ["administrativo"] =
            "Preguntá por los datos del trámite y los catálogos del sistema: períodos, cargos, "
            + "materias y en qué estado está cada pedido.",
        ["docente"] =
            "Preguntá por tus designaciones: en qué materias estás designado, con qué cargo y "
            + "desde cuándo.",
    };

    /// <summary>
    /// La presentación del actor, o la genérica si su rol no está en la tabla.
    /// </summary>
    /// <param name="codigoDeRol">
    /// El código del único rol vigente del actor. <c>null</c> cuando no tiene
    /// ninguno o tiene más de uno: los dos casos son el genérico.
    /// </param>
    public static string Texto(string? codigoDeRol) =>
        codigoDeRol is not null && PorCodigo.TryGetValue(codigoDeRol, out var texto)
            ? texto
            : Generica;
}
