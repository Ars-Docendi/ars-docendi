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

/// <summary>Lee los valores de los catálogos cerrados, para que el modelo no los adivine.</summary>
internal static class LectorDeValoresDeCatalogo
{
    /// <summary>Tope de valores por columna.</summary>
    internal const int MaximoDeValores = 40;

    /// <summary>Las columnas cuyos valores viajan al prompt.</summary>
    internal static readonly IReadOnlyList<(string Esquema, string Tabla, string Columna)>
        CatalogosCerrados =
        [
            ("identity", "carreras", "code"),
            ("identity", "carreras", "name"),
            ("designaciones", "cargos", "codigo"),
            ("designaciones", "cargos", "nombre"),
        ];

    /// <summary>
    /// Las columnas que se suman a <see cref="CatalogosCerrados"/> sólo con
    /// <c>GlosarioEnElPrefijo</c> prendida (asistente-glosario-institucional, D5).
    /// </summary>
    internal static readonly IReadOnlyList<(string Esquema, string Tabla, string Columna)>
        CatalogosDelGlosario =
        [
            ("designaciones", "dedicaciones", "nombre"),
            ("designaciones", "dedicaciones", "codigo"),
        ];

    /// <summary>
    /// Las columnas que se leen: las de siempre y, con el glosario, las del glosario
    /// a continuación, para que los valores de siempre conserven su posición.
    /// </summary>
    internal static IReadOnlyList<(string Esquema, string Tabla, string Columna)> Declaradas(
        bool conGlosario) =>
        conGlosario ? [.. CatalogosCerrados, .. CatalogosDelGlosario] : CatalogosCerrados;

    /// <summary>Lee los valores que la conexión actual puede leer.</summary>
    internal static async Task<IReadOnlyList<VocabularioDeUnaColumna>> LeerAsync(
        NpgsqlConnection conexion, CancellationToken ct, bool conGlosario = false)
    {
        var vocabularios = new List<VocabularioDeUnaColumna>();

        foreach (var (esquema, tabla, columna) in Declaradas(conGlosario))
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
    private static async Task<IReadOnlyList<string>?> LeerColumnaAsync(
        NpgsqlConnection conexion,
        string esquema,
        string tabla,
        string columna,
        CancellationToken ct)
    {
        // Los identificadores no pueden ir como parámetros, así que se interpolan.
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
            // Sin privilegio, o la tabla todavía no existe en este ambiente. Es el mismo criterio
            // que el resto del prefijo: se describe lo que el rol puede leer y nada más.
            return null;
        }

        // Se leyó uno de más justamente para poder distinguir «entran todos» de
        // «hay más»: con LIMIT exacto las dos situaciones devuelven lo mismo.
        return valores.Count > MaximoDeValores ? null : valores;
    }
}
