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
public sealed record TurnoParaHistorial(
    Guid Actor,
    string Pregunta,
    string? SqlResuelto,
    EstadoDelTurno Estado,
    DateTimeOffset OcurrioEn);

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
}
