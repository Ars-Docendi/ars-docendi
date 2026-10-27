using System.Text.RegularExpressions;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// La restricción de una columna no tiene la forma de la que se pueden derivar sus valores
/// admitidos.
/// </summary>
internal sealed class VocabularioIlegible(string restriccion, string definicion)
    : InvalidOperationException(
        $"La restricción '{restriccion}' no enumera literales, así que no se puede "
        + $"derivar de ella un vocabulario cerrado. Definición: {definicion}")
{
    /// <summary>La restricción que no se pudo leer.</summary>
    public string Restriccion { get; } = restriccion;
}

/// <summary>Lee de la base los vocabularios cerrados del trámite.</summary>
internal static partial class LectorDeVocabulario
{
    /// <summary>La definición normalizada de las restricciones de una tabla.</summary>
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

    /// <summary>Los valores admitidos de cada restricción pedida.</summary>
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

    /// <summary>Extrae los literales de una definición normalizada.</summary>
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

    // El bloque ARRAY[...] de la forma normalizada. Perezoso a propósito: una restricción puede
    // tener más de un ANY —`col IS NULL OR col = ANY (...)`— y el primero es el que enumera.
    [GeneratedRegex(@"=\s*ANY\s*\(\s*ARRAY\[(?<valores>.*?)\]", RegexOptions.Singleline)]
    private static partial Regex Arreglo();

    // Un literal con su cast, tal como lo emite el motor. La comilla escapada va
    // como '' adentro del literal, que es como PostgreSQL la devuelve.
    [GeneratedRegex(@"'(?<valor>(?:[^']|'')*)'::(?:character varying|text|bpchar)")]
    private static partial Regex Literal();
}
