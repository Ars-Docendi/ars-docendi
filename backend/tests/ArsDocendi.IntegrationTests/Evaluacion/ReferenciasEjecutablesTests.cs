using ArsDocendi.Evaluacion.Nucleo.Dataset;
using ArsDocendi.Evaluacion.Nucleo.Fixture;
using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Asistente.Application;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Evaluacion;

/// <summary>Cada consulta de referencia de los datasets ejecuta contra el fixture.</summary>
public sealed class ReferenciasEjecutablesTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "eval_referencias")
{
    [Fact]
    public async Task Cada_consulta_de_referencia_ejecuta_contra_el_fixture()
    {
        await AplicarFixtureAsync();

        var rotas = new List<string>();

        foreach (var (id, sql, bindings) in Referencias())
        {
            try
            {
                await using var conexion = new NpgsqlConnection(Cadena);
                await conexion.OpenAsync(TestContext.Current.CancellationToken);
                await using var comando = PrepararComando(conexion, sql, bindings);
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
                rotas.Add($"{id}: {error.SqlState} {error.MessageText}");
            }
        }

        Assert.True(
            rotas.Count == 0,
            "Hay consultas de referencia que no ejecutan contra el fixture:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, rotas));
    }

    private async Task AplicarFixtureAsync()
    {
        await using var conexion = new NpgsqlConnection(Cadena);
        await conexion.OpenAsync(TestContext.Current.CancellationToken);
        await using var comando = new NpgsqlCommand(
            new GeneradorDeFixture().Generar(), conexion)
        { CommandTimeout = 60 };

        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Cada referencia de los dos ejes que las tienen, con su ítem.</summary>
    private static IEnumerable<(string Id, string Sql, IReadOnlyDictionary<string, Guid>? Bindings)> Referencias()
    {
        var capacidad = DatasetDeCapacidad.Cargar(RutaDelDataset("capacidad.json"));

        foreach (var item in capacidad.Items)
        {
            // Los ítems infactibles no llevan referencia: su respuesta correcta es
            // la abstención, y no hay consulta que la exprese.
            if (!string.IsNullOrWhiteSpace(item.SqlReferencia))
            {
                yield return (item.Id, item.SqlReferencia, BindingsDe(item));
            }
        }

        var dialogo = DatasetDeDialogo.Cargar(RutaDelDataset("dialogo.json"));

        foreach (var conversacion in dialogo.Dialogos)
        {
            for (var turno = 0; turno < conversacion.Turnos.Count; turno++)
            {
                var sql = conversacion.Turnos[turno].SqlReferencia;

                if (!string.IsNullOrWhiteSpace(sql))
                {
                    yield return ($"{conversacion.Id}#{turno + 1}", sql, null);
                }
            }
        }

        // Robustez no aporta referencias propias: hereda la de su origen, y ésa ya
        // pasó por acá. Verificarla otra vez mediría lo mismo dos veces.
    }

    /// <summary>
    /// Los bindings <c>$refN -&gt; id</c> de un ítem, derivados del ÍNDICE del fixture y no de un
    /// GUID escrito a mano — mismo criterio que <c>RunnerDeCapacidad</c>.
    /// </summary>
    private static IReadOnlyDictionary<string, Guid>? BindingsDe(ItemDeCapacidad item) =>
        item.Referencias is { Count: > 0 } referencias
            ? referencias.ToDictionary(
                r => r.Marcador,
                r => Guid.Parse(r.Tipo == "materia"
                    ? GeneradorDeFixture.IdDeMateria(r.Indice)
                    : GeneradorDeFixture.IdDePersona(r.Indice)),
                StringComparer.Ordinal)
            : null;

    /// <summary>
    /// Un comando listo para ejecutar: si hay bindings, reescribe <c>$refN</c> a
    /// <c>@refN</c> —mismo mecanismo que <c>ReescritorDeMarcadores</c>— y liga
    /// cada uno como parámetro <c>uuid</c>, nunca interpolado.
    /// </summary>
    private static NpgsqlCommand PrepararComando(
        NpgsqlConnection conexion, string sql, IReadOnlyDictionary<string, Guid>? bindings)
    {
        if (bindings is null || bindings.Count == 0)
        {
            return new NpgsqlCommand(sql, conexion);
        }

        var (sqlConParametros, usados) = ReescritorDeMarcadores.Reescribir(sql, bindings);
        var comando = new NpgsqlCommand(sqlConParametros, conexion);

        foreach (var (nombre, id) in usados)
        {
            comando.Parameters.Add(new NpgsqlParameter(nombre, NpgsqlTypes.NpgsqlDbType.Uuid) { Value = id });
        }

        return comando;
    }

    private static string RutaDelDataset(string archivo) =>
        Path.Combine(RaizRepositorio.Ruta(), "backend", "eval", "datasets", archivo);
}
