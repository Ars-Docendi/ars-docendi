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

        // Todo lo que se calcula sobre el resultado ORIGINAL va antes de quitar las claves: las
        // posiciones en el texto (un apellido repetido no debe enlazar a la persona equivocada,
        // y un uuid nunca aparece en la prosa) y el elemento principal.
        var enElTexto = UbicadorEnElTexto.Ubicar(texto, resultado.Filas, vinculos);
        var principal = await PrincipalAsync(resultado, vinculos, fichas, log, ct);
        var sinClaves = ClavesInternas.Quitar(resultado, vinculos);

        return sinClaves.Resultado with
        {
            Respuesta = texto,
            Vinculos = sinClaves.Vinculos,
            VinculosDelTexto = enElTexto,
            Principal = principal,
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
        // Un elemento, o una fila con un vínculo, o varias filas de la MISMA clave interna (un
        // docente con dos designaciones): ahí la tabla sigue montada, así que nada se pierde.
        var mismoElemento = vinculos.Count > 0
            && vinculos.All(v => v.EsClave)
            && vinculos.Select(v => (v.Tipo, v.Id)).Distinct().Count() == 1;

        if (!(resultado.Filas.Count == 1 && vinculos.Count == 1) && !mismoElemento)
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
