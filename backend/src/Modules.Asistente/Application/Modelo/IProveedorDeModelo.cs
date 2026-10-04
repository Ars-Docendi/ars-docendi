namespace Modules.Asistente.Application;

/// <summary>
/// Proveedor del modelo de lenguaje, detrás de una interfaz propia (RNF-13).
/// </summary>
/// <remarks>
/// La interfaz existe desde el principio por dos motivos concretos, ninguno
/// especulativo:
///
/// 1. El proyecto contempla migrar a un modelo propio corriendo en la nube.
///    Ponerla ahora sale gratis; ponerla después es refactorizar el pipeline entero.
/// 2. Los ambientes efímeros de PR no pueden tener clave real: su workflow hace
///    checkout del head del PR y ejecuta un script que viene del propio PR, en un
///    job con los secrets del environment. El cliente simulado es lo que les
///    permite correr.
/// </remarks>
public interface IProveedorDeModelo
{
    /// <summary>Nombre del proveedor, para el registro operativo.</summary>
    string Nombre { get; }

    /// <summary>
    /// Si sus respuestas son simuladas y no vienen de ningún modelo.
    /// </summary>
    /// <remarks>
    /// Lo expone la interfaz —y no solo la configuración— para que quien consuma
    /// una respuesta pueda saber que es simulada sin averiguar cómo está armado el
    /// ambiente. Un texto inventado presentado como respuesta real es exactamente
    /// lo que la métrica del asistente prohíbe.
    /// </remarks>
    bool EsSimulado { get; }

    /// <summary>Pide una completación al modelo.</summary>
    Task<RespuestaDelModelo> CompletarAsync(SolicitudAlModelo solicitud, CancellationToken ct);
}

/// <summary>Cuánta deliberación se le pide al modelo antes de responder.</summary>
/// <remarks>
/// Vocabulario del puerto, no de ningún proveedor. Un adaptador lo mapea a lo que
/// su API entienda —o lo ignora, si su modelo no delibera— sin que el pipeline se
/// entere.
/// </remarks>
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

/// <summary>
/// Una llamada al modelo.
/// </summary>
/// <remarks>
/// Propiedades con nombre y no parámetros posicionales: <see cref="PrefijoEstable"/>
/// y <see cref="Mensaje"/> son las dos cadenas, y confundirlas en el orden no daría
/// error de compilación — mandaría la pregunta del usuario como system prompt y
/// rompería la caché del prefijo, que es de donde sale casi todo el ahorro.
/// </remarks>
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
    /// Cuánta variación se pide: 0.0 para generar SQL, más alta para redactar en
    /// español. Es una <b>intención</b>, y cada adaptador decide cómo expresarla.
    /// </summary>
    /// <remarks>
    /// No todos los proveedores la aceptan: los modelos Claude actuales la rechazan
    /// con 400, así que <c>ProveedorAnthropic</c> la absorbe y pide lo mismo por
    /// instrucción del prefijo y por esfuerzo de razonamiento.
    ///
    /// Sigue en el contrato igual, y a propósito. La temperatura es un parámetro
    /// real de casi cualquier otro proveedor, incluido un modelo propio corriendo en
    /// la nube —que es la migración que el proyecto contempla—. Sacarla porque UN
    /// adaptador no la soporta convertiría al puerto en la forma de ese adaptador,
    /// que es exactamente lo que un puerto no debe ser.
    /// </remarks>
    public required decimal Temperatura { get; init; }

    /// <summary>
    /// Cuánto se le pide al modelo que delibere antes de escribir.
    /// </summary>
    /// <remarks>
    /// <b>Va por solicitud y no por proveedor, y esa es la decisión.</b> Las tres
    /// llamadas del pipeline necesitan cosas distintas: generar una consulta contra
    /// catorce tablas se beneficia de pensar, y convertir filas ya obtenidas en una
    /// oración en español no. Con un solo valor global, la redacción razonaba antes
    /// de escribir la primera palabra y eso es espera pura para quien preguntó.
    ///
    /// Es una <b>intención</b>, como la temperatura: cada adaptador la expresa como
    /// puede. El vocabulario es propio del puerto y no de ningún proveedor, porque
    /// un puerto que nombra los niveles de un SDK deja de ser un puerto.
    /// </remarks>
    public required EsfuerzoDelModelo Esfuerzo { get; init; }

    /// <summary>Techo de tokens de la respuesta.</summary>
    public required int MaximoDeTokens { get; init; }

    /// <summary>
    /// JSON Schema de la respuesta esperada, o nulo si la respuesta es texto libre
    /// (asistente-proveedor-local, design.md D3).
    /// </summary>
    /// <remarks>
    /// Es una <b>declaración</b> del pipeline, no una orden a ningún proveedor: la
    /// forma de la salida la decide quien la interpreta. Un adaptador que puede
    /// imponerla —un servidor local con decodificación restringida— la usa; uno
    /// que no —<c>ProveedorAnthropic</c>— la ignora y su request no cambia en un
    /// byte, así que las claves de cassette tampoco.
    ///
    /// No reemplaza a ninguna validación: una salida con la forma correcta todavía
    /// pasa por el intérprete del generador y por el validador de SQL.
    /// </remarks>
    public string? EsquemaDeSalidaJson { get; init; }

    /// <summary>
    /// Recibe el texto de la respuesta a medida que llega, o nulo si alcanza con la
    /// respuesta completa (asistente-optimizaciones-modelo-local, design.md D9).
    /// </summary>
    /// <remarks>
    /// Es un <b>ofrecimiento</b>, igual que el esquema de salida: un adaptador que
    /// puede pedir la respuesta por fragmentos —el local— la usa, y uno que no la
    /// ignora. La respuesta que devuelve <c>CompletarAsync</c> sigue siendo el texto
    /// completo y es la única que vale: los fragmentos son una vista previa.
    ///
    /// Quien la provee no debe lanzar por un problema propio —un cliente que se
    /// desconectó—: el adaptador lo contaría como una falla del proveedor.
    /// No entra en la huella de los cassettes.
    /// </remarks>
    public Func<string, CancellationToken, Task>? AlRecibirTexto { get; init; }
}

/// <summary>
/// Lo que devuelve el modelo. Los conteos de tokens alimentan el registro
/// operativo, no la facturación.
/// </summary>
/// <param name="TokensDeCache">
/// Cuántos de los tokens de entrada los sirvió la caché del proveedor.
///
/// <b>Va aparte de <see cref="TokensDeEntrada"/>, que los incluye.</b> No es
/// redundancia: sin este número no hay forma de saber si la caché está pegando,
/// porque un prefijo cacheado y uno que se reprocesa entero producen exactamente
/// el mismo total de entrada. La diferencia es un orden de magnitud en costo y
/// tiempo de proceso, y hoy era invisible.
///
/// Cero significa dos cosas que conviene no confundir: que el proveedor no cachea,
/// o que cachea y esta llamada no acertó. La primera es del adaptador; la segunda
/// es lo que hay que investigar cuando se repite.
/// </param>
public sealed record RespuestaDelModelo(
    string Texto,
    int TokensDeEntrada,
    int TokensDeSalida,
    bool EsSimulada,
    bool SeQuedoSinTokens = false,
    int TokensDeCache = 0);

/// <summary>
/// Traduce el esfuerzo configurado por ambiente al vocabulario del puerto.
/// </summary>
/// <remarks>
/// Vive acá y no en el adaptador para que un valor mal escrito falle igual con
/// cualquier proveedor. Si el parseo estuviera del lado de Anthropic, cambiar de
/// proveedor cambiaría qué configuraciones son válidas, que es lo contrario de lo
/// que un puerto promete.
/// </remarks>
internal static class EsfuerzoConfigurado
{
    /// <summary>Interpreta el valor de configuración.</summary>
    /// <exception cref="InvalidOperationException">
    /// Si no es ninguno de los aceptados. Falla al construir y no en la primera
    /// llamada: un esfuerzo mal escrito descubierto en runtime es una respuesta
    /// degradada sin causa aparente.
    /// </exception>
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
