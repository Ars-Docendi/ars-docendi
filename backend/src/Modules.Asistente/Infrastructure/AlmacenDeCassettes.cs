using System.Text.Encodings.Web;
using System.Text.Json;

namespace Modules.Asistente.Infrastructure;

/// <summary>Un cassette leído del disco.</summary>
/// <param name="Archivo">Nombre del archivo, para poder nombrarlo en un error.</param>
/// <param name="Sello">La identidad de la corrida que lo produjo.</param>
/// <param name="Cuerpo">El cuerpo de la respuesta del proveedor, tal como llegó.</param>
internal sealed record CassetteEnDisco(string Archivo, SelloDelCassette Sello, string Cuerpo);

/// <summary>
/// Lee y escribe los cassettes de un directorio.
/// </summary>
/// <remarks>
/// El sobre es un JSON con el sello arriba y el cuerpo de la respuesta como cadena
/// <b>verbatim</b> abajo, sin reindentar ni reordenar.
///
/// <b>Verbatim y no embebido como JSON parseado</b>, aunque se lea muchísimo peor
/// en un diff. Un cuerpo reserializado es un registro de <b>nuestro</b>
/// serializador y no del suyo: el día que el proveedor cambie el orden de las
/// claves, agregue un campo o mande un escape distinto, el cassette lo taparía — y
/// eso es exactamente lo que la fixture existe para no tapar. El costo se acota
/// poniendo el sello primero, así lo que un revisor necesita mirar está antes del
/// muro de texto.
/// </remarks>
internal sealed class AlmacenDeCassettes(string directorio)
{
    /// <summary>Extensión de los archivos de cassette.</summary>
    private const string Extension = ".json";

    /// <summary>Sufijo del archivo temporal de una escritura en curso.</summary>
    private const string Parcial = ".parcial";

    /// <summary>Directorio donde viven los cassettes.</summary>
    public string Directorio => directorio;

    /// <summary>
    /// Lee el cassette de una clave, o nulo si no hay ninguno.
    /// </summary>
    /// <remarks>
    /// «No está» y «está roto» se distinguen a propósito: la ausencia es la que deja
    /// al handler decidir entre grabar y fallar cerrado, y un archivo ilegible tiene
    /// que gritar en lugar de hacerse pasar por ausencia y disparar una llamada de
    /// red que nadie pidió.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Si el archivo existe pero no se puede interpretar o le falta un campo.
    /// </exception>
    public CassetteEnDisco? Leer(string clave)
    {
        var ruta = Ruta(clave);

        return File.Exists(ruta) ? Interpretar(ruta) : null;
    }

    /// <summary>
    /// El cuerpo grabado para una clave, verificado contra lo vigente.
    /// </summary>
    /// <remarks>
    /// La verificación va ANTES de devolver nada. Un cassette es una respuesta
    /// grabada contra <b>un</b> esquema y <b>un</b> fixture; servirlo contra otros
    /// dos no es reproducir, es contestar una pregunta que ya no es la misma con la
    /// ventaja de que nada falla mientras tanto.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Si el sello no corresponde al prefijo o al fixture vigentes, o si no se sabe
    /// cuál es el fixture vigente: un cassette que no se puede verificar es
    /// indistinguible de uno grabado contra una base con datos importados.
    /// </exception>
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

    /// <summary>
    /// Por qué no hay cassette para una clave, dicho de forma accionable.
    /// </summary>
    /// <remarks>
    /// «Falta este cassette» y «los cassettes que hay son de otro prefijo» mandan a
    /// hacer cosas distintas —grabar esa pregunta, o regrabarlas todas—, y sin la
    /// distinción el segundo caso se ve igual que el primero y alguien termina
    /// grabando de a una para siempre.
    /// </remarks>
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

    /// <summary>
    /// Escribe el cassette de una clave, pisando el que hubiera.
    /// </summary>
    /// <remarks>
    /// El sello se valida ANTES de tocar el disco y la escritura pasa por un
    /// temporal que recién al final se mueve al nombre definitivo. Un cassette
    /// parcial es peor que ninguno: se encuentra por clave y falla recién al
    /// interpretarlo, mucho después de la corrida que lo dejó así.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Si al sello le falta un campo.</exception>
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

    /// <summary>
    /// Las huellas de prefijo que declaran los cassettes del directorio.
    /// </summary>
    /// <remarks>
    /// Es lo que permite distinguir «falta este cassette» de «los cassettes que hay
    /// son de otro prefijo». Los dos diagnósticos mandan a hacer cosas distintas y
    /// el segundo, sin este dato, se ve igual que el primero.
    /// </remarks>
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

                // Sin escapes defensivos de caracteres que acá no son peligrosos: el
                // sobre es un archivo del repositorio y no HTML, y un cuerpo con los
                // acentos escritos como secuencias de escape se revisa mucho peor.
                // El viaje de ida y vuelta es exacto en las dos codificaciones.
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
