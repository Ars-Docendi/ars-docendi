namespace Modules.Asistente.Application;

/// <summary>Un turno tal como lo necesita el historial persistido.</summary>
/// <remarks>
/// No incluye las filas devueltas ni el texto redactado de la respuesta: no se
/// persisten en ningún lado (asistente-historial-conversaciones), y no
/// tenerlas acá hace que no se puedan persistir por accidente. Tampoco incluye
/// <see cref="ResultadoDelTurno.ClaveDeRetroalimentacion"/>: el historial no
/// referencia ese token a propósito, ver design.md D8.
/// </remarks>
/// <param name="Actor">Quién preguntó. Sólo se usa al MINTAR una conversación nueva.</param>
/// <param name="Pregunta">La pregunta interpretada, autocontenida.</param>
/// <param name="SqlResuelto">
/// La consulta que respondió (<c>ResultadoDelTurno.SqlEjecutado</c>), o
/// <c>null</c> si el turno no llegó a devolver filas.
/// </param>
/// <param name="Estado">
/// Uno de los cuatro estados que el contrato HTTP expone. <b>Nunca</b>
/// <see cref="EstadoDelTurno.Fallo"/> — ese estado no llega hasta acá: el
/// llamador lo excluye antes de invocar este método (design.md D2).
/// </param>
/// <param name="OcurrioEn">Cuándo se resolvió el turno.</param>
/// <param name="Referencias">
/// Un diccionario marcador → (tipo, id) de las menciones de este turno, o
/// <c>null</c> si no declaró ninguna. Se persiste en
/// <c>asistente.turno_historico.referencias</c>. **No implica que <paramref
/// name="SqlResuelto"/> las use**: cuando el carril SQL corrió, son
/// exactamente los marcadores que la consulta liga —para que «Volver a
/// consultar» y «Reanudar» puedan volver a bindearla—; cuando el turno nunca
/// entró al carril SQL (rechazo, aclaración, degradación, social/meta), son
/// las menciones que el pedido declaró y el controller ya revalidó, numeradas
/// igual pero sin ninguna consulta que las use (decisión 15 del PO,
/// 2026-09-26, design.md D11 de asistente-rediseno-v3): existen para que
/// <c>GET /historial/{id}</c> arme sus chips, no para re-ejecutar nada — «Volver
/// a consultar» sigue exigiendo <see cref="SqlResuelto"/> antes de mirar acá.
/// </param>
public sealed record TurnoParaHistorial(
    Guid Actor,
    string Pregunta,
    string? SqlResuelto,
    EstadoDelTurno Estado,
    DateTimeOffset OcurrioEn,
    IReadOnlyDictionary<string, (TipoDeMencion Tipo, Guid Id)>? Referencias = null);

/// <summary>
/// Persiste un turno al historial propio del actor (asistente-historial-conversaciones).
/// </summary>
/// <remarks>
/// <b>Nunca hace fallar el turno</b>, igual que <see cref="IRegistroDelTurno"/>:
/// un registro que rompe el turno que estaba registrando convierte la
/// observabilidad —o, acá, el self-service del usuario— en una fuente de
/// indisponibilidad.
///
/// Mintea la conversación persistida la primera vez que <paramref name="conversacion"/>
/// (el parámetro del método, ver abajo) escribe una fila, y fija
/// <see cref="HiloConversacional.HiloHistorico"/> con el id resultante; los
/// turnos siguientes del mismo hilo efímero reusan esa misma conversación en
/// vez de abrir una nueva (design.md D1).
/// </remarks>
public interface IRegistroDeHistorial
{
    /// <summary>Registra un turno. No propaga errores.</summary>
    /// <param name="conversacion">
    /// El hilo efímero del turno. Se lee y, la primera vez, se le fija
    /// <see cref="HiloConversacional.HiloHistorico"/>.
    /// </param>
    /// <param name="turno">Lo que hay que persistir de este turno.</param>
    Task RegistrarTurnoAsync(
        HiloConversacional conversacion, TurnoParaHistorial turno, CancellationToken ct);

    /// <summary>
    /// Reemplaza, en una sola transacción, la última fila de
    /// <c>asistente.turno_historico</c> de esta conversación por <paramref
    /// name="turno"/> — para «Editar y reenviar» (asistente-edicion-de-la-ultima-pregunta,
    /// design.md D9 de asistente-rediseno-v3). El historial conserva sólo la
    /// versión final: no queda ninguna fila de la pregunta reemplazada.
    /// </summary>
    /// <remarks>
    /// Igual que <see cref="RegistrarTurnoAsync"/>: <b>nunca hace fallar el
    /// turno</b>. Una fila que no se pudo borrar o insertar deja la conversación
    /// exactamente como estaba — nunca a mitad de camino —, porque las tres
    /// operaciones corren en una única transacción.
    /// </remarks>
    /// <param name="conversacion">
    /// El hilo efímero del turno. Si todavía no tiene
    /// <see cref="HiloConversacional.HiloHistorico"/> fijado —la fila vieja
    /// nunca llegó a persistirse—, se comporta como
    /// <see cref="RegistrarTurnoAsync"/>: mintea la conversación y agrega la
    /// fila nueva.
    /// </param>
    /// <param name="turno">Lo que hay que persistir de la versión nueva.</param>
    Task ReemplazarUltimoTurnoAsync(
        HiloConversacional conversacion, TurnoParaHistorial turno, CancellationToken ct);
}
