namespace Modules.Asistente.Application;

/// <summary>
/// Ejecuta la consulta generada contra una conexión de solo lectura, acotada al
/// actor del turno.
/// </summary>
public interface IEjecutorDeConsulta
{
    /// <summary>
    /// Ejecuta la consulta en una transacción nueva de solo lectura, con el actor
    /// fijado transaction-local.
    /// </summary>
    /// <param name="sql">La consulta generada, <b>ya validada</b>.</param>
    /// <param name="actor">
    /// Identificador de <c>identity.users</c> del usuario autenticado. Nunca el
    /// identificador del proveedor de identidad externo: los dos son UUID, así que
    /// confundirlos compila y ejecuta, y la diferencia aparece como un resultado
    /// vacío sobre una base llena.
    /// </param>
    /// <param name="conDatosPersonales">Cuál de las dos conexiones de lectura usar.</param>
    Task<ResultadoDeConsulta> EjecutarAsync(
        string sql, Guid actor, bool conDatosPersonales, CancellationToken ct);
}

/// <summary>
/// Lo que el carril necesita saber del actor antes de decidir nada.
/// </summary>
/// <param name="EsGlobal">Si ve todo el Departamento.</param>
/// <param name="VeDatosPersonales">
/// Si corresponde usar la conexión con acceso a las columnas personales.
/// </param>
public sealed record PerfilDelActor(
    bool EsGlobal, bool VeDatosPersonales, bool VeLaConsulta = false);

/// <summary>
/// Resuelve el alcance y el acceso a datos personales del actor.
/// </summary>
/// <remarks>
/// El alcance es lo que permite distinguir «no hay datos» de «no podés verlos».
/// RLS convierte la falta de permiso en cero filas, que es exactamente la misma
/// firma que un literal que no matcheó: mismo conteo, mismo tipo de resultado,
/// ninguna señal. Sin esta consulta, el carril gastaría el único reintento en un
/// caso donde ningún reintento puede ayudar, y la redacción diría «no hay» cuando
/// la verdad es «no podés verlo».
///
/// El acceso a datos personales exige alcance global <b>además</b> del permiso, y
/// no es redundancia: la política de la aplicación es la puerta, pero los
/// endpoints de docentes acotan los datos por separado, en el controller. Un
/// asistente que mirara solo la política heredaría la puerta y no el acotamiento,
/// y como <c>identity.personas</c> no tiene RLS, un jefe de cátedra podría leer
/// documento y teléfono de todo el padrón — algo que la interfaz le niega.
/// </remarks>
public interface IPerfilDelActor
{
    /// <summary>
    /// Resuelve el perfil del actor del turno.
    /// </summary>
    /// <remarks>
    /// Valida además el actor: la consulta invoca <c>identity.asistente_actor()</c>,
    /// que no resuelve si el UUID no corresponde a un usuario activo. Un
    /// identificador del directorio externo falla acá, de forma visible, en lugar
    /// de producir un turno que responde «no encontré nada» sobre una base llena.
    /// </remarks>
    /// <exception cref="ActorNoResuelto">Si el identificador no es un usuario activo.</exception>
    Task<PerfilDelActor> ObtenerAsync(Guid actor, CancellationToken ct);
}

/// <summary>
/// El identificador que se quiso usar como actor no corresponde a ningún usuario
/// activo del sistema.
/// </summary>
/// <remarks>
/// El caso típico es haber tomado el identificador del proveedor de identidad
/// externo en lugar del de <c>identity.users</c>. Los dos son UUID, así que la
/// confusión compila y ejecuta; sin esta excepción se manifestaría como un turno
/// que responde «no encontré nada» sobre una base llena, que es un error mucho
/// peor que uno que rompe.
/// </remarks>
public sealed class ActorNoResuelto(Guid actor, Exception? causa = null)
    : Exception(
        $"El identificador '{actor}' no corresponde a ningún usuario activo del sistema.",
        causa)
{
    /// <summary>El identificador que no resolvió.</summary>
    public Guid Actor { get; } = actor;
}
