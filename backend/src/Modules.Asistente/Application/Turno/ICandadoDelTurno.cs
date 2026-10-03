namespace Modules.Asistente.Application;

/// <summary>
/// Un turno en curso por actor (asistente-turno-exclusivo-del-actor,
/// design.md D5 de asistente-administracion-de-uso).
/// </summary>
public interface ICandadoDelTurno
{
    /// <summary>
    /// Intenta tomar el candado del actor. Devuelve <c>null</c> si ya hay un
    /// turno en curso para ese actor — nunca lanza por eso.
    /// </summary>
    /// <remarks>
    /// El disponible se libera disponiendo el <see cref="IAsyncDisposable"/>
    /// devuelto — no hay un método <c>Liberar</c> separado, para que sea
    /// imposible tomarlo y olvidarse de liberarlo sin que el compilador lo
    /// marque con un <c>using</c> faltante.
    /// </remarks>
    Task<IAsyncDisposable?> IntentarAsync(Guid actor, CancellationToken ct);
}
