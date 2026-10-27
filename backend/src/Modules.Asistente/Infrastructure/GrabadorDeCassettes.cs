using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Graba y reproduce el cuerpo crudo de la respuesta del proveedor (RNF-15, RNF-16).
/// </summary>
internal sealed class GrabadorDeCassettes(
    AlmacenDeCassettes almacen,
    bool regrabar,
    string hashDelFixture,
    TimeProvider reloj,
    ILogger<GrabadorDeCassettes> log) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var cuerpoDeLaSolicitud = solicitud.Content is null
            ? string.Empty
            : await solicitud.Content.ReadAsStringAsync(ct);

        var identidad = ClaveDeCassette.Calcular(cuerpoDeLaSolicitud);

        var grabado = almacen.Reproducir(
            identidad.Clave, identidad.HashDelPrefijo, hashDelFixture);

        if (grabado is not null)
        {
            // Con el cassette presente NO se re-graba, aunque la variable esté puesta.
            log.LogInformation(
                "Se sirvió el cassette {Clave} desde {Directorio}; no hubo llamada al proveedor.",
                identidad.Clave,
                almacen.Directorio);

            return Servir(solicitud, grabado);
        }

        if (!regrabar)
        {
            var motivo = almacen.ExplicarAusencia(identidad.Clave, identidad.HashDelPrefijo);

            log.LogError(
                "Falta el cassette {Clave} en {Directorio} y la re-grabación no está puesta. "
                + "La llamada no sale. {Motivo}",
                identidad.Clave,
                almacen.Directorio,
                motivo);

            throw new InvalidOperationException(motivo);
        }

        var respuesta = await base.SendAsync(solicitud, ct);
        var cuerpoDeLaRespuesta = await respuesta.Content.ReadAsStringAsync(ct);

        if (respuesta.IsSuccessStatusCode)
        {
            almacen.Escribir(
                identidad.Clave,
                new SelloDelCassette(
                    identidad.Modelo,
                    reloj.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    identidad.HashDelPrefijo,
                    hashDelFixture),
                cuerpoDeLaRespuesta);

            log.LogInformation(
                "Se grabó el cassette {Clave} en {Directorio} con el modelo {Modelo}.",
                identidad.Clave,
                almacen.Directorio,
                identidad.Modelo);
        }
        else
        {
            // El sobre guarda un cuerpo y no un estado: grabar un 5xx dejaría un
            // cassette que se reproduce como un 200 con un cuerpo de error adentro,
            // y falla al interpretarlo en vez de al servirlo.
            log.LogWarning(
                "El proveedor respondió {Estado} para el cassette {Clave}: no se graba nada.",
                (int)respuesta.StatusCode,
                identidad.Clave);
        }

        // El contenido ya se leyó, así que se reemplaza por uno equivalente: el adaptador de
        // adentro tiene que poder leerlo igual que si nadie lo hubiera tocado.
        respuesta.Content.Dispose();
        respuesta.Content = Contenido(cuerpoDeLaRespuesta);

        return respuesta;
    }

    private static HttpResponseMessage Servir(HttpRequestMessage solicitud, string cuerpo) =>
        new(HttpStatusCode.OK)
        {
            Content = Contenido(cuerpo),
            RequestMessage = solicitud,
        };

    private static StringContent Contenido(string cuerpo) =>
        new(cuerpo, Encoding.UTF8, "application/json");
}
