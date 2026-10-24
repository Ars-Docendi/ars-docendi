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
/// A stored credential cannot be opened: it is malformed, was encrypted under a
/// master key that is not configured, or failed authentication (tampered, or
/// moved to another company's row). Carries no key material and no inner detail.
/// </summary>
internal sealed class CredencialIlegibleException(string message) : InvalidOperationException(message);

/// <summary>Result of opening a stored credential.</summary>
/// <param name="Clave">The plaintext API key. Never log it or put it in a message.</param>
/// <param name="NecesitaRecifrado">
/// True when it was encrypted under the previous master key and should be
/// stored again under the current one.
/// </param>
internal sealed record ClaveDescifrada(string Clave, bool NecesitaRecifrado)
{
    /// <summary>Redacted on purpose: the default record printer would show <see cref="Clave"/>.</summary>
    public override string ToString() => "ClaveDescifrada { Clave = ***, NecesitaRecifrado = "
        + NecesitaRecifrado + " }";
}

/// <summary>
/// Encrypts per-company API keys with AES-GCM (asistente-modelo-en-caliente, D4).
/// </summary>
/// <remarks>
/// Stored format: <c>v1:{kid}:{base64(iv|tag|ciphertext)}</c>, with a 12-byte IV
/// and a 16-byte tag. The associated data is <c>asistente:{empresa}</c>, so a
/// ciphertext copied to another company's row fails authentication.
///
/// <c>kid</c> is the first 8 hex characters of SHA-256 over a fixed label and the
/// raw master key. It names which master key encrypted a value, so decryption
/// picks the key directly instead of trying the current one and then the previous
/// one. A truncated hash of a 256-bit random key discloses nothing useful.
///
/// Reads <see cref="IOptions{TOptions}"/> on every operation and validates the
/// master keys only then (<see cref="ValidadorDeOpcionesAsistente.ValidarClavesMaestras"/>),
/// so constructing it, and starting the module, never depends on the keys.
/// </remarks>
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
    /// <exception cref="ClaveMaestraNoDisponibleException">No usable master key.</exception>
    /// <exception cref="CredencialIlegibleException">Malformed, unknown key id, or failed authentication.</exception>
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
