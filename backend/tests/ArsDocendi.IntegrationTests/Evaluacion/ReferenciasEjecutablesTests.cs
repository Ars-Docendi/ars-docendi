using ArsDocendi.Evaluacion.Nucleo.Dataset;
using ArsDocendi.Evaluacion.Nucleo.Fixture;
using ArsDocendi.IntegrationTests.Infraestructura;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Evaluacion;

/// <summary>
/// Cada consulta de referencia de los datasets ejecuta contra el fixture.
/// </summary>
/// <remarks>
/// LO QUE ESTE TEST EXISTE PARA EVITAR. La corrida financiada descubrió que dos
/// turnos de <c>dia-004</c> apuntaban a <c>tipo_novedad</c>, una columna que el
/// esquema ya no tiene: la referencia ni siquiera ejecutaba. El evaluador lo
/// reportó como «falló el turno», que en el reporte se lee como un problema del
/// asistente y no del dataset, y sólo aparecía después de pagar la corrida.
///
/// La referencia es la mitad de la medición. Una que no ejecuta no mide de menos:
/// mide mal, y hacia el lado que hace quedar peor al asistente.
///
/// Corre contra el fixture de evaluación —el mismo que la corrida usa— y no contra
/// el seed de demostración: una referencia puede ejecutar contra uno y no contra el
/// otro, y el que importa es el que la métrica va a usar.
/// </remarks>
[Collection(ColeccionPostgres.Nombre)]
public sealed class ReferenciasEjecutablesTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "eval_referencias")
{
    [Fact]
    public async Task Cada_consulta_de_referencia_ejecuta_contra_el_fixture()
    {
        await AplicarFixtureAsync();

        var rotas = new List<string>();

        foreach (var (id, sql) in Referencias())
        {
            try
            {
                await using var conexion = new NpgsqlConnection(Cadena);
                await conexion.OpenAsync(TestContext.Current.CancellationToken);
                await using var comando = new NpgsqlCommand(sql, conexion);
                await using var lector = await comando.ExecuteReaderAsync(
                    TestContext.Current.CancellationToken);

                // Se recorre entero: un error de tipo aparece al leer, no al abrir.
                while (await lector.ReadAsync(TestContext.Current.CancellationToken))
                {
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

    [Fact]
    public async Task El_seguimiento_al_resultado_apunta_a_una_sola_materia()
    {
        // LA PREMISA DEL ÍTEM dia-005, y sin ella el ítem mide otra cosa. Si el
        // primer turno devolviera varias materias, «esa materia» sería ambiguo y el
        // ítem pasaría a medir desambiguación en vez de arrastre de la consulta —y
        // fallaría por un motivo que su nombre no explica.
        //
        // Depende del fixture, no del código, así que el día que cambien las
        // cardinalidades este test dice exactamente qué hay que revisar.
        await AplicarFixtureAsync();
        var ct = TestContext.Current.CancellationToken;

        var referencia = Referencias().Single(r => r.Id == "dia-005-referencia-al-resultado#1");

        await using var conexion = new NpgsqlConnection(Cadena);
        await conexion.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(referencia.Sql, conexion);
        await using var lector = await comando.ExecuteReaderAsync(ct);

        Assert.True(await lector.ReadAsync(ct));
        Assert.Equal("Señales y Sistemas", lector.GetString(0));
        Assert.False(await lector.ReadAsync(ct));
    }

    [Fact]
    public async Task Ninguna_referencia_devuelve_cero_filas_sin_declararlo()
    {
        // Una referencia vacía convierte el ítem en «el asistente acierta si tampoco
        // devuelve nada», que pasa en verde con una consulta cualquiera que no
        // matchee. No es un error por sí solo —hay preguntas cuya respuesta legítima
        // es ninguna fila— pero tiene que ser una decisión y no un accidente del
        // fixture, así que acá se enumera lo que hoy es cierto.
        await AplicarFixtureAsync();

        var vacias = new List<string>();

        foreach (var (id, sql) in Referencias())
        {
            await using var conexion = new NpgsqlConnection(Cadena);
            await conexion.OpenAsync(TestContext.Current.CancellationToken);
            await using var comando = new NpgsqlCommand(sql, conexion);
            await using var lector = await comando.ExecuteReaderAsync(
                TestContext.Current.CancellationToken);

            if (!await lector.ReadAsync(TestContext.Current.CancellationToken))
            {
                vacias.Add(id);
            }
        }

        Assert.True(
            vacias.Count == 0,
            "Hay referencias que no devuelven ninguna fila contra el fixture, así que "
            + "el ítem se satisface con cualquier consulta que tampoco devuelva nada: "
            + string.Join(", ", vacias));
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
    private static IEnumerable<(string Id, string Sql)> Referencias()
    {
        var capacidad = DatasetDeCapacidad.Cargar(RutaDelDataset("capacidad.json"));

        foreach (var item in capacidad.Items)
        {
            // Los ítems infactibles no llevan referencia: su respuesta correcta es
            // la abstención, y no hay consulta que la exprese.
            if (!string.IsNullOrWhiteSpace(item.SqlReferencia))
            {
                yield return (item.Id, item.SqlReferencia);
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
                    yield return ($"{conversacion.Id}#{turno + 1}", sql);
                }
            }
        }

        // Robustez no aporta referencias propias: hereda la de su origen, y ésa ya
        // pasó por acá. Verificarla otra vez mediría lo mismo dos veces.
    }

    private static string RutaDelDataset(string archivo) =>
        Path.Combine(RaizRepositorio.Ruta(), "backend", "eval", "datasets", archivo);
}
