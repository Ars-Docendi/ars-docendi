using Microsoft.Extensions.Logging;

namespace Modules.Asistente.Application;

/// <summary>
/// Lo último que le pasa al resultado de un turno antes de salir: el texto con
/// que se rechaza y los vínculos que lo acompañan.
/// </summary>
/// <remarks>
/// Sale de <see cref="CapaConversacional"/> porque no es orquestación: no decide
/// por dónde va el turno, adorna el que ya terminó. Las dos son estáticas y
/// reciben lo que necesitan, así que se prueban sin armar la capa entera.
/// </remarks>
internal static class CierreDelTurno
{
    /// <summary>
    /// Los vínculos del resultado, o ninguno.
    /// </summary>
    /// <remarks>
    /// <b>Va en esta capa y no en el carril.</b> El carril responde una pregunta con
    /// datos; ofrecer una pantalla donde seguir es de la superficie de conversación,
    /// y el evaluador —que corre el carril sin interfaz— no tiene qué hacer con
    /// ellos.
    ///
    /// <b>Sin filas no se le pregunta nada a nadie.</b> Es el caso mayoritario
    /// —abstenciones, saludos, aclaraciones— y no tiene por qué pagar una consulta.
    ///
    /// <b>Un fallo acá no puede tumbar el turno</b>, por el mismo motivo que la
    /// cobertura de portal: el vínculo es un atajo sobre la respuesta, no la
    /// respuesta. Responder sin el atajo es peor que tenerlo y muchísimo mejor que
    /// un error sobre una pregunta que se contestó bien.
    /// </remarks>
    public static async Task<IReadOnlyList<VinculoDelResultado>> VinculosAsync(
        ResultadoDelTurno resultado,
        IResolutorDeVinculos vinculos,
        ILogger log,
        CancellationToken ct)
    {
        if (resultado.Filas.Count == 0)
        {
            return [];
        }

        var candidatos = BuscadorDeVinculos.Candidatos(resultado.Filas);

        if (candidatos.Count == 0)
        {
            return [];
        }

        try
        {
            var destinos = await vinculos.ResolverAsync(candidatos, ct);
            return BuscadorDeVinculos.Ubicar(resultado.Filas, destinos);
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            log.LogWarning(
                excepcion, "No se pudieron resolver los vínculos del turno; se responde sin ellos.");
            return [];
        }
    }

    /// <summary>
    /// El texto de un rechazo, especializado cuando el seguimiento no se resolvió.
    /// </summary>
    /// <remarks>
    /// <b>Las TRES condiciones hacen falta, y ninguna sola alcanza.</b>
    ///
    /// Hubo <b>historial</b>, así que el turno era un seguimiento y no una pregunta
    /// suelta. La pregunta interpretada <b>todavía apunta a algo que no nombra</b>,
    /// o sea que el reescritor no cumplió lo que promete. Y el turno <b>terminó en
    /// rechazo</b>: mientras el generador pueda contestar —y con el arrastre de la
    /// consulta anterior muchas veces puede—, no hay nada que explicar.
    ///
    /// Sin la tercera, el asistente le echaría la culpa a la referencia cada vez que
    /// una pregunta con demostrativo se rechaza por estar fuera del esquema. Eso es
    /// exactamente la clase de explicación falsa que el resto de esta política
    /// existe para evitar: suena informada y manda a corregir lo que no estaba mal.
    /// </remarks>
    public static string TextoDelRechazo(
        ResultadoDelTurno resultado,
        IReadOnlyList<TurnoDelHilo> historial,
        string interpretada) =>
        resultado.Estado == EstadoDelTurno.NoContestable
        && historial.Count > 0
        && PoliticaDeAbstencion.HayReferenciaSinResolver(interpretada)
            ? PoliticaDeAbstencion.TextoReferenciaSinResolver
            : resultado.Respuesta;
}
