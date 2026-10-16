using System.Globalization;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>Los valores que existen para una columna de catálogo cerrado.</summary>
internal sealed record VocabularioDeUnaColumna(
    string Esquema,
    string Tabla,
    string Columna,
    IReadOnlyList<string> Valores)
{
    /// <summary>Nombre cualificado, tal como se escribe en el prompt.</summary>
    public string Cualificado => $"{Esquema}.{Tabla}.{Columna}";
}

/// <summary>
/// Lee los valores de los catálogos cerrados, para que el modelo no los adivine.
/// </summary>
/// <remarks>
/// <b>EL PROBLEMA QUE RESUELVE, con el caso que lo motivó.</b> Alguien preguntó
/// por «ingeniería informática» y el modelo copió esas palabras al <c>WHERE</c>.
/// La carrera se llama «Ingeniería <b>en</b> Informática», así que la consulta
/// volvió vacía y el turno respondió que no había nada. Ni el motor ni el
/// validador tienen forma de notarlo: el SQL era válido y el resultado, cero
/// filas legítimas.
///
/// Un literal que el modelo inventa a partir de lo que escribió el usuario es
/// una adivinanza. Con la lista de valores adelante deja de serlo, que es la
/// misma idea con la que el catálogo de intenciones saca su vocabulario de
/// <c>pg_constraint</c> en lugar de escribirlo a mano.
///
/// <b>Por qué una lista declarada y no una detección automática.</b> Enumerar
/// «las tablas chicas» funcionaría hoy y sería una fuga mañana: la tabla que hoy
/// tiene ocho filas puede tener apellidos el mes que viene, y el vocabulario
/// viaja entero al proveedor del modelo dentro del prefijo. La lista de acá es
/// corta, se revisa en el diff, y hay un test que falla si alguien mete una
/// columna de otra tabla.
/// </remarks>
internal static class LectorDeValoresDeCatalogo
{
    /// <summary>
    /// Tope de valores por columna.
    /// </summary>
    /// <remarks>
    /// No es un presupuesto de tokens: es la definición de «catálogo cerrado». Una
    /// columna que lo pasa dejó de serlo, y hay que sacarla de la lista en vez de
    /// subir el número.
    /// </remarks>
    internal const int MaximoDeValores = 40;

    /// <summary>
    /// Las columnas cuyos valores viajan al prompt.
    /// </summary>
    /// <remarks>
    /// <b>Sólo catálogos genuinamente cerrados y sin datos personales.</b> Las
    /// carreras de un departamento son media docena y los cargos docentes los fija
    /// la normativa: las dos listas cambian con un acto administrativo, no con el
    /// uso del sistema.
    ///
    /// <b><c>identity.materias</c> queda afuera a propósito</b>, aunque hoy tenga
    /// seis filas y sea lo que más se nombra en las preguntas. No es un catálogo
    /// cerrado —un departamento suma materias— y enumerarla sería empezar a
    /// depender de que nunca crezca. Para ésas está la regla 8 del prompt, que
    /// pide comparar por la palabra distintiva en vez de por igualdad.
    /// </remarks>
    internal static readonly IReadOnlyList<(string Esquema, string Tabla, string Columna)>
        CatalogosCerrados =
        [
            ("identity", "carreras", "code"),
            ("identity", "carreras", "name"),
            ("designaciones", "cargos", "codigo"),
            ("designaciones", "cargos", "nombre"),
        ];

    /// <summary>
    /// Lee los valores que la conexión actual puede leer.
    /// </summary>
    /// <remarks>
    /// Una columna que este rol no puede leer se omite en silencio, igual que el
    /// resto del esquema: el rol básico y el de datos personales obtienen prefijos
    /// distintos sin que este código sepa de ellos. Que las columnas esperadas
    /// estén presentes lo verifica un test contra la base real.
    /// </remarks>
    internal static async Task<IReadOnlyList<VocabularioDeUnaColumna>> LeerAsync(
        NpgsqlConnection conexion, CancellationToken ct)
    {
        var vocabularios = new List<VocabularioDeUnaColumna>();

        foreach (var (esquema, tabla, columna) in CatalogosCerrados)
        {
            var valores = await LeerColumnaAsync(conexion, esquema, tabla, columna, ct);

            if (valores is not null)
            {
                vocabularios.Add(new VocabularioDeUnaColumna(esquema, tabla, columna, valores));
            }
        }

        return vocabularios;
    }

    /// <summary>
    /// Los valores de una columna, o <c>null</c> si no se puede o no corresponde.
    /// </summary>
    /// <remarks>
    /// <b>Devuelve <c>null</c> —y no una lista recortada— cuando hay más valores
    /// que el tope.</b> Una lista parcial es peor que ninguna: el prompt la
    /// presenta como el conjunto completo, así que un valor que quedó afuera del
    /// recorte pasa a no existir para el modelo, y una pregunta legítima sobre él
    /// se responde con una abstención convencida.
    ///
    /// El orden es <c>COLLATE "C"</c> y no el del locale: el prefijo tiene que ser
    /// idéntico byte a byte entre procesos, y una base con otra configuración
    /// regional cambiaría el orden —y con él la huella— sin que cambie ningún dato.
    /// </remarks>
    private static async Task<IReadOnlyList<string>?> LeerColumnaAsync(
        NpgsqlConnection conexion,
        string esquema,
        string tabla,
        string columna,
        CancellationToken ct)
    {
        // Los identificadores no pueden ir como parámetros, así que se interpolan.
        // Salen de `CatalogosCerrados`, que es una constante del ensamblado y nunca
        // de una pregunta; `SoloIdentificadoresSimples` lo verifica en un test.
        var sql = string.Create(
            CultureInfo.InvariantCulture,
            $"""
             SELECT DISTINCT "{columna}"::text COLLATE "C" AS valor
               FROM "{esquema}"."{tabla}"
              WHERE "{columna}" IS NOT NULL
              ORDER BY 1
              LIMIT {MaximoDeValores + 1}
             """);

        var valores = new List<string>();

        try
        {
            await using var comando = new NpgsqlCommand(sql, conexion);
            await using var lector = await comando.ExecuteReaderAsync(ct);

            while (await lector.ReadAsync(ct))
            {
                valores.Add(lector.GetString(0));
            }
        }
        catch (PostgresException)
        {
            // Sin privilegio, o la tabla todavía no existe en este ambiente. Es el
            // mismo criterio que el resto del prefijo: se describe lo que el rol
            // puede leer y nada más.
            return null;
        }

        // Se leyó uno de más justamente para poder distinguir «entran todos» de
        // «hay más»: con LIMIT exacto las dos situaciones devuelven lo mismo.
        return valores.Count > MaximoDeValores ? null : valores;
    }
}
