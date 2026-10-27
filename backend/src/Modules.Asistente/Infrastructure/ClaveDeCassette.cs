using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Modules.Asistente.Infrastructure;

/// <summary>Lo que identifica a una solicitud al proveedor, leído del cuerpo que sale.</summary>
internal sealed record IdentidadDeLaSolicitud(string Clave, string HashDelPrefijo, string Modelo);

/// <summary>Deriva la clave de un cassette de cuatro campos del cuerpo de la solicitud.</summary>
internal static class ClaveDeCassette
{
    /// <summary>Separador de los cuatro campos al armar el material de la huella.</summary>
    private const char Separador = '\u001f';

    /// <summary>Marca del esfuerzo cuando la solicitud no manda <c>output_config</c>.</summary>
    private const string SinEsfuerzo = "(sin output_config)";

    /// <summary>Calcula la identidad de la solicitud a partir de su cuerpo.</summary>
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

    /// <summary>El esfuerzo pedido, o la marca de que no se pidió ninguno.</summary>
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

    /// <summary>El texto de un bloque del cable, sea cadena, objeto o lista de bloques.</summary>
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
