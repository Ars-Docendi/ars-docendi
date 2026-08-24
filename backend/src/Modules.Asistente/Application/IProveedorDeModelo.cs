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

    /// <summary>0.0 para generar SQL; más alta para redactar en español.</summary>
    public required decimal Temperatura { get; init; }

    /// <summary>Techo de tokens de la respuesta.</summary>
    public required int MaximoDeTokens { get; init; }
}

/// <summary>
/// Lo que devuelve el modelo. Los conteos de tokens alimentan el registro
/// operativo, no la facturación.
/// </summary>
public sealed record RespuestaDelModelo(
    string Texto,
    int TokensDeEntrada,
    int TokensDeSalida,
    bool EsSimulada);
