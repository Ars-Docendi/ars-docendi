namespace Modules.Asistente.Application;

/// <summary>Proveedor del modelo de lenguaje, detrás de una interfaz propia (RNF-13).</summary>
public interface IProveedorDeModelo
{
    /// <summary>Nombre del proveedor, para el registro operativo.</summary>
    string Nombre { get; }

    /// <summary>Si sus respuestas son simuladas y no vienen de ningún modelo.</summary>
    bool EsSimulado { get; }

    /// <summary>Pide una completación al modelo.</summary>
    Task<RespuestaDelModelo> CompletarAsync(SolicitudAlModelo solicitud, CancellationToken ct);
}

/// <summary>Cuánta deliberación se le pide al modelo antes de responder.</summary>
public enum EsfuerzoDelModelo
{
    /// <summary>Sin deliberación: responder directo.</summary>
    Minimo,

    /// <summary>Poca. Alcanza para transformar algo que ya está resuelto.</summary>
    Bajo,

    /// <summary>La del trabajo que exige elegir entre alternativas.</summary>
    Medio,

    /// <summary>Alta, para lo que no sale de una sola pasada.</summary>
    Alto,

    /// <summary>Todo lo que el modelo pueda.</summary>
    Maximo,
}

/// <summary>Una llamada al modelo.</summary>
public sealed record SolicitudAlModelo
{
    /// <summary>
    /// Parte estable del prompt: el esquema y las instrucciones. Nada que mute por
    /// turno (RNF-14), porque es lo que se cachea del lado del proveedor.
    /// </summary>
    public required string PrefijoEstable { get; init; }

    /// <summary>Parte variable: la pregunta del turno y sus parámetros.</summary>
    public required string Mensaje { get; init; }

    /// <summary>
    /// Cuánta variación se pide: 0.0 para generar SQL, más alta para redactar en español.
    /// </summary>
    public required decimal Temperatura { get; init; }

    /// <summary>Cuánto se le pide al modelo que delibere antes de escribir.</summary>
    public required EsfuerzoDelModelo Esfuerzo { get; init; }

    /// <summary>Techo de tokens de la respuesta.</summary>
    public required int MaximoDeTokens { get; init; }

    /// <summary>
    /// JSON Schema de la respuesta esperada, o nulo si la respuesta es texto libre
    /// (asistente-proveedor-local, design.md D3).
    /// </summary>
    public string? EsquemaDeSalidaJson { get; init; }

    /// <summary>
    /// Recibe el texto de la respuesta a medida que llega, o nulo si alcanza con la respuesta
    /// completa (asistente-optimizaciones-modelo-local, design.md D9).
    /// </summary>
    public Func<string, CancellationToken, Task>? AlRecibirTexto { get; init; }
}

/// <summary>
/// Lo que devuelve el modelo. Los conteos de tokens alimentan el registro operativo, no la
/// facturación.
/// </summary>
public sealed record RespuestaDelModelo(
    string Texto,
    int TokensDeEntrada,
    int TokensDeSalida,
    bool EsSimulada,
    bool SeQuedoSinTokens = false,
    int TokensDeCache = 0);

/// <summary>Traduce el esfuerzo configurado por ambiente al vocabulario del puerto.</summary>
internal static class EsfuerzoConfigurado
{
    /// <summary>Interpreta el valor de configuración.</summary>
    public static EsfuerzoDelModelo Interpretar(string configurado, string nombreDelValor) =>
        configurado?.Trim().ToLowerInvariant() switch
        {
            "minimo" or "mínimo" => EsfuerzoDelModelo.Minimo,
            "bajo" => EsfuerzoDelModelo.Bajo,
            "medio" => EsfuerzoDelModelo.Medio,
            "alto" => EsfuerzoDelModelo.Alto,
            "maximo" or "máximo" => EsfuerzoDelModelo.Maximo,
            _ => throw new InvalidOperationException(
                $"Esfuerzo '{configurado}' desconocido en {nombreDelValor}. "
                + "Los aceptados son: minimo, bajo, medio, alto, maximo."),
        };
}
