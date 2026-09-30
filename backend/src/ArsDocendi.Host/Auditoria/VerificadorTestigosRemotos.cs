using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace ArsDocendi.Host.Auditoria;

public sealed record OpcionesLecturaTestigos(Uri Primario, string TokenPrimario,
    Uri Secundario, string TokenSecundario, IReadOnlyDictionary<string, string> ClavesPublicasPem,
    IReadOnlyList<TransicionClaveFirma>? Transiciones = null);
public sealed record TransicionClaveFirma(string Ambiente, string IdAnterior, string IdNuevo,
    string FirmaAnteriorBase64);
public sealed record EvidenciaTestigoRemoto(string Ambiente, long PrimeraSecuencia,
    long UltimaSecuencia, string HashManifiesto, string IdClaveFirma, string FirmaBase64,
    string? ManifiestoBase64, DateTimeOffset RegistradoEn);
public sealed record EstadoTestigosRemotos(long Cursor, string HashManifiesto,
    DateTimeOffset PrimarioEn, DateTimeOffset SecundarioEn, bool RequiereReconciliacion);
public sealed record LecturaPublicacionTestigos(EvidenciaTestigoRemoto? Primario,
    EvidenciaTestigoRemoto? Secundario);

/// <summary>
/// Contrato de lectura independiente, sin API de escritura ni credencial de firma.
/// RSA-PSS/SHA-256 sobre el digest del manifiesto; claves fijadas por el operador,
/// nunca aceptadas desde PostgreSQL o desde la respuesta del custodio.
/// </summary>
public sealed class VerificadorTestigosRemotos(HttpClient http, OpcionesLecturaTestigos opciones)
{
    /// <summary>404 significa ausencia sólo si cada custodio independiente la informa.</summary>
    public async Task<LecturaPublicacionTestigos> LeerParaPublicarAsync(string ambiente, CancellationToken ct)
    {
        if (opciones.Primario.Authority == opciones.Secundario.Authority
            || opciones.TokenPrimario == opciones.TokenSecundario)
            throw new InvalidOperationException("Se requieren testigos separados.");
        var primero = await LeerOpcionalAsync(opciones.Primario, opciones.TokenPrimario, ct);
        var segundo = await LeerOpcionalAsync(opciones.Secundario, opciones.TokenSecundario, ct);
        foreach (var evidencia in new[] { primero, segundo })
        {
            if (evidencia is null) continue;
            if (evidencia.Ambiente != ambiente || evidencia.PrimeraSecuencia < 1
                || evidencia.UltimaSecuencia < evidencia.PrimeraSecuencia - 1)
                throw new InvalidDataException("Ambiente o rango remoto inválido.");
            try { if (Convert.FromHexString(evidencia.HashManifiesto).Length != 32) throw new FormatException(); }
            catch (FormatException error) { throw new InvalidDataException("Digest remoto mal formado.", error); }
        }
        if (primero is not null)
        {
            var manifiesto = Convert.FromBase64String(primero.ManifiestoBase64
                ?? throw new InvalidDataException("Falta manifiesto del custodio principal."));
            var hash = SHA256.HashData(manifiesto);
            if (!CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(primero.HashManifiesto)))
                throw new InvalidDataException("El manifiesto remoto difiere de su digest.");
            using var json = JsonDocument.Parse(manifiesto);
            var raiz = json.RootElement;
            if (raiz.GetProperty("version").GetInt32() != 1
                || raiz.GetProperty("ambiente").GetString() != ambiente
                || raiz.GetProperty("primeraSecuencia").GetInt64() != primero.PrimeraSecuencia
                || raiz.GetProperty("ultimaSecuencia").GetInt64() != primero.UltimaSecuencia)
                throw new InvalidDataException("Metadatos firmados inconsistentes.");
            VerificarFirma(hash, Convert.FromBase64String(primero.FirmaBase64), primero.IdClaveFirma);
        }
        return new(primero, segundo);
    }

    private async Task<EvidenciaTestigoRemoto?> LeerOpcionalAsync(Uri uri, string token, CancellationToken ct)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)
            || string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Lectura de testigos exige HTTPS y credencial dedicada.");
        using var solicitud = new HttpRequestMessage(HttpMethod.Get, uri);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var respuesta = await http.SendAsync(solicitud, ct);
        if (respuesta.StatusCode == HttpStatusCode.NotFound) return null;
        respuesta.EnsureSuccessStatusCode();
        return await respuesta.Content.ReadFromJsonAsync<EvidenciaTestigoRemoto>(cancellationToken: ct)
            ?? throw new InvalidDataException("Testigo sin evidencia.");
    }

    public async Task<EstadoTestigosRemotos> VerificarAsync(string ambiente, long cursorLocal, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ambiente);
        if (cursorLocal < 0) throw new ArgumentOutOfRangeException(nameof(cursorLocal));
        if (opciones.Primario.Authority == opciones.Secundario.Authority
            || opciones.TokenPrimario == opciones.TokenSecundario)
            throw new InvalidOperationException("Se requieren testigos y credenciales de lectura separados.");
        var primario = await LeerAsync(opciones.Primario, opciones.TokenPrimario, ct);
        var secundario = await LeerAsync(opciones.Secundario, opciones.TokenSecundario, ct);
        if (primario.Ambiente != ambiente || secundario.Ambiente != ambiente
            || primario.UltimaSecuencia != secundario.UltimaSecuencia
            || primario.PrimeraSecuencia != secundario.PrimeraSecuencia
            || !string.Equals(primario.HashManifiesto, secundario.HashManifiesto, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Testigos divergentes o ambiente incorrecto; reconciliar.");
        if (primario.PrimeraSecuencia < 1 || primario.UltimaSecuencia < primario.PrimeraSecuencia - 1)
            throw new InvalidDataException("Rango remoto inválido.");
        var manifiesto = Convert.FromBase64String(primario.ManifiestoBase64
            ?? throw new InvalidDataException("Falta el manifiesto primario."));
        var hash = SHA256.HashData(manifiesto);
        if (!CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(primario.HashManifiesto)))
            throw new InvalidDataException("Digest remoto inválido.");
        using var json = JsonDocument.Parse(manifiesto);
        var raiz = json.RootElement;
        if (raiz.GetProperty("version").GetInt32() != 1
            || raiz.GetProperty("ambiente").GetString() != ambiente
            || raiz.GetProperty("primeraSecuencia").GetInt64() != primario.PrimeraSecuencia
            || raiz.GetProperty("ultimaSecuencia").GetInt64() != primario.UltimaSecuencia)
            throw new InvalidDataException("Metadatos remotos no coinciden con el manifiesto firmado.");
        VerificarFirma(hash, Convert.FromBase64String(primario.FirmaBase64), primario.IdClaveFirma);
        return new(primario.UltimaSecuencia, primario.HashManifiesto,
            primario.RegistradoEn, secundario.RegistradoEn, cursorLocal < primario.UltimaSecuencia);
    }

    public void VerificarFirma(byte[] hash, byte[] firma, string idClave)
    {
        if (!opciones.ClavesPublicasPem.TryGetValue(idClave, out var pem))
            throw new InvalidDataException("Clave de firma no confiable.");
        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        if (rsa.KeySize < 2048 || !rsa.VerifyHash(hash, firma,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
            throw new InvalidDataException("Firma de auditoría inválida.");
    }

    /// <summary>La clave anterior autoriza explícitamente la huella de la nueva.</summary>
    public void VerificarTransicion(string ambiente, string idAnterior, string idNuevo)
    {
        var transiciones = opciones.Transiciones?.Where(t => t.Ambiente == ambiente
            && t.IdAnterior == idAnterior && t.IdNuevo == idNuevo).ToArray() ?? [];
        if (transiciones.Length != 1 || !opciones.ClavesPublicasPem.TryGetValue(idNuevo, out var pem))
            throw new InvalidDataException("Falta transición firmada de clave de auditoría.");
        byte[] firma;
        try { firma = Convert.FromBase64String(transiciones[0].FirmaAnteriorBase64); }
        catch (FormatException error) { throw new InvalidDataException("Transición de clave mal formada.", error); }
        VerificarFirma(CalcularDigestTransicion(ambiente, idAnterior, idNuevo, pem), firma, idAnterior);
    }

    public static byte[] CalcularDigestTransicion(string ambiente, string idAnterior, string idNuevo, string claveNuevaPem)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(claveNuevaPem);
        if (rsa.KeySize < 2048) throw new InvalidDataException("Clave nueva insuficiente.");
        var huella = Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            dominio = "ars-docendi:transicion-clave-auditoria:v1", ambiente, idAnterior, idNuevo, huella
        });
        return SHA256.HashData(bytes);
    }

    private async Task<EvidenciaTestigoRemoto> LeerAsync(Uri uri, string token, CancellationToken ct)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)
            || string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Lectura de testigos exige HTTPS y credencial dedicada.");
        using var solicitud = new HttpRequestMessage(HttpMethod.Get, uri);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var respuesta = await http.SendAsync(solicitud, ct);
        respuesta.EnsureSuccessStatusCode();
        return await respuesta.Content.ReadFromJsonAsync<EvidenciaTestigoRemoto>(cancellationToken: ct)
            ?? throw new InvalidDataException("Testigo sin evidencia.");
    }
}
