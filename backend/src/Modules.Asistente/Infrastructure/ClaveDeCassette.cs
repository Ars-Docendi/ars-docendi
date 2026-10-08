using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Lo que identifica a una solicitud al proveedor, leído del cuerpo que sale.
/// </summary>
/// <param name="Clave">Nombre con que el cassette vive en disco.</param>
/// <param name="HashDelPrefijo">Huella del prefijo estable, para el sello.</param>
/// <param name="Modelo">Modelo al que se le pidió la respuesta, para el sello.</param>
internal sealed record IdentidadDeLaSolicitud(string Clave, string HashDelPrefijo, string Modelo);

/// <summary>
/// Deriva la clave de un cassette de cuatro campos del cuerpo de la solicitud.
/// </summary>
/// <remarks>
/// <b>Cuatro campos y no el cuerpo entero</b>, y el motivo es el modo de fallar.
/// Hashear el cuerpo completo es más simple y más honesto con «esta clave
/// identifica este request», pero subir <c>MaximoDeTokensDeGeneracion</c> —una
/// perilla que la propia documentación invita a mover cuando aparece el aviso de
/// corte— invalidaría <b>todos</b> los cassettes de golpe, y recuperarlos costaría
/// otra corrida financiada. Los cuatro campos son los que determinan qué contesta
/// el modelo; el techo de tokens determina cuánto, y eso ya viaja adentro de la
/// respuesta grabada.
///
/// Costo asumido: dos solicitudes que difieren solo en un campo de afuera de los
/// cuatro comparten cassette. Es deliberado y es el punto.
///
/// SHA-256 y no <c>string.GetHashCode()</c>, con el mismo criterio —y por la misma
/// razón— que <c>ProveedorSimulado.Huella</c>: en .NET el hash de string está
/// aleatorizado por proceso, así que un cassette grabado hoy no se encontraría
/// mañana y el mecanismo fallaría cerrado sin que nada explicara por qué.
///
/// <b>No nombra el SDK.</b> Lee campos del cable —<c>system</c>, <c>messages</c>,
/// <c>model</c>, <c>output_config</c>—, que son nombres del protocolo y no
/// símbolos de la librería: por eso el guard que fija el SDK en un solo archivo
/// sigue en pie sin excepción nueva.
/// </remarks>
internal static class ClaveDeCassette
{
    /// <summary>
    /// Separador de los cuatro campos al armar el material de la huella.
    /// </summary>
    /// <remarks>
    /// Un carácter de control, que JSON obliga a escapar dentro de una cadena: así
    /// ningún valor puede contenerlo y dos repartos distintos de los mismos
    /// caracteres no pueden producir el mismo material.
    /// </remarks>
    private const char Separador = '\u001f';

    /// <summary>
    /// Marca del esfuerzo cuando la solicitud no manda <c>output_config</c>.
    /// </summary>
    /// <remarks>
    /// Omitir el campo NO es «esfuerzo bajo»: es lo que el adaptador hace con
    /// esfuerzo mínimo, porque hay modelos que no deliberan y rechazan el campo con
    /// 400. Las dos llamadas le hablan distinto al modelo y no pueden compartir
    /// cassette, así que la ausencia tiene su propio valor en el material.
    /// </remarks>
    private const string SinEsfuerzo = "(sin output_config)";

    /// <summary>Calcula la identidad de la solicitud a partir de su cuerpo.</summary>
    /// <exception cref="InvalidOperationException">
    /// Si el cuerpo no es JSON o le falta uno de los campos esperados. Falla
    /// ruidoso a propósito: un campo que deja de estar —porque el formato del cable
    /// cambió— haría que todas las solicitudes colapsaran a la misma clave sobre
    /// cadena vacía y se sirvieran unas a otras, en silencio.
    /// </exception>
    public static IdentidadDeLaSolicitud Calcular(string cuerpoDeLaSolicitud)
    {
        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(cuerpoDeLaSolicitud);
        }
        catch (JsonException excepcion)
        {
            throw new InvalidOperationException(
                "El cuerpo de la solicitud al proveedor no es JSON, así que no se puede "
                + "derivar la clave del cassette.",
                excepcion);
        }

        using (documento)
        {
            var raiz = documento.RootElement;

            var prefijo = TextoDe(Requerido(raiz, "system"), "system");
            var mensaje = TextoDeLosMensajes(Requerido(raiz, "messages"));
            var esfuerzo = EsfuerzoDe(raiz);
            var modelo = Requerido(raiz, "model").GetString();

            if (string.IsNullOrWhiteSpace(modelo))
            {
                throw Falta("model");
            }

            var material = string.Join(Separador, prefijo, mensaje, esfuerzo, modelo);

            return new IdentidadDeLaSolicitud(HuellaDe(material), HuellaDe(prefijo), modelo);
        }
    }

    /// <summary>Huella estable de un texto, en hexadecimal minúscula.</summary>
    internal static string HuellaDe(string texto) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));

    private static JsonElement Requerido(JsonElement raiz, string campo) =>
        raiz.ValueKind == JsonValueKind.Object && raiz.TryGetProperty(campo, out var valor)
            ? valor
            : throw Falta(campo);

    /// <summary>
    /// El esfuerzo pedido, o la marca de que no se pidió ninguno.
    /// </summary>
    /// <remarks>
    /// <c>output_config</c> ausente es legítimo y no falla. Presente pero sin
    /// <c>effort</c> sí: significa que el formato del cable cambió, y adivinar ahí
    /// es exactamente lo que hace que dos llamadas distintas compartan cassette.
    /// </remarks>
    private static string EsfuerzoDe(JsonElement raiz)
    {
        if (raiz.ValueKind != JsonValueKind.Object
            || !raiz.TryGetProperty("output_config", out var salida)
            || salida.ValueKind == JsonValueKind.Null)
        {
            return SinEsfuerzo;
        }

        return salida.ValueKind == JsonValueKind.Object
            && salida.TryGetProperty("effort", out var esfuerzo)
            && esfuerzo.GetString() is { Length: > 0 } valor
                ? valor
                : throw Falta("output_config.effort");
    }

    /// <summary>
    /// El texto de un bloque del cable, sea cadena, objeto o lista de bloques.
    /// </summary>
    /// <remarks>
    /// Se lee el <b>texto</b> y no el JSON crudo del campo. Con el JSON crudo, un
    /// cambio de formato del serializador —otro orden de claves, otro espaciado—
    /// movería la huella sin que el prefijo hubiera cambiado, y todos los cassettes
    /// quedarían sellados con un prefijo «ajeno» que nadie tocó.
    /// </remarks>
    private static string TextoDe(JsonElement bloque, string campo) => bloque.ValueKind switch
    {
        JsonValueKind.String => bloque.GetString() ?? string.Empty,
        JsonValueKind.Object => bloque.TryGetProperty("text", out var texto)
            ? texto.GetString() ?? string.Empty
            : string.Empty,
        JsonValueKind.Array => string.Concat(
            bloque.EnumerateArray().Select(hijo => TextoDe(hijo, campo))),
        _ => throw Falta(campo),
    };

    private static string TextoDeLosMensajes(JsonElement mensajes)
    {
        if (mensajes.ValueKind != JsonValueKind.Array)
        {
            throw Falta("messages");
        }

        var texto = new StringBuilder();

        foreach (var mensaje in mensajes.EnumerateArray())
        {
            if (mensaje.ValueKind == JsonValueKind.Object
                && mensaje.TryGetProperty("content", out var contenido))
            {
                texto.Append(TextoDe(contenido, "messages"));
            }
            else
            {
                texto.Append(TextoDe(mensaje, "messages"));
            }
        }

        return texto.ToString();
    }

    private static InvalidOperationException Falta(string campo) =>
        new($"El cuerpo de la solicitud al proveedor no trae '{campo}'. Sin ese campo la "
            + "clave del cassette se calcularía sobre una cadena vacía, y todas las "
            + "solicitudes compartirían cassette sin que nada fallara.");
}
