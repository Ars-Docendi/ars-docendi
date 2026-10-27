using System.Text.Encodings.Web;
using System.Text.Json;

namespace Modules.Asistente.Infrastructure;

/// <summary>Un cassette leído del disco.</summary>
internal sealed record CassetteEnDisco(string Archivo, SelloDelCassette Sello, string Cuerpo);

/// <summary>Lee y escribe los cassettes de un directorio.</summary>
internal sealed class AlmacenDeCassettes(string directorio)
{
    /// <summary>Extensión de los archivos de cassette.</summary>
    private const string Extension = ".json";

    /// <summary>Sufijo del archivo temporal de una escritura en curso.</summary>
    private const string Parcial = ".parcial";

    /// <summary>Directorio donde viven los cassettes.</summary>
    public string Directorio => directorio;

    /// <summary>Lee el cassette de una clave, o nulo si no hay ninguno.</summary>
    public CassetteEnDisco? Leer(string clave)
    {
        var ruta = Ruta(clave);

        return File.Exists(ruta) ? Interpretar(ruta) : null;
    }

    /// <summary>El cuerpo grabado para una clave, verificado contra lo vigente.</summary>
    public string? Reproducir(
        string clave, string hashDelPrefijoVigente, string hashDelFixtureVigente)
    {
        var cassette = Leer(clave);

        if (cassette is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(hashDelFixtureVigente))
        {
            throw new InvalidOperationException(
                $"No se puede servir el cassette '{cassette.Archivo}': no se sabe cuál es el "
                + "hash del fixture vigente, así que no hay contra qué verificar que se grabó "
                + "contra datos sintéticos.");
        }

        if (!string.Equals(
                cassette.Sello.HashDelPrefijo, hashDelPrefijoVigente, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"El cassette '{cassette.Archivo}' se grabó con otro prefijo del esquema "
                + $"({cassette.Sello.HashDelPrefijo}, y el vigente es {hashDelPrefijoVigente}). "
                + "No se sirve: la respuesta que guarda la dio el modelo sobre otras columnas. "
                + "Volvé a grabarlo.");
        }

        if (!string.Equals(
                cassette.Sello.HashDelFixture, hashDelFixtureVigente, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"El cassette '{cassette.Archivo}' se grabó contra otro fixture "
                + $"({cassette.Sello.HashDelFixture}, y el vigente es {hashDelFixtureVigente}). "
                + "No se sirve: una respuesta grabada sobre otros datos describe otro sistema. "
                + "Volvé a grabarlo.");
        }

        return cassette.Cuerpo;
    }

    /// <summary>Por qué no hay cassette para una clave, dicho de forma accionable.</summary>
    public string ExplicarAusencia(string clave, string hashDelPrefijoVigente)
    {
        var presentes = HashesDePrefijoPresentes();

        var mensaje =
            $"No hay cassette para la clave '{clave}' en '{directorio}', y la re-grabación "
            + "no está puesta: no se emite ninguna llamada de red.";

        return presentes.Count > 0
            && !presentes.Contains(hashDelPrefijoVigente, StringComparer.Ordinal)
                ? mensaje
                    + " Los cassettes que hay son todos de otro prefijo del esquema "
                    + $"({string.Join(", ", presentes)}, y el vigente es "
                    + $"{hashDelPrefijoVigente}): hay que volver a grabarlos."
                : mensaje;
    }

    /// <summary>Escribe el cassette de una clave, pisando el que hubiera.</summary>
    public void Escribir(string clave, SelloDelCassette sello, string cuerpo)
    {
        ArgumentNullException.ThrowIfNull(sello);

        var vacios = sello.CamposVacios();

        if (vacios.Count > 0)
        {
            throw new InvalidOperationException(
                $"No se puede grabar el cassette '{clave}{Extension}': el sello no trae "
                + $"{string.Join(", ", vacios)}. Un cassette sin sello completo no se puede "
                + "verificar contra el prefijo ni contra el fixture vigentes, así que no se "
                + "escribe.");
        }

        Directory.CreateDirectory(directorio);

        var definitivo = Ruta(clave);
        var temporal = definitivo + Parcial;

        try
        {
            File.WriteAllBytes(temporal, Sobre(sello, cuerpo));
            File.Move(temporal, definitivo, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporal))
            {
                File.Delete(temporal);
            }

            throw;
        }
    }

    /// <summary>Las huellas de prefijo que declaran los cassettes del directorio.</summary>
    public IReadOnlyList<string> HashesDePrefijoPresentes() =>
        !Directory.Exists(directorio)
            ? []
            : [.. Directory.EnumerateFiles(directorio, "*" + Extension)
                .Select(ruta => Interpretar(ruta).Sello.HashDelPrefijo)
                .Distinct(StringComparer.Ordinal)];

    /// <summary>Los cassettes del directorio, en orden de nombre de archivo.</summary>
    public IReadOnlyList<CassetteEnDisco> Todos() =>
        !Directory.Exists(directorio)
            ? []
            : [.. Directory.EnumerateFiles(directorio, "*" + Extension)
                .OrderBy(ruta => ruta, StringComparer.Ordinal)
                .Select(Interpretar)];

    private string Ruta(string clave) => Path.Combine(directorio, clave + Extension);

    private static CassetteEnDisco Interpretar(string ruta)
    {
        var archivo = Path.GetFileName(ruta);

        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(File.ReadAllText(ruta));
        }
        catch (JsonException excepcion)
        {
            throw new InvalidOperationException(
                $"El cassette '{archivo}' no es un sobre JSON válido.", excepcion);
        }

        using (documento)
        {
            var raiz = documento.RootElement;

            var sello = new SelloDelCassette(
                Campo(raiz, SelloDelCassette.CampoModelo),
                Campo(raiz, SelloDelCassette.CampoFecha),
                Campo(raiz, SelloDelCassette.CampoHashDelPrefijo),
                Campo(raiz, SelloDelCassette.CampoHashDelFixture));

            var vacios = sello.CamposVacios();

            if (vacios.Count > 0)
            {
                throw new InvalidOperationException(
                    $"El cassette '{archivo}' no se puede servir: le falta "
                    + $"{string.Join(", ", vacios)} en el sello. Volvé a grabarlo.");
            }

            if (!raiz.TryGetProperty(SelloDelCassette.CampoCuerpo, out var cuerpo)
                || cuerpo.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException(
                    $"El cassette '{archivo}' no se puede servir: le falta "
                    + $"'{SelloDelCassette.CampoCuerpo}'. Volvé a grabarlo.");
            }

            return new CassetteEnDisco(archivo, sello, cuerpo.GetString() ?? string.Empty);
        }
    }

    private static string Campo(JsonElement raiz, string nombre) =>
        raiz.ValueKind == JsonValueKind.Object
        && raiz.TryGetProperty(nombre, out var valor)
        && valor.ValueKind == JsonValueKind.String
            ? valor.GetString() ?? string.Empty
            : string.Empty;

    /// <summary>Arma el sobre: sello arriba, cuerpo verbatim abajo.</summary>
    private static byte[] Sobre(SelloDelCassette sello, string cuerpo)
    {
        using var memoria = new MemoryStream();
        using (var escritor = new Utf8JsonWriter(
            memoria,
            new JsonWriterOptions
            {
                Indented = true,

                // Sin escapes defensivos de caracteres que acá no son peligrosos: el sobre es un
                // archivo del repositorio y no HTML, y un cuerpo con los acentos escritos como
                // secuencias de escape se revisa mucho peor.
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }))
        {
            escritor.WriteStartObject();
            escritor.WriteString(SelloDelCassette.CampoModelo, sello.Modelo);
            escritor.WriteString(SelloDelCassette.CampoFecha, sello.Fecha);
            escritor.WriteString(SelloDelCassette.CampoHashDelPrefijo, sello.HashDelPrefijo);
            escritor.WriteString(SelloDelCassette.CampoHashDelFixture, sello.HashDelFixture);
            escritor.WriteString(SelloDelCassette.CampoCuerpo, cuerpo);
            escritor.WriteEndObject();
        }

        // Con salto final, como cualquier otro archivo de texto del repositorio.
        memoria.WriteByte((byte)'\n');

        return memoria.ToArray();
    }
}
