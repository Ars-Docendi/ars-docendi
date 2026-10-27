using Microsoft.Extensions.Logging;

namespace Modules.Asistente.Application;

/// <summary>
/// Lo último que le pasa al resultado de un turno antes de salir: el texto con que se rechaza y
/// todo lo que lo acompaña (vínculos, posiciones en el texto, elemento principal y áreas de datos).
/// </summary>
internal static class CierreDelTurno
{
    /// <summary>
    /// El resultado con todo lo que se le agrega al salir, calculado sobre el texto FINAL.
    /// </summary>
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

    /// <summary>La ficha del elemento principal, o ninguna.</summary>
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

    /// <summary>Los vínculos del resultado, o ninguno.</summary>
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
