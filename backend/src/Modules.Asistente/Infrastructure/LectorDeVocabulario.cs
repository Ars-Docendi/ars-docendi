using System.Text.RegularExpressions;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// La restricción de una columna no tiene la forma de la que se pueden derivar sus
/// valores admitidos.
/// </summary>
/// <remarks>
/// Existe para que el desajuste sea ruidoso. La alternativa —devolver una lista
/// vacía— es el mismo modo de fallar que este lector existe para evitar: nada se
/// rompe, el resolutor deja de reconocer un vocabulario entero y la única señal es
/// que las preguntas dejan de tomar el camino barato.
/// </remarks>
internal sealed class VocabularioIlegible(string restriccion, string definicion)
    : InvalidOperationException(
        $"La restricción '{restriccion}' no enumera literales, así que no se puede "
        + $"derivar de ella un vocabulario cerrado. Definición: {definicion}")
{
    /// <summary>La restricción que no se pudo leer.</summary>
    public string Restriccion { get; } = restriccion;
}

/// <summary>
/// Lee de la base los vocabularios cerrados del trámite.
/// </summary>
/// <remarks>
/// <b>De la base y no de una lista.</b> El vocabulario del trámite —estados,
/// novedades, tipos de baja— vive en restricciones <c>CHECK</c> sobre la tabla que
/// las declara, y ahí es donde se lo lee. El motivo no es evitar la duplicación
/// sino el modo de fallar de la copia: una lista escrita a mano no se rompe cuando
/// alguien agrega un estado. El resolutor simplemente deja de reconocerlo, la
/// pregunta cae al carril caro y nadie se entera de que había uno más barato.
///
/// Los cargos ya tienen tabla propia, así que se leen de ella.
///
/// Todo va calificado con <c>pg_catalog.</c> y con el esquema escrito, porque los
/// roles del asistente corren con <c>search_path</c> vacío.
/// </remarks>
internal static partial class LectorDeVocabulario
{
    /// <summary>
    /// La definición normalizada de las restricciones de una tabla.
    /// </summary>
    /// <remarks>
    /// Se pide <c>pg_get_constraintdef</c> y no el texto original: PostgreSQL
    /// normaliza <c>IN (...)</c> a <c>= ANY (ARRAY[...])</c>, así que la forma que
    /// se parsea es estable frente a cómo se haya escrito el DDL.
    /// </remarks>
    private const string SqlRestricciones = """
        SELECT c.conname                                   AS restriccion,
               pg_catalog.pg_get_constraintdef(c.oid)      AS definicion
          FROM pg_catalog.pg_constraint c
          JOIN pg_catalog.pg_class     t ON t.oid = c.conrelid
          JOIN pg_catalog.pg_namespace n ON n.oid = t.relnamespace
         WHERE n.nspname = @esquema
           AND t.relname = @tabla
           AND c.contype = 'c'
           AND c.conname = ANY (@restricciones)
        """;

    private const string SqlCargos = """
        SELECT nombre, abreviatura
          FROM designaciones.cargos
         WHERE activo
         ORDER BY orden
        """;

    /// <summary>
    /// Los valores admitidos de cada restricción pedida.
    /// </summary>
    /// <exception cref="VocabularioIlegible">
    /// Si alguna restricción no enumera literales.
    /// </exception>
    public static async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> VocabulariosAsync(
        NpgsqlConnection conexion,
        string esquema,
        string tabla,
        IReadOnlyList<string> restricciones,
        CancellationToken ct)
    {
        var leidos = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        await using var comando = new NpgsqlCommand(SqlRestricciones, conexion);
        comando.Parameters.AddWithValue("esquema", esquema);
        comando.Parameters.AddWithValue("tabla", tabla);
        comando.Parameters.AddWithValue("restricciones", restricciones.ToArray());

        await using (var lector = await comando.ExecuteReaderAsync(ct))
        {
            while (await lector.ReadAsync(ct))
            {
                var nombre = lector.GetString(0);
                leidos[nombre] = Literales(nombre, lector.GetString(1));
            }
        }

        // Una restricción que no volvió es tan grave como una que no se pudo leer, y
        // por el mismo motivo: alguien la renombró o la borró, y el vocabulario que
        // dependía de ella se quedó sin fuente sin que nada se rompiera.
        var faltantes = restricciones.Where(r => !leidos.ContainsKey(r)).ToList();

        return faltantes.Count == 0
            ? leidos
            : throw new VocabularioIlegible(
                string.Join(", ", faltantes), $"no existe en {esquema}.{tabla}");
    }

    /// <summary>Los cargos activos, por su nombre y por su abreviatura.</summary>
    /// <remarks>
    /// Las dos formas entran como valores distintos del mismo cargo: la gente
    /// pregunta tanto «los JTP» como «los jefes de trabajos prácticos».
    /// </remarks>
    public static async Task<IReadOnlyList<(string Valor, string Cargo)>> CargosAsync(
        NpgsqlConnection conexion, CancellationToken ct)
    {
        var cargos = new List<(string, string)>();

        await using var comando = new NpgsqlCommand(SqlCargos, conexion);
        await using var lector = await comando.ExecuteReaderAsync(ct);

        while (await lector.ReadAsync(ct))
        {
            var nombre = lector.GetString(0);
            var abreviatura = lector.GetString(1);

            cargos.Add((nombre, nombre));

            if (!abreviatura.Equals(nombre, StringComparison.OrdinalIgnoreCase))
            {
                cargos.Add((abreviatura, nombre));
            }
        }

        return cargos;
    }

    /// <summary>
    /// Extrae los literales de una definición normalizada.
    /// </summary>
    /// <remarks>
    /// Exige la forma <c>= ANY (ARRAY[...])</c> y falla si no está. Es deliberado
    /// que sea estricto: cualquier otra forma significa que la restricción dejó de
    /// ser una enumeración, y adivinar qué quiso decir es peor que decir que no se
    /// puede leer.
    /// </remarks>
    private static IReadOnlyList<string> Literales(string restriccion, string definicion)
    {
        var arreglo = Arreglo().Match(definicion);

        if (!arreglo.Success)
        {
            throw new VocabularioIlegible(restriccion, definicion);
        }

        var valores = Literal()
            .Matches(arreglo.Groups["valores"].Value)
            .Select(m => m.Groups["valor"].Value.Replace("''", "'", StringComparison.Ordinal))
            .ToList();

        return valores.Count > 0 ? valores : throw new VocabularioIlegible(restriccion, definicion);
    }

    // El bloque ARRAY[...] de la forma normalizada. Perezoso a propósito: una
    // restricción puede tener más de un ANY —`col IS NULL OR col = ANY (...)`— y el
    // primero es el que enumera.
    [GeneratedRegex(@"=\s*ANY\s*\(\s*ARRAY\[(?<valores>.*?)\]", RegexOptions.Singleline)]
    private static partial Regex Arreglo();

    // Un literal con su cast, tal como lo emite el motor. La comilla escapada va
    // como '' adentro del literal, que es como PostgreSQL la devuelve.
    [GeneratedRegex(@"'(?<valor>(?:[^']|'')*)'::(?:character varying|text|bpchar)")]
    private static partial Regex Literal();
}
