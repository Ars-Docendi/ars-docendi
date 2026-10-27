using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// There is no master key to encrypt or decrypt with: <c>Asistente__ClaveMaestra</c>
/// is unset or malformed. The message names the setting, never a value.
/// </summary>
internal sealed class ClaveMaestraNoDisponibleException(string message) : InvalidOperationException(message);

/// <summary>
/// A stored credential cannot be opened: it is malformed, was encrypted under a master key that is
/// not configured, or failed authentication (tampered, or moved to another company's row).
/// </summary>
internal sealed class CredencialIlegibleException(string message) : InvalidOperationException(message);

/// <summary>Result of opening a stored credential.</summary>
internal sealed record ClaveDescifrada(string Clave, bool NecesitaRecifrado)
{
    /// <summary>Redacted on purpose: the default record printer would show <see cref="Clave"/>.</summary>
    public override string ToString() => "ClaveDescifrada { Clave = ***, NecesitaRecifrado = "
        + NecesitaRecifrado + " }";
}

/// <summary>
/// Encrypts per-company API keys with AES-GCM (asistente-modelo-en-caliente, D4).
/// </summary>
internal sealed class CifradoDeCredenciales(IOptions<OpcionesAsistente> opciones)
{
    private const string Version = "v1";
    private const int LongitudDeIv = 12;
    private const int LongitudDeTag = 16;

    private static readonly byte[] EtiquetaDelKid = Encoding.UTF8.GetBytes("asistente-clave-maestra-kid-v1");

    /// <summary>True when a valid current master key is configured.</summary>
    public bool Disponible => ValidadorDeOpcionesAsistente.ValidarClavesMaestras(opciones.Value).Count == 0;

    /// <summary>Encrypts <paramref name="claveEnClaro"/> for <paramref name="empresa"/> under the current key.</summary>
    /// <exception cref="ClaveMaestraNoDisponibleException">No usable master key.</exception>
    public string Cifrar(string empresa, string claveEnClaro)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(empresa);
        ArgumentException.ThrowIfNullOrEmpty(claveEnClaro);

        var (actual, _) = Claves();

        var texto = Encoding.UTF8.GetBytes(claveEnClaro);
        var iv = RandomNumberGenerator.GetBytes(LongitudDeIv);
        var cifrado = new byte[texto.Length];
        var tag = new byte[LongitudDeTag];

        using (var aes = new AesGcm(actual, LongitudDeTag))
        {
            aes.Encrypt(iv, texto, cifrado, tag, DatoAsociado(empresa));
        }

        CryptographicOperations.ZeroMemory(texto);

        return $"{Version}:{Kid(actual)}:{Convert.ToBase64String([.. iv, .. tag, .. cifrado])}";
    }

    /// <summary>Opens a stored value, choosing the master key by its <c>kid</c>.</summary>
    public ClaveDescifrada Descifrar(string empresa, string almacenado)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(empresa);
        ArgumentNullException.ThrowIfNull(almacenado);

        var (actual, anterior) = Claves();

        var partes = almacenado.Split(':', 3);
        if (partes.Length != 3 || partes[0] != Version)
        {
            throw new CredencialIlegibleException("The stored credential has an unsupported format.");
        }

        byte[] clave;
        bool recifrar;
        if (partes[1] == Kid(actual))
        {
            clave = actual;
            recifrar = false;
        }
        else if (anterior is not null && partes[1] == Kid(anterior))
        {
            clave = anterior;
            recifrar = true;
        }
        else
        {
            throw new CredencialIlegibleException(
                "The stored credential was encrypted under a master key that is not configured.");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(partes[2]);
        }
        catch (FormatException)
        {
            throw new CredencialIlegibleException("The stored credential has an unsupported format.");
        }

        if (bytes.Length < LongitudDeIv + LongitudDeTag)
        {
            throw new CredencialIlegibleException("The stored credential has an unsupported format.");
        }

        var iv = bytes.AsSpan(0, LongitudDeIv);
        var tag = bytes.AsSpan(LongitudDeIv, LongitudDeTag);
        var cifrado = bytes.AsSpan(LongitudDeIv + LongitudDeTag);
        var texto = new byte[cifrado.Length];

        try
        {
            using var aes = new AesGcm(clave, LongitudDeTag);
            aes.Decrypt(iv, cifrado, tag, texto, DatoAsociado(empresa));
            return new ClaveDescifrada(Encoding.UTF8.GetString(texto), recifrar);
        }
        catch (CryptographicException)
        {
            // Deliberately drops the inner exception: nothing but "it failed".
            throw new CredencialIlegibleException("The stored credential failed authentication.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(texto);
        }
    }

    private (byte[] Actual, byte[]? Anterior) Claves()
    {
        var valores = opciones.Value;

        var fallas = ValidadorDeOpcionesAsistente.ValidarClavesMaestras(valores);
        if (fallas.Count > 0)
        {
            throw new ClaveMaestraNoDisponibleException(string.Join(" ", fallas));
        }

        return (
            Convert.FromBase64String(valores.ClaveMaestra.Trim()),
            string.IsNullOrWhiteSpace(valores.ClaveMaestraAnterior)
                ? null
                : Convert.FromBase64String(valores.ClaveMaestraAnterior.Trim()));
    }

    private static byte[] DatoAsociado(string empresa) => Encoding.UTF8.GetBytes($"asistente:{empresa}");

    private static string Kid(byte[] clave) =>
        Convert.ToHexStringLower(SHA256.HashData([.. EtiquetaDelKid, .. clave]))[..8];
}
