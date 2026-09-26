using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Memory;

namespace ArsDocendi.Host.Desarrollo;

/// <summary>
/// Suma a la configuración los valores del archivo <c>.env</c> de la raíz del
/// repositorio, en desarrollo y sólo en desarrollo.
/// </summary>
/// <remarks>
/// <b>Existe para que la clave del proveedor se pueda probar sin exportarla a
/// mano en cada terminal.</b> Es el mismo archivo que lee <c>docker compose</c>,
/// así que la base y el asistente se configuran en un solo lugar y no en dos.
///
/// <b>Sólo en Development, y eso es la mitad del diseño.</b> En los ambientes
/// desplegados la credencial la inyecta la infraestructura
/// (<c>infra/scripts/spin-up.sh</c>) y no hay ningún <c>.env</c> que leer. Un
/// lector activo allá sería un segundo camino hacia la credencial —más débil, y
/// preferido en silencio si alguien deja un archivo olvidado en el working
/// directory—. Staging queda afuera a propósito aunque no sea producción: es un
/// ambiente desplegado, y ahí un archivo en disco no tiene por qué ganarle a lo
/// que puso la infra.
///
/// <b>Pierde contra las variables de ambiente reales.</b> La fuente se inserta
/// ANTES de la de variables de ambiente, así que un <c>export</c> en la terminal
/// sigue mandando sobre el archivo. Al revés, un <c>.env</c> viejo pisaría en
/// silencio lo que alguien acaba de exportar para probar, que es exactamente el
/// tipo de sorpresa que hace perder una tarde. Y gana contra
/// <c>appsettings.json</c>, porque para eso se escribe.
///
/// <b>El archivo nunca se versiona.</b> <c>.gitignore</c> lo excluye desde antes
/// de que esto existiera; acá no se agrega ninguna garantía nueva, sólo se
/// depende de ésa.
/// </remarks>
public static class ArchivoDeEntorno
{
    /// <summary>Nombre del archivo, igual al que lee <c>docker compose</c>.</summary>
    public const string Nombre = ".env";

    /// <summary>
    /// Archivo que marca la raíz del repositorio.
    /// </summary>
    /// <remarks>
    /// Se busca junto al <c>.env</c> y no solo: subir hasta el primer <c>.env</c>
    /// que aparezca haría que un archivo suelto en cualquier directorio padre
    /// —el home del desarrollador, sin ir más lejos— entrara a la configuración
    /// del Host. El ancla lo ata al <c>.env</c> de ESTE proyecto, que es el mismo
    /// que compose ya usa.
    /// </remarks>
    public const string Ancla = "docker-compose.yml";

    /// <summary>
    /// Tope de directorios que se suben buscando la raíz.
    /// </summary>
    /// <remarks>
    /// Desde <c>backend/src/ArsDocendi.Host</c> la raíz está a tres niveles, y
    /// desde el <c>bin/Debug/net10.0</c> de los tests a seis. Ocho deja margen sin
    /// convertir la búsqueda en un recorrido del filesystem entero.
    /// </remarks>
    private const int NivelesQueSube = 8;

    /// <summary>
    /// Aplica el archivo si corresponde. Devuelve <c>null</c> si no se aplicó.
    /// </summary>
    /// <remarks>
    /// No registra nada por su cuenta: en este punto del arranque todavía no hay
    /// logger, y el proyecto no admite <c>Console.WriteLine</c>. Devuelve qué hizo
    /// para que <c>Program</c> lo registre con Serilog una vez construido el host.
    /// </remarks>
    /// <param name="ambiente">Ambiente de hosting; fuera de Development no hace nada.</param>
    /// <param name="configuracion">Configuración en construcción.</param>
    /// <param name="directorioDeArranque">Desde dónde se empieza a subir.</param>
    public static ArchivoDeEntornoAplicado? Sumar(
        IHostEnvironment ambiente,
        IConfigurationBuilder configuracion,
        string directorioDeArranque)
    {
        ArgumentNullException.ThrowIfNull(ambiente);
        ArgumentNullException.ThrowIfNull(configuracion);

        if (!ambiente.IsDevelopment())
        {
            return null;
        }

        var ruta = Ubicar(directorioDeArranque);

        if (ruta is null)
        {
            return null;
        }

        var valores = Interpretar(File.ReadAllLines(ruta));

        if (valores.Count == 0)
        {
            return new ArchivoDeEntornoAplicado(ruta, 0);
        }

        var fuente = new MemoryConfigurationSource { InitialData = valores };

        // La posición ES el contrato de precedencia. Insertar al final —que es lo
        // que hace Add— pondría al archivo por encima de las variables reales.
        var indiceDeVariables = IndiceDeVariablesDeAmbiente(configuracion.Sources);

        if (indiceDeVariables < 0)
        {
            configuracion.Sources.Add(fuente);
        }
        else
        {
            configuracion.Sources.Insert(indiceDeVariables, fuente);
        }

        return new ArchivoDeEntornoAplicado(ruta, valores.Count);
    }

    /// <summary>
    /// Ruta del <c>.env</c> de la raíz del repositorio, o <c>null</c> si no hay.
    /// </summary>
    public static string? Ubicar(string directorioDeArranque)
    {
        if (string.IsNullOrWhiteSpace(directorioDeArranque))
        {
            return null;
        }

        var directorio = new DirectoryInfo(directorioDeArranque);

        for (var nivel = 0; nivel <= NivelesQueSube && directorio is not null; nivel++)
        {
            var candidato = Path.Combine(directorio.FullName, Nombre);

            if (File.Exists(candidato)
                && File.Exists(Path.Combine(directorio.FullName, Ancla)))
            {
                return candidato;
            }

            directorio = directorio.Parent;
        }

        return null;
    }

    /// <summary>
    /// Traduce las líneas del archivo a claves de configuración.
    /// </summary>
    /// <remarks>
    /// <b>La traducción de <c>__</c> a <c>:</c> es la que ya hace .NET con las
    /// variables de ambiente</b>, así que lo que se escribe en el archivo es
    /// idéntico a lo que se exportaría en la terminal: se copia y pega una línea
    /// entre los dos lados sin traducir nada.
    ///
    /// <b>Las reglas de comillas y de comentario al final de línea son las de
    /// docker compose</b>, que lee este mismo archivo. Dos lectores del mismo
    /// archivo que entiendan distinto el mismo renglón es una trampa que sólo se
    /// descubre cuando un valor llega cortado.
    ///
    /// Las claves que no son de configuración de .NET —<c>POSTGRES_USER</c> y
    /// compañía, que están ahí para compose— entran igual y no molestan: nadie las
    /// pide, y filtrarlas exigiría una lista que habría que mantener.
    /// </remarks>
    public static Dictionary<string, string?> Interpretar(IEnumerable<string> lineas)
    {
        ArgumentNullException.ThrowIfNull(lineas);

        var valores = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var cruda in lineas)
        {
            var linea = cruda.Trim();

            if (linea.Length == 0 || linea[0] == '#')
            {
                continue;
            }

            if (linea.StartsWith("export ", StringComparison.Ordinal))
            {
                linea = linea["export ".Length..].TrimStart();
            }

            var corte = linea.IndexOf('=', StringComparison.Ordinal);

            if (corte <= 0)
            {
                continue;
            }

            var clave = linea[..corte].TrimEnd();

            if (clave.Length == 0)
            {
                continue;
            }

            valores[clave.Replace("__", ":", StringComparison.Ordinal)] =
                LimpiarValor(linea[(corte + 1)..].Trim());
        }

        return valores;
    }

    /// <summary>
    /// Saca las comillas envolventes, o el comentario final si no las hay.
    /// </summary>
    /// <remarks>
    /// El comentario se saca SÓLO en valores sin comillas y sólo cuando el
    /// <c>#</c> viene después de un espacio: sin esa condición, una credencial que
    /// contuviera un numeral quedaría cortada en silencio, que es el peor de los
    /// modos de falla posibles para un secreto. Entre comillas no se toca nada.
    /// </remarks>
    private static string LimpiarValor(string valor)
    {
        if (valor.Length >= 2
            && (valor[0] == '"' || valor[0] == '\'')
            && valor[^1] == valor[0])
        {
            return valor[1..^1];
        }

        for (var i = 1; i < valor.Length; i++)
        {
            if (valor[i] == '#' && char.IsWhiteSpace(valor[i - 1]))
            {
                return valor[..i].TrimEnd();
            }
        }

        return valor;
    }

    /// <summary>
    /// Posición de la primera fuente de variables de ambiente.
    /// </summary>
    private static int IndiceDeVariablesDeAmbiente(IList<IConfigurationSource> fuentes)
    {
        for (var i = 0; i < fuentes.Count; i++)
        {
            if (fuentes[i] is EnvironmentVariablesConfigurationSource)
            {
                return i;
            }
        }

        return -1;
    }
}

/// <summary>Qué archivo se aplicó y cuántas claves trajo. Nunca los valores.</summary>
public sealed record ArchivoDeEntornoAplicado(string Ruta, int Claves);
