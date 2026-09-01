using Modules.Asistente.Application;

namespace ArsDocendi.Evaluacion.Nucleo.Runner;

/// <summary>Veredicto del preflight.</summary>
/// <param name="Aprobado">Si la corrida puede arrancar.</param>
/// <param name="Motivo">Por qué no, cuando no.</param>
public sealed record ResultadoDePreflight(bool Aprobado, string? Motivo)
{
    /// <summary>Veredicto favorable.</summary>
    public static readonly ResultadoDePreflight Aprueba = new(true, null);

    /// <summary>Arma un veredicto de rechazo.</summary>
    public static ResultadoDePreflight Rechaza(string motivo) => new(false, motivo);
}

/// <summary>
/// Verifica que el proveedor responde <b>de verdad</b> antes de correr nada.
/// </summary>
/// <remarks>
/// <b>Sin crédito de API el eval no falla: miente.</b> El request devuelve una
/// abstención con error seteado y métricas en cero, así que los ítems no
/// contestables pasan espuriamente y el reporte muestra un número bajo que parece
/// una regresión del modelo. La señal de diagnóstico es entrada y salida en cero,
/// con latencias de milisegundos en vez de segundos.
///
/// Se chequean tres cosas, y las tres por separado porque cubren casos distintos:
/// que no haya excepción —el proveedor está caído—, que la respuesta no sea
/// simulada —se corrió el eval sin configurar el proveedor real— y que los tokens
/// sean mayores que cero —hay proveedor pero no hay crédito—.
/// </remarks>
public static class Preflight
{
    private const string PreguntaDePrueba =
        "Respondé únicamente con la palabra listo.";

    /// <summary>Pide una completación trivial y verifica que sea real.</summary>
    public static async Task<ResultadoDePreflight> VerificarAsync(
        IProveedorDeModelo proveedor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(proveedor);

        if (proveedor.EsSimulado)
        {
            // Se corta antes de gastar la llamada: la bandera ya lo dice.
            return ResultadoDePreflight.Rechaza(
                $"El proveedor '{proveedor.Nombre}' es simulado. Una corrida contra un "
                + "proveedor simulado no mide nada: configurá uno real.");
        }

        RespuestaDelModelo respuesta;
        try
        {
            respuesta = await proveedor.CompletarAsync(
                new SolicitudAlModelo
                {
                    PrefijoEstable = "Prueba de conectividad del evaluador.",
                    Mensaje = PreguntaDePrueba,
                    Temperatura = 0.0m,

                    // Mínimo: el preflight pregunta si el proveedor responde y
                    // cobra, no si piensa bien. Deliberar acá alarga cada corrida
                    // sin agregar nada a lo que la comprobación verifica.
                    Esfuerzo = EsfuerzoDelModelo.Minimo,

                    // Holgado para una respuesta de una palabra, y a propósito. Con
                    // esfuerzo configurado el modelo razona antes de escribir y ese
                    // razonamiento sale del MISMO presupuesto: con un techo de 16
                    // tokens el preflight se cortaría siempre, avisaría de un corte
                    // por presupuesto en cada corrida, y ese aviso —que existe para
                    // señalar un problema real— se volvería ruido que se aprende a
                    // ignorar. Lo que se paga son los tokens generados, no el techo.
                    MaximoDeTokens = 512,
                },
                ct);
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            return ResultadoDePreflight.Rechaza(
                $"El proveedor '{proveedor.Nombre}' no respondió: {excepcion.Message}");
        }

        if (respuesta.EsSimulada)
        {
            return ResultadoDePreflight.Rechaza(
                $"El proveedor '{proveedor.Nombre}' devolvió una respuesta simulada.");
        }

        if (respuesta.TokensDeEntrada <= 0 || respuesta.TokensDeSalida <= 0)
        {
            // Éste es el caso que engaña: hay proveedor, contesta, y no cobra nada
            // porque no procesó nada.
            return ResultadoDePreflight.Rechaza(
                $"El proveedor '{proveedor.Nombre}' respondió con métricas en cero "
                + $"(entrada {respuesta.TokensDeEntrada}, salida {respuesta.TokensDeSalida}). "
                + "Es la firma de una cuenta sin crédito: la corrida mediría abstenciones falsas.");
        }

        return ResultadoDePreflight.Aprueba;
    }
}
