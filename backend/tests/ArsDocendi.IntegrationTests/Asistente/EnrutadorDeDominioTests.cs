using System.Text.Json;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica la decisión de carril y el banco de preguntas que NO debe capturar.
/// </summary>
/// <remarks>
/// <b>El banco negativo es la mitad que importa.</b> Que el enrutador capture lo que
/// tiene que capturar lo prueban unos pocos casos; que no capture lo que no le
/// corresponde no se puede probar con casos escritos a mano, porque uno escribe los
/// que ya sabe que fallan. Por eso las preguntas salen de los datasets de
/// evaluación: los escribió otra tarea con otro objetivo, así que son legítimas y
/// ajenas al catálogo.
/// </remarks>
[Collection(ColeccionPostgres.Nombre)]
public sealed class EnrutadorDeDominioTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_enrutador")
{
    // ----------------------------------------------------- lo que sí se captura

    [Fact]
    public async Task Una_pregunta_cubierta_devuelve_su_intencion_con_los_slots()
    {
        await SembrarAsync();

        var decidida = await Enrutador().DecidirAsync(
            "¿en qué estado está el pedido de Gómez?", TestContext.Current.CancellationToken);

        Assert.Equal("estado-del-pedido-de-una-persona", decidida?.Intencion.Nombre);
        Assert.Equal("Gómez", decidida?.Slots["persona"].Valor);
        Assert.Equal("designaciones/pedidos-por-persona", decidida?.Destino);
    }

    // ------------------------------------------- el default es SQL, nunca error

    [Fact]
    public async Task Una_pregunta_que_no_matchea_ninguna_intencion_no_falla()
    {
        await SembrarAsync();

        // No captura y no lanza. Es el caso normal: un catálogo de cinco intenciones
        // no cubre la mayoría de las preguntas y no pretende hacerlo.
        var decidida = await Enrutador().DecidirAsync(
            "¿cuántas horas semanales suman los nombramientos abiertos?",
            TestContext.Current.CancellationToken);

        Assert.Null(decidida);
    }

    [Fact]
    public async Task Un_slot_sin_resolver_no_enruta()
    {
        await SembrarAsync();
        await AgregarColisionAsync();

        var decidida = await Enrutador().DecidirAsync(
            "¿en qué estado está el pedido de López?", TestContext.Current.CancellationToken);

        Assert.Null(decidida);
    }

    [Fact]
    public void El_enrutador_no_recibe_por_donde_llamar_al_modelo()
    {
        // Sobre las dependencias y no contando llamadas: un contador en cero dice
        // que esta vez no llamó; que el tipo no reciba por dónde llamar dice que no
        // puede, y lo sigue diciendo cuando alguien agregue una rama.
        var parametros = typeof(EnrutadorDeDominio)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .ToList();

        Assert.DoesNotContain(typeof(IProveedorDeModelo), parametros);
        Assert.DoesNotContain(typeof(CarrilSql), parametros);
    }

    // ------------------------------------------- el banco de preguntas negativas

    [Theory]
    [InlineData("capacidad.json", "items")]
    [InlineData("robustez.json", "items")]
    public async Task Ninguna_pregunta_del_dataset_se_captura(string archivo, string coleccion)
    {
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;
        var enrutador = Enrutador();

        var capturadas = new List<string>();

        foreach (var item in await ItemsAsync(archivo, coleccion))
        {
            var pregunta = item.GetProperty("pregunta").GetString()!;
            var decidida = await enrutador.DecidirAsync(pregunta, ct);

            if (decidida is not null)
            {
                capturadas.Add(
                    $"{item.GetProperty("id").GetString()} «{pregunta}» "
                    + $"la capturó {decidida.Intencion.Nombre}");
            }
        }

        // Nombrar la pregunta Y la intención culpable: sin la segunda, quien agregue
        // una intención demasiado laxa ve el test rojo y no sabe cuál sacar.
        Assert.True(capturadas.Count == 0,
            $"El enrutador capturó preguntas de {archivo}, que son legítimas y ajenas "
            + "al catálogo. Es una intención demasiado laxa, no un dataset mal "
            + "escrito:\n" + string.Join("\n", capturadas));
    }

    [Fact]
    public async Task El_banco_negativo_no_esta_vacio()
    {
        // Un banco vacío daría verde para siempre. El test que protege al catálogo
        // necesita que alguien proteja al test.
        var capacidad = await ItemsAsync("capacidad.json", "items");
        var robustez = await ItemsAsync("robustez.json", "items");

        Assert.True(capacidad.Count + robustez.Count >= 30);
    }

    // ------------------------------------------------------------ apoyo

    private EnrutadorDeDominio Enrutador()
    {
        var basica = CadenasDeLectura().Basica;

        return new EnrutadorDeDominio(
            new ResolutorDeIntenciones(
                CatalogoDeIntenciones.Cargar(),
                new CatalogoDelDominioReal(new IndiceDeEntidades(basica), basica)),
            NullLogger<EnrutadorDeDominio>.Instance);
    }

    private static async Task<List<JsonElement>> ItemsAsync(string archivo, string coleccion)
    {
        var ruta = Path.Combine(
            RaizRepositorio.Ruta(), "backend", "eval", "datasets", archivo);

        var documento = JsonDocument.Parse(
            await File.ReadAllTextAsync(ruta, TestContext.Current.CancellationToken));

        return [.. documento.RootElement.GetProperty(coleccion).EnumerateArray()];
    }

    private async Task AgregarColisionAsync()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO identity.personas (id, documento, cuil, legajo, nombre, apellido)
            VALUES ('d0000000-0000-4000-8000-0000000009e1', '35111444', '20-35111444-9',
                    '9903', 'Damián', 'López');
            """,
            conexion);

        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task SembrarAsync()
    {
        var sql = await File.ReadAllTextAsync(
            Path.Combine(RaizRepositorio.Ruta(), "infra", "scripts", "seed-data", "sintetico.sql"),
            TestContext.Current.CancellationToken);

        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = 60 };
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
