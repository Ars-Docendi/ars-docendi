using ArsDocendi.Shared.Persistencia;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>What the administration surface may know about a stored key: never the key.</summary>
internal sealed record EstadoDeCredencial(
    string Empresa, string Pista, DateTimeOffset? ValidadaEn, DateTimeOffset ActualizadoEn);

/// <summary>
/// A decrypted key, for the one consumer that has to send it to the provider.
/// </summary>
/// <remarks>
/// Not a record and with a redacted <see cref="ToString"/>, so it cannot be
/// printed by accident through string interpolation or a structured logger.
/// </remarks>
internal sealed class CredencialAbierta(string empresa, string clave)
{
    public string Empresa { get; } = empresa;

    /// <summary>The plaintext API key. Never log it or put it in a message.</summary>
    public string Clave { get; } = clave;

    public override string ToString() => $"CredencialAbierta {{ Empresa = {Empresa}, Clave = *** }}";
}

/// <summary>
/// Per-company API keys in <c>asistente.credencial_de_proveedor</c>
/// (asistente-modelo-en-caliente, D4). Every read and write goes through
/// <see cref="CifradoDeCredenciales"/>; the listing and write surface returns the
/// four-character hint and never the key.
/// </summary>
/// <remarks>
/// Uses the owner connection: the table sits inside the wholesale-revoked
/// <c>asistente</c> schema, so even reading it needs it.
/// </remarks>
internal sealed class RepositorioDeCredenciales(
    CadenaDuena cadena, CifradoDeCredenciales cifrado, TimeProvider reloj)
{
    /// <summary>The shortest key accepted: below this, a 4-character hint would reveal most of it.</summary>
    private const int LongitudMinimaDeLaClave = 8;

    private const int LongitudDeLaPista = 4;

    /// <summary>Hint and metadata for every company that has a key. No key text.</summary>
    public async Task<IReadOnlyList<EstadoDeCredencial>> ListarAsync(CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        await using var comando = new NpgsqlCommand(
            """
            SELECT empresa, pista, validada_en, actualizado_en
              FROM asistente.credencial_de_proveedor
             ORDER BY empresa
            """, conexion);

        var estados = new List<EstadoDeCredencial>();
        await using var lector = await comando.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
        {
            estados.Add(new EstadoDeCredencial(
                lector.GetString(0),
                lector.GetString(1),
                lector.IsDBNull(2) ? null : lector.GetFieldValue<DateTimeOffset>(2),
                lector.GetFieldValue<DateTimeOffset>(3)));
        }

        return estados;
    }

    /// <summary>
    /// Stores or replaces the key of <paramref name="empresa"/>, encrypted. Returns
    /// the hint, the only thing a caller may show.
    /// </summary>
    /// <exception cref="ClaveMaestraNoDisponibleException">No usable master key (the endpoint answers 503).</exception>
    public async Task<string> GuardarAsync(string empresa, string clave, Guid actor, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(empresa);
        ArgumentNullException.ThrowIfNull(clave);

        // Master key first: with none configured the answer is "unavailable",
        // whatever the input looks like.
        var limpia = clave.Trim();
        var cifrada = limpia.Length < LongitudMinimaDeLaClave
            ? throw ClaveInvalida()
            : cifrado.Cifrar(empresa, limpia);

        var pista = limpia[^LongitudDeLaPista..];

        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        // validada_en goes back to NULL: a replaced key has not been tested yet.
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.credencial_de_proveedor
                (empresa, clave_cifrada, pista, validada_en, actor_id, actualizado_en)
            VALUES (@empresa, @cifrada, @pista, NULL, @actor, @ahora)
            ON CONFLICT (empresa) DO UPDATE
                SET clave_cifrada = @cifrada, pista = @pista, validada_en = NULL,
                    actor_id = @actor, actualizado_en = @ahora
            """, conexion);
        comando.Parameters.AddWithValue("empresa", empresa);
        comando.Parameters.AddWithValue("cifrada", cifrada);
        comando.Parameters.AddWithValue("pista", pista);
        comando.Parameters.AddWithValue("actor", actor);
        comando.Parameters.AddWithValue("ahora", reloj.GetUtcNow());
        await comando.ExecuteNonQueryAsync(ct);

        return pista;
    }

    /// <summary>Records that the stored key of <paramref name="empresa"/> passed a connection test.</summary>
    public async Task RegistrarValidacionAsync(string empresa, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(empresa);

        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        await using var comando = new NpgsqlCommand(
            "UPDATE asistente.credencial_de_proveedor SET validada_en = @ahora WHERE empresa = @empresa",
            conexion);
        comando.Parameters.AddWithValue("empresa", empresa);
        comando.Parameters.AddWithValue("ahora", reloj.GetUtcNow());
        await comando.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Opens the key of <paramref name="empresa"/> to send it to its provider, or
    /// null when none is stored. A value still under the previous master key is
    /// stored again under the current one (lazily, on this read).
    /// </summary>
    /// <remarks>
    /// The re-encryption is a compare-and-swap on the ciphertext just read: if an
    /// administrator replaced the key meanwhile, the update touches no row and the
    /// newer value stands. A failed re-encryption never fails the read.
    /// </remarks>
    /// <exception cref="ClaveMaestraNoDisponibleException">No usable master key.</exception>
    /// <exception cref="CredencialIlegibleException">The stored value cannot be opened.</exception>
    public async Task<CredencialAbierta?> AbrirAsync(string empresa, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(empresa);

        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        string almacenada;
        await using (var leer = new NpgsqlCommand(
            "SELECT clave_cifrada FROM asistente.credencial_de_proveedor WHERE empresa = @empresa",
            conexion))
        {
            leer.Parameters.AddWithValue("empresa", empresa);
            if (await leer.ExecuteScalarAsync(ct) is not string valor)
            {
                return null;
            }

            almacenada = valor;
        }

        var abierta = cifrado.Descifrar(empresa, almacenada);

        if (abierta.NecesitaRecifrado)
        {
            await using var recifrar = new NpgsqlCommand(
                """
                UPDATE asistente.credencial_de_proveedor
                   SET clave_cifrada = @nueva, actualizado_en = @ahora
                 WHERE empresa = @empresa AND clave_cifrada = @vieja
                """, conexion);
            recifrar.Parameters.AddWithValue("empresa", empresa);
            recifrar.Parameters.AddWithValue("vieja", almacenada);
            recifrar.Parameters.AddWithValue("nueva", cifrado.Cifrar(empresa, abierta.Clave));
            recifrar.Parameters.AddWithValue("ahora", reloj.GetUtcNow());
            await recifrar.ExecuteNonQueryAsync(ct);
        }

        return new CredencialAbierta(empresa, abierta.Clave);
    }

    private static ArgumentException ClaveInvalida() =>
        new($"The key must have at least {LongitudMinimaDeLaClave} characters.", "clave");
}
