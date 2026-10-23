using Microsoft.Extensions.Logging;

namespace Modules.Asistente.Application;

/// <summary>
/// Lo último que le pasa al resultado de un turno antes de salir: el texto con
/// que se rechaza y todo lo que lo acompaña (vínculos, posiciones en el texto,
/// elemento principal y áreas de datos).
/// </summary>
/// <remarks>
/// Sale de <see cref="CapaConversacional"/> porque no es orquestación: no decide
/// por dónde va el turno, adorna el que ya terminó. Son estáticas y reciben lo que
/// necesitan, así que se prueban sin armar la capa entera.
/// </remarks>
internal static class CierreDelTurno
{
    /// <summary>
    /// El resultado con todo lo que se le agrega al salir, calculado sobre el texto
    /// FINAL.
    /// </summary>
    /// <remarks>
    /// <b>El orden es el contrato:</b> primero el texto (que puede reescribirse), después
    /// los vínculos de las celdas, y recién entonces lo que depende de las dos cosas —las
    /// posiciones se miden sobre el texto que el usuario lee, y el principal sobre los
    /// vínculos—. Cambiar el orden calcularía posiciones sobre un texto que nunca se
    /// muestra.
    /// </remarks>
    public static async Task<ResultadoDelTurno> CompletarAsync(
        ResultadoDelTurno resultado,
        string texto,
        IResolutorDeVinculos resolutor,
        IProveedorDeFichas fichas,
        ILogger log,
        CancellationToken ct)
    {
        var vinculos = await VinculosAsync(resultado, resolutor, log, ct);

        return resultado with
        {
            Respuesta = texto,
            Vinculos = vinculos,
            VinculosDelTexto = UbicadorEnElTexto.Ubicar(texto, resultado.Filas, vinculos),
            Principal = await PrincipalAsync(resultado, vinculos, fichas, log, ct),
            Origenes = resultado.Origenes ?? OrigenesDeLosDatos.De(resultado.SqlEjecutado),
        };
    }

    /// <summary>
    /// La ficha del elemento principal, o ninguna.
    /// </summary>
    /// <remarks>
    /// <b>La regla es determinista y no depende del modelo:</b> hay principal cuando el
    /// resultado tiene UNA fila y esa fila tiene UN vínculo. Con varias filas no hay un
    /// «el elemento», y con varios vínculos en una fila tampoco.
    ///
    /// <b>Un fallo acá no tumba el turno</b>, por el mismo motivo que los vínculos: la
    /// ficha es una presentación sobre la respuesta, no la respuesta.
    /// </remarks>
    public static async Task<FichaDelElemento?> PrincipalAsync(
        ResultadoDelTurno resultado,
        IReadOnlyList<VinculoDelResultado> vinculos,
        IProveedorDeFichas fichas,
        ILogger log,
        CancellationToken ct)
    {
        if (resultado.Filas.Count != 1 || vinculos.Count != 1)
        {
            return null;
        }

        var vinculo = vinculos[0];

        try
        {
            return await fichas.ObtenerAsync(vinculo.Tipo, vinculo.Id, ct);
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            log.LogWarning(
                excepcion,
                "No se pudo obtener la ficha del elemento principal ({Tipo}); se responde sin ella.",
                vinculo.Tipo);
            return null;
        }
    }

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
