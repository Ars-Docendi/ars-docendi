using System.Net.Http.Headers;
using System.Net.Http.Json;
using ArsDocendi.Shared.Auditing;

namespace ArsDocendi.Host.Auditoria;

public sealed record OpcionesPublicacionLotes(
    Uri FirmadorEndpoint,
    string TokenFirmador,
    Uri TestigoPrimarioEndpoint,
    string TokenTestigoPrimario,
    Uri TestigoSecundarioEndpoint,
    string TokenTestigoSecundario);

public sealed record ResultadoPublicacionLote(
    bool SinTrabajo,
    bool Ocupado,
    bool DobleCustodiaRegistrada,
    long? LoteId,
    long? UltimaSecuencia);

/// <summary>
/// Publica con un contrato HTTP mínimo. La clave privada permanece en el firmador;
/// el testigo primario recibe el manifiesto y el secundario sólo su hash.
/// </summary>
public sealed class PublicadorRemotoLotes(
    PreparadorLotesAuditoria preparador,
    HttpClient http,
    OpcionesPublicacionLotes opciones,
    CompuertaPublicacionAuditoria? compuerta = null)
{
    public async Task<ResultadoPublicacionLote> PublicarSiguienteAsync(
        string ambiente,
        int maximoEventos,
        CancellationToken ct)
    {
        ValidarOpciones(opciones);
        if (compuerta is not null) await compuerta.ValidarAsync(ambiente, ct);
        var preparado = await preparador.PrepararSiguienteAsync(ambiente, maximoEventos, ct);
        if (preparado.OcupadoPorOtroProceso)
            return new(false, true, false, null, null);
        var lote = preparado.Lote;
        if (lote is null)
            return new(true, false, false, null, null);

        if (lote.Firma is null || lote.IdClaveFirma is null)
        {
            var firma = await FirmarAsync(lote, ct);
            compuerta?.VerificarFirma(lote.HashManifiesto, firma.Firma, firma.IdClave);
            await preparador.RegistrarFirmaAsync(lote.Id, firma.IdClave, firma.Firma, ct);
            lote = lote with { Firma = firma.Firma, IdClaveFirma = firma.IdClave };
        }

        if (lote.TestigoPrimarioEn is null)
        {
            await PublicarPrimarioAsync(lote, ct);
            await preparador.RegistrarAcuseTestigoAsync(lote.Id, true, lote.HashManifiesto, ct);
        }

        if (lote.TestigoSecundarioEn is null)
        {
            await PublicarSecundarioAsync(lote, ct);
            await preparador.RegistrarAcuseTestigoAsync(lote.Id, false, lote.HashManifiesto, ct);
        }

        return new(false, false, true, lote.Id, lote.UltimaSecuencia);
    }

    private async Task<FirmaRemota> FirmarAsync(LoteAuditoriaPreparado lote, CancellationToken ct)
    {
        using var solicitud = CrearSolicitud(opciones.FirmadorEndpoint, opciones.TokenFirmador);
        solicitud.Content = JsonContent.Create(new SolicitudFirma(
            lote.Ambiente,
            Convert.ToHexString(lote.HashManifiesto).ToLowerInvariant(),
            Convert.ToBase64String(lote.Manifiesto)));
        using var respuesta = await http.SendAsync(solicitud, ct);
        respuesta.EnsureSuccessStatusCode();
        var contenido = await respuesta.Content.ReadFromJsonAsync<RespuestaFirma>(cancellationToken: ct)
            ?? throw new InvalidDataException("El servicio de firma devolvió una respuesta vacía.");
        if (string.IsNullOrWhiteSpace(contenido.IdClave))
            throw new InvalidDataException("El servicio de firma no informó el identificador de clave.");
        byte[] firma;
        try { firma = Convert.FromBase64String(contenido.FirmaBase64); }
        catch (FormatException error) { throw new InvalidDataException("La firma recibida no es Base64 válido.", error); }
        if (firma.Length == 0) throw new InvalidDataException("El servicio de firma devolvió una firma vacía.");
        return new(contenido.IdClave, firma);
    }

    private async Task PublicarPrimarioAsync(LoteAuditoriaPreparado lote, CancellationToken ct)
    {
        using var solicitud = CrearSolicitud(opciones.TestigoPrimarioEndpoint, opciones.TokenTestigoPrimario);
        solicitud.Content = JsonContent.Create(new SolicitudTestigoPrimario(
            lote.Ambiente,
            lote.PrimeraSecuencia,
            lote.UltimaSecuencia,
            Convert.ToHexString(lote.HashLote).ToLowerInvariant(),
            Convert.ToHexString(lote.HashManifiesto).ToLowerInvariant(),
            lote.IdClaveFirma!,
            Convert.ToBase64String(lote.Firma!),
            Convert.ToBase64String(lote.Manifiesto)));
        using var respuesta = await http.SendAsync(solicitud, ct);
        respuesta.EnsureSuccessStatusCode();
        await ValidarAcuseAsync(respuesta, lote, ct);
    }

    private async Task PublicarSecundarioAsync(LoteAuditoriaPreparado lote, CancellationToken ct)
    {
        using var solicitud = CrearSolicitud(opciones.TestigoSecundarioEndpoint, opciones.TokenTestigoSecundario);
        solicitud.Content = JsonContent.Create(new SolicitudTestigoSecundario(
            lote.Ambiente,
            lote.PrimeraSecuencia,
            lote.UltimaSecuencia,
            Convert.ToHexString(lote.HashManifiesto).ToLowerInvariant()));
        using var respuesta = await http.SendAsync(solicitud, ct);
        respuesta.EnsureSuccessStatusCode();
        await ValidarAcuseAsync(respuesta, lote, ct);
    }

    private static async Task ValidarAcuseAsync(
        HttpResponseMessage respuesta,
        LoteAuditoriaPreparado lote,
        CancellationToken ct)
    {
        var contenido = await respuesta.Content.ReadFromJsonAsync<RespuestaTestigo>(cancellationToken: ct)
            ?? throw new InvalidDataException("El testigo devolvió un acuse vacío.");
        var hashEsperado = Convert.ToHexString(lote.HashManifiesto).ToLowerInvariant();
        if (!string.Equals(contenido.HashAceptado, hashEsperado, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("El testigo no confirmó el hash publicado.");
    }

    private static HttpRequestMessage CrearSolicitud(Uri endpoint, string token)
    {
        var solicitud = new HttpRequestMessage(HttpMethod.Post, endpoint);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return solicitud;
    }

    private static void ValidarOpciones(OpcionesPublicacionLotes configuracion)
    {
        ValidarEndpoint(configuracion.FirmadorEndpoint, configuracion.TokenFirmador);
        ValidarEndpoint(configuracion.TestigoPrimarioEndpoint, configuracion.TokenTestigoPrimario);
        ValidarEndpoint(configuracion.TestigoSecundarioEndpoint, configuracion.TokenTestigoSecundario);
        var autoridades = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            configuracion.FirmadorEndpoint.Authority,
            configuracion.TestigoPrimarioEndpoint.Authority,
            configuracion.TestigoSecundarioEndpoint.Authority
        };
        if (autoridades.Count != 3)
            throw new InvalidOperationException("Firmador y custodios deben usar authorities HTTPS separadas.");
        if (configuracion.TokenFirmador == configuracion.TokenTestigoPrimario
            || configuracion.TokenFirmador == configuracion.TokenTestigoSecundario
            || configuracion.TokenTestigoPrimario == configuracion.TokenTestigoSecundario)
            throw new InvalidOperationException("Firmador y testigos deben usar credenciales distintas.");
    }

    private static void ValidarEndpoint(Uri endpoint, string token)
    {
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Los servicios de firma y custodia deben usar HTTPS.");
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Falta una credencial de servicio de firma o custodia.");
    }

    private sealed record SolicitudFirma(string Ambiente, string HashManifiesto, string ManifiestoBase64);
    private sealed record RespuestaFirma(string IdClave, string FirmaBase64);
    private sealed record SolicitudTestigoPrimario(
        string Ambiente, long PrimeraSecuencia, long UltimaSecuencia,
        string HashLote, string HashManifiesto,
        string IdClaveFirma, string FirmaBase64, string ManifiestoBase64);
    private sealed record SolicitudTestigoSecundario(
        string Ambiente, long PrimeraSecuencia, long UltimaSecuencia, string HashManifiesto);
    private sealed record RespuestaTestigo(string HashAceptado);
    private sealed record FirmaRemota(string IdClave, byte[] Firma);
}
