using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ArsDocendi.Shared.Auditing;

/// <summary>Snapshot local de un evento; no se serializa ni publica como manifiesto externo.</summary>
public sealed record EventoAuditoriaSellable(
    long Id,
    long SealSeq,
    string SchemaName,
    string TableName,
    string RowPk,
    string Action,
    string? OldRowJson,
    string? NewRowJson,
    IReadOnlyList<string>? ChangedColumns,
    string? ChangedBy,
    DateTimeOffset ChangedAt,
    string? RequestId,
    string? ClientIp);

/// <summary>Canonicaliza eventos locales y calcula el digest versionado de un lote.</summary>
public static class HasherLotesAuditoria
{
    public const int VersionFormato = 1;
    private static readonly byte[] Dominio = "ARS-DOCENDI-AUDIT-BATCH\0"u8.ToArray();

    public static byte[] CalcularHash(
        string ambiente,
        long primeraSecuencia,
        long ultimaSecuencia,
        Guid nonce,
        IReadOnlyCollection<EventoAuditoriaSellable> eventos,
        byte[]? hashLoteAnterior = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ambiente);
        ArgumentNullException.ThrowIfNull(eventos);
        if (hashLoteAnterior is { Length: not 32 })
            throw new ArgumentException("El hash del lote anterior debe tener 32 bytes.", nameof(hashLoteAnterior));
        if (primeraSecuencia <= 0 || ultimaSecuencia < primeraSecuencia - 1)
            throw new ArgumentOutOfRangeException(nameof(primeraSecuencia), "Rango de cursor inválido.");
        if (eventos.Count != ultimaSecuencia - primeraSecuencia + 1)
            throw new InvalidDataException("La cantidad de eventos no coincide con el rango declarado.");

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Dominio);
        EscribirEntero(hash, VersionFormato);
        EscribirTexto(hash, ambiente);
        EscribirEntero(hash, primeraSecuencia);
        EscribirEntero(hash, ultimaSecuencia);
        EscribirBytes(hash, Encoding.ASCII.GetBytes(nonce.ToString("N")));
        if (hashLoteAnterior is null) EscribirEntero(hash, -1);
        else EscribirBytes(hash, hashLoteAnterior);

        var ordenados = eventos.OrderBy(e => e.SealSeq).ToArray();
        for (var indice = 0; indice < ordenados.Length; indice++)
        {
            var evento = ordenados[indice];
            var esperado = primeraSecuencia + indice;
            if (evento.SealSeq != esperado)
                throw new InvalidDataException($"Falta o se duplica la secuencia {esperado}.");

            EscribirEntero(hash, evento.Id);
            EscribirEntero(hash, evento.SealSeq);
            EscribirTexto(hash, evento.SchemaName);
            EscribirTexto(hash, evento.TableName);
            EscribirTexto(hash, evento.RowPk);
            EscribirTexto(hash, evento.Action);
            EscribirJson(hash, evento.OldRowJson);
            EscribirJson(hash, evento.NewRowJson);
            EscribirColumnas(hash, evento.ChangedColumns);
            EscribirNullable(hash, evento.ChangedBy);
            EscribirTexto(hash, evento.ChangedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            EscribirNullable(hash, evento.RequestId);
            EscribirNullable(hash, evento.ClientIp);
        }

        return hash.GetHashAndReset();
    }

    public static byte[] CalcularHashJson(string dominio, Guid nonce, string json)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        EscribirTexto(hash, dominio);
        EscribirTexto(hash, nonce.ToString("N"));
        EscribirJson(hash, json);
        return hash.GetHashAndReset();
    }

    private static void EscribirJson(IncrementalHash hash, string? json)
    {
        if (json is null)
        {
            EscribirEntero(hash, -1);
            return;
        }

        using var documento = JsonDocument.Parse(json);
        using var buffer = new MemoryStream();
        using (var escritor = new Utf8JsonWriter(buffer))
            EscribirElemento(escritor, documento.RootElement);
        EscribirBytes(hash, buffer.ToArray());
    }

    private static void EscribirElemento(Utf8JsonWriter escritor, JsonElement elemento)
    {
        switch (elemento.ValueKind)
        {
            case JsonValueKind.Object:
                escritor.WriteStartObject();
                var propiedades = elemento.EnumerateObject()
                    .OrderBy(p => p.Name, StringComparer.Ordinal)
                    .ToArray();
                for (var indice = 1; indice < propiedades.Length; indice++)
                {
                    if (string.Equals(propiedades[indice - 1].Name, propiedades[indice].Name, StringComparison.Ordinal))
                        throw new InvalidDataException("El snapshot JSON contiene una clave duplicada.");
                }
                foreach (var propiedad in propiedades)
                {
                    escritor.WritePropertyName(propiedad.Name);
                    EscribirElemento(escritor, propiedad.Value);
                }
                escritor.WriteEndObject();
                break;
            case JsonValueKind.Array:
                escritor.WriteStartArray();
                foreach (var valor in elemento.EnumerateArray()) EscribirElemento(escritor, valor);
                escritor.WriteEndArray();
                break;
            case JsonValueKind.Number:
                escritor.WriteRawValue(CanonicalizarNumero(elemento), skipInputValidation: false);
                break;
            default:
                elemento.WriteTo(escritor);
                break;
        }
    }

    private static string CanonicalizarNumero(JsonElement elemento)
    {
        // Trabajar sobre el léxico JSON: decimal/double redondean valores válidos
        // de PostgreSQL numeric y pueden ocultar una alteración del snapshot.
        var texto = elemento.GetRawText();
        var negativo = texto.StartsWith('-');
        if (negativo) texto = texto[1..];
        var partes = texto.Split(['e', 'E']);
        var mantisa = partes[0];
        var exponente = partes.Length == 2
            ? System.Numerics.BigInteger.Parse(partes[1], CultureInfo.InvariantCulture)
            : System.Numerics.BigInteger.Zero;
        var punto = mantisa.IndexOf('.');
        if (punto >= 0) exponente -= mantisa.Length - punto - 1;
        var digitos = mantisa.Replace(".", string.Empty).TrimStart('0');
        if (digitos.Length == 0) return "0";
        var sinCeros = digitos.TrimEnd('0');
        exponente += digitos.Length - sinCeros.Length;
        var signo = negativo ? "-" : string.Empty;
        return signo + sinCeros + (exponente.IsZero ? string.Empty : "e" + exponente.ToString(CultureInfo.InvariantCulture));
    }

    private static void EscribirColumnas(IncrementalHash hash, IReadOnlyList<string>? columnas)
    {
        if (columnas is null)
        {
            EscribirEntero(hash, -1);
            return;
        }

        var ordenadas = columnas.OrderBy(c => c, StringComparer.Ordinal).ToArray();
        for (var indice = 1; indice < ordenadas.Length; indice++)
        {
            if (string.Equals(ordenadas[indice - 1], ordenadas[indice], StringComparison.Ordinal))
                throw new InvalidDataException("El evento contiene columnas cambiadas duplicadas.");
        }
        EscribirEntero(hash, ordenadas.Length);
        foreach (var columna in ordenadas) EscribirTexto(hash, columna);
    }

    private static void EscribirNullable(IncrementalHash hash, string? valor)
    {
        if (valor is null)
        {
            EscribirEntero(hash, -1);
            return;
        }
        EscribirTexto(hash, valor);
    }

    private static void EscribirTexto(IncrementalHash hash, string valor) =>
        EscribirBytes(hash, Encoding.UTF8.GetBytes(valor));

    private static void EscribirBytes(IncrementalHash hash, byte[] bytes)
    {
        EscribirEntero(hash, bytes.Length);
        hash.AppendData(bytes);
    }

    private static void EscribirEntero(IncrementalHash hash, long valor)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, valor);
        hash.AppendData(bytes);
    }
}
