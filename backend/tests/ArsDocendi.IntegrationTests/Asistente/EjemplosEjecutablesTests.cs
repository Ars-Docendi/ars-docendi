using System.Text.RegularExpressions;
using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>Cada consulta del catálogo de ejemplos ejecuta contra el esquema real.</summary>
public sealed class EjemplosEjecutablesTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_ejemplos")
{
    [Fact]
    public async Task Cada_ejemplo_del_catalogo_ejecuta_contra_el_esquema()
    {
        await SembrarAsync();

        var rotos = new List<string>();

        foreach (var ejemplo in new SelectorDeEjemplos().Catalogo)
        {
            try
            {
                await using var conexion = await AbrirConexionAsync();
                await using var comando = new NpgsqlCommand(ejemplo.Sql, conexion);
                await using var lector = await comando.ExecuteReaderAsync(
                    TestContext.Current.CancellationToken);

                // Se recorre entero: un error de tipo aparece al leer, no al abrir.
                while (await lector.ReadAsync(TestContext.Current.CancellationToken))
                {
                    // Nada que hacer con la fila: sólo importa que se pueda leer.
                }
            }
            catch (PostgresException error)
            {
                rotos.Add($"«{ejemplo.Pregunta}»: {error.SqlState} {error.MessageText}");
            }
        }

        Assert.True(
            rotos.Count == 0,
            "Hay ejemplos del catálogo que no ejecutan contra el esquema. Se le inyectan "
            + "al modelo como pares verificados, así que uno roto le enseña un join que no "
            + "existe:" + Environment.NewLine + string.Join(Environment.NewLine, rotos));
    }

    // ------------------------------------------------------------------ What "executes" does not catch.

    [Fact]
    public void No_example_reads_the_legacy_dedication_text()
    {
        // `dedicacion` and `dedicacion_solicitada` keep the text of rows older than the catalog;
        // the current form is `dedicacion_id` against `designaciones.dedicaciones`.
        var viejos = new SelectorDeEjemplos().Catalogo
            .Where(e => Regex.IsMatch(
                e.Sql, @"\.dedicacion(_solicitada)?\b(?!_)", RegexOptions.IgnoreCase))
            .Select(e => e.Pregunta)
            .ToList();

        Assert.True(
            viejos.Count == 0,
            "Examples reading the legacy dedication text instead of dedicacion_id: "
            + string.Join(" · ", viejos));
    }

    [Fact]
    public async Task Literals_on_closed_catalog_columns_exist_in_the_seed()
    {
        // The prompt lists the values of the closed catalogs so the model uses the exact one.
        await SembrarAsync();

        var cerrados = LectorDeValoresDeCatalogo.CatalogosCerrados.ToHashSet();
        var inexistentes = new List<string>();

        foreach (var ejemplo in new SelectorDeEjemplos().Catalogo)
        {
            foreach (var (esquema, tabla, columna, literal) in IgualdadesConLiteral(ejemplo.Sql))
            {
                if (!cerrados.Contains((esquema, tabla, columna)))
                {
                    continue;
                }

                await using var conexion = await AbrirConexionAsync();
                await using var comando = new NpgsqlCommand(
                    $"SELECT EXISTS (SELECT 1 FROM {esquema}.{tabla} WHERE {columna} = @literal)", conexion);
                comando.Parameters.AddWithValue("literal", literal);

                if (await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken) is not true)
                {
                    inexistentes.Add($"«{ejemplo.Pregunta}»: {tabla}.{columna} = '{literal}'");
                }
            }
        }

        Assert.True(
            inexistentes.Count == 0,
            "Examples filtering a closed catalog by a value that does not exist:"
            + Environment.NewLine + string.Join(Environment.NewLine, inexistentes));
    }

    /// <summary>
    /// Every <c>alias.column = 'literal'</c> of a query, with the alias resolved to
    /// its table through the FROM/JOIN clauses.
    /// </summary>
    private static IEnumerable<(string Esquema, string Tabla, string Columna, string Literal)>
        IgualdadesConLiteral(string sql)
    {
        var tablas = new Dictionary<string, (string Esquema, string Tabla)>(StringComparer.OrdinalIgnoreCase);

        foreach (Match origen in Regex.Matches(
                     sql, @"\b(?:FROM|JOIN)\s+([a-z_]+)\.([a-z_]+)(?:\s+(?:AS\s+)?([a-z_]+))?", RegexOptions.IgnoreCase))
        {
            var tabla = (origen.Groups[1].Value.ToLowerInvariant(), origen.Groups[2].Value.ToLowerInvariant());
            tablas[origen.Groups[2].Value] = tabla;
            if (origen.Groups[3].Success)
            {
                tablas[origen.Groups[3].Value] = tabla;
            }
        }

        foreach (Match igualdad in Regex.Matches(sql, @"\b([a-z_]+)\.([a-z_]+)\s*=\s*'((?:[^']|'')*)'", RegexOptions.IgnoreCase))
        {
            if (tablas.TryGetValue(igualdad.Groups[1].Value, out var tabla))
            {
                yield return (
                    tabla.Esquema,
                    tabla.Tabla,
                    igualdad.Groups[2].Value.ToLowerInvariant(),
                    igualdad.Groups[3].Value.Replace("''", "'", StringComparison.Ordinal));
            }
        }
    }
}
