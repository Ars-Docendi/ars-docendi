using Microsoft.Extensions.Options;
using Modules.Asistente;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// AES-GCM envelope for per-company API keys (asistente-modelo-en-caliente, tasks 1.5, 1.9, 1.10).
/// </summary>
public sealed class CifradoDeCredencialesTests
{
    private const string Secreto = "sk-test-0123456789abcdef-NEVER-LOG-ME";

    internal static string ClaveMaestra(byte relleno) =>
        Convert.ToBase64String(Enumerable.Repeat(relleno, 32).ToArray());

    internal static CifradoDeCredenciales Cifrado(string actual, string anterior = "") =>
        new(Options.Create(new OpcionesAsistente { ClaveMaestra = actual, ClaveMaestraAnterior = anterior }));

    [Fact]
    public void A_key_round_trips_and_uses_the_documented_format()
    {
        var cifrado = Cifrado(ClaveMaestra(1));

        var almacenado = cifrado.Cifrar("openai", Secreto);
        var abierta = cifrado.Descifrar("openai", almacenado);

        Assert.Equal(Secreto, abierta.Clave);
        Assert.False(abierta.NecesitaRecifrado);

        var partes = almacenado.Split(':');
        Assert.Equal(3, partes.Length);
        Assert.Equal("v1", partes[0]);
        Assert.Equal(8, partes[1].Length);
        // iv (12) + tag (16) + one ciphertext byte per plaintext byte.
        Assert.Equal(12 + 16 + System.Text.Encoding.UTF8.GetByteCount(Secreto), Convert.FromBase64String(partes[2]).Length);
        Assert.DoesNotContain(Secreto, almacenado, StringComparison.Ordinal);
    }

    [Fact]
    public void Encrypting_twice_gives_different_ciphertexts()
    {
        var cifrado = Cifrado(ClaveMaestra(1));

        Assert.NotEqual(cifrado.Cifrar("openai", Secreto), cifrado.Cifrar("openai", Secreto));
    }

    [Fact]
    public void A_tampered_ciphertext_is_rejected()
    {
        var cifrado = Cifrado(ClaveMaestra(1));
        var almacenado = cifrado.Cifrar("openai", Secreto);

        var partes = almacenado.Split(':');
        var bytes = Convert.FromBase64String(partes[2]);
        bytes[^1] ^= 0x01;
        var alterado = $"{partes[0]}:{partes[1]}:{Convert.ToBase64String(bytes)}";

        var error = Assert.Throws<CredencialIlegibleException>(() => cifrado.Descifrar("openai", alterado));
        Assert.DoesNotContain(Secreto, error.Message, StringComparison.Ordinal);
        Assert.Null(error.InnerException);
    }

    [Fact]
    public void A_ciphertext_moved_to_another_company_fails_authentication()
    {
        var cifrado = Cifrado(ClaveMaestra(1));
        var almacenado = cifrado.Cifrar("openai", Secreto);

        Assert.Throws<CredencialIlegibleException>(() => cifrado.Descifrar("mistral", almacenado));
    }

    [Theory]
    [InlineData("")]
    [InlineData("v2:00000000:AAAA")]
    [InlineData("v1:abc")]
    [InlineData("v1:00000000:not-base64!!")]
    public void A_malformed_stored_value_is_rejected_without_detail(string almacenado)
    {
        var cifrado = Cifrado(ClaveMaestra(1));

        Assert.Throws<CredencialIlegibleException>(() => cifrado.Descifrar("openai", almacenado));
    }

    [Fact]
    public void A_value_under_the_previous_key_opens_and_is_flagged_for_re_encryption()
    {
        var vieja = Cifrado(ClaveMaestra(1));
        var almacenado = vieja.Cifrar("google", Secreto);

        var rotado = Cifrado(ClaveMaestra(2), anterior: ClaveMaestra(1));
        var abierta = rotado.Descifrar("google", almacenado);

        Assert.Equal(Secreto, abierta.Clave);
        Assert.True(abierta.NecesitaRecifrado);

        // Re-encrypted under the current key (different kid), it no longer asks for it.
        var nuevo = rotado.Cifrar("google", abierta.Clave);
        Assert.NotEqual(almacenado.Split(':')[1], nuevo.Split(':')[1]);
        Assert.False(rotado.Descifrar("google", nuevo).NecesitaRecifrado);
    }

    [Fact]
    public void A_value_under_an_unconfigured_key_is_rejected()
    {
        var almacenado = Cifrado(ClaveMaestra(1)).Cifrar("google", Secreto);

        var otra = Cifrado(ClaveMaestra(2));

        Assert.Throws<CredencialIlegibleException>(() => otra.Descifrar("google", almacenado));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not base64 !!")]
    [InlineData("AAAA")]
    public void Without_a_usable_master_key_every_operation_reports_it(string clave)
    {
        var cifrado = Cifrado(clave);

        Assert.False(cifrado.Disponible);
        Assert.Throws<ClaveMaestraNoDisponibleException>(() => cifrado.Cifrar("openai", Secreto));
        Assert.Throws<ClaveMaestraNoDisponibleException>(() => cifrado.Descifrar("openai", "v1:00000000:AAAA"));
    }

    [Fact]
    public void A_malformed_previous_key_is_reported_by_name_and_never_by_value()
    {
        const string Mala = "esto-no-es-base64-pero-es-secreto";
        var cifrado = Cifrado(ClaveMaestra(1), anterior: Mala);

        var error = Assert.Throws<ClaveMaestraNoDisponibleException>(() => cifrado.Cifrar("openai", Secreto));

        Assert.Contains(nameof(OpcionesAsistente.ClaveMaestraAnterior), error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Mala, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Master_keys_are_not_part_of_the_options_validation_that_runs_on_first_read()
    {
        // The module has to start with no master key: the lazy options validator
        // must accept the defaults (empty master keys).
        var resultado = new ValidadorDeOpcionesAsistente().Validate(null, new OpcionesAsistente());

        Assert.True(resultado.Succeeded, string.Join(" | ", resultado.Failures ?? []));
    }

    [Fact]
    public void The_decrypted_key_and_the_opened_credential_do_not_print_the_key()
    {
        var abierta = new ClaveDescifrada(Secreto, false);
        var credencial = new CredencialAbierta("openai", Secreto);

        Assert.DoesNotContain(Secreto, abierta.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Secreto, credencial.ToString(), StringComparison.Ordinal);
    }
}
