using System.Text.Json;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica la decisión de carril y la tabla dorada sobre el corpus de evaluación.
/// </summary>
/// <remarks>
/// <b>El corpus ajeno es la mitad que importa.</b> Que el enrutador capture lo que
/// tiene que capturar lo prueban unos pocos casos; que no capture lo que no le
/// corresponde no se puede probar con casos escritos a mano, porque uno escribe los
/// que ya sabe que fallan. Por eso las preguntas salen de los datasets de
/// evaluación: los escribió otra tarea con otro objetivo, así que son legítimas y
/// ajenas al catálogo.
///
/// <b>Sobre ese corpus se fija una tabla dorada</b> —un archivo versionado con el
/// mapeo ítem → intención capturada o nulo— en vez de un assert booleano. La
/// diferencia no es de estilo: el booleano produce un veredicto y la tabla produce
/// un número, que es lo que hace falta para fundamentar el pedido de los edges de
/// ARS-46. Y ante una intención nueva legítima, el booleano sólo se podía satisfacer
/// debilitándola; la tabla se actualiza y el diff muestra la decisión.
///
/// Todo esto cuesta <b>cero llamadas al modelo</b>: el enrutador es determinista y no
/// tiene por dónde llamar.
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

    // ---------------------------------------------------- la tabla dorada

    [Fact]
    public async Task El_mapeo_de_los_datasets_coincide_con_la_tabla_dorada()
    {
        // REEMPLAZA AL ASSERT BOOLEANO «ninguna se captura», y no convive con él.
        // Los dos fallan en las mismas situaciones, pero ante un rojo piden arreglos
        // opuestos: el booleano afirma «no captura ninguna» y ante una intención
        // nueva legítima que DEBE capturar un ítem sólo se puede satisfacer
        // debilitando la intención o sacando el ítem del dataset. La tabla afirma
        // «captura exactamente esto», así que se actualiza la entrada y el diff
        // muestra la decisión.
        //
        // Y produce un NÚMERO en vez de un veredicto: cuántos ítems del corpus
        // captura el catálogo. Es la mitad del dato que fundamenta el pedido de los
        // edges de ARS-46, y se obtiene hoy, sin tráfico real.
        //
        // CUESTA CERO LLAMADAS AL MODELO. No porque esta corrida no haya llamado sino
        // porque el enrutador no tiene por dónde: lo sostiene
        // `El_enrutador_no_recibe_por_donde_llamar_al_modelo`, sobre las dependencias.
        //
        // LA TABLA NO SE REGENERA COMO EFECTO DE CORRER ESTO. Se edita a mano y el
        // diff del archivo es lo que se revisa. Si regenerar fuera automático, una
        // intención demasiado laxa se absorbería sola en el primer commit que la
        // causara y nada la detectaría nunca.
        await SembrarAsync();

        var dorada = await TablaDoradaAsync();
        var observado = await ObservadoAsync();

        var derivas = dorada
            .Where(entrada => observado[entrada.Id] != entrada.Intencion)
            .Select(entrada => Deriva(entrada, observado[entrada.Id]))
            .ToList();

        Assert.True(derivas.Count == 0,
            "El enrutador decide distinto de lo que fija la tabla dorada "
            + $"({RutaDeLaTablaDorada}). Cada línea dice el ítem, en qué dirección se "
            + "movió y qué lectura tiene:\n" + string.Join("\n", derivas));
    }

    [Fact]
    public async Task La_tabla_dorada_cubre_exactamente_los_items_de_los_dos_datasets()
    {
        // EL GUARD QUE LA TABLA HEREDA DEL BANCO NEGATIVO. Una tabla vacía —o a la
        // que le falte justo el ítem que se rompió— daría verde para siempre: el test
        // que protege al catálogo necesita que alguien proteja al test.
        var dorada = await TablaDoradaAsync();
        var items = await ItemsDeLosDosDatasetsAsync();

        var enLaTabla = dorada.Select(entrada => entrada.Id).ToHashSet(StringComparer.Ordinal);
        var enLosDatasets = items.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);

        var sinEntrada = enLosDatasets.Except(enLaTabla).Order(StringComparer.Ordinal).ToList();
        var sobrantes = enLaTabla.Except(enLosDatasets).Order(StringComparer.Ordinal).ToList();

        Assert.True(sinEntrada.Count == 0,
            "Estos ítems de los datasets no tienen entrada en la tabla dorada, así que "
            + "nadie mira qué decide el enrutador para ellos: "
            + string.Join(", ", sinEntrada));

        Assert.True(sobrantes.Count == 0,
            "Estas entradas de la tabla dorada no las reclama ningún ítem de los "
            + "datasets; sobraron de un ítem borrado o renombrado: "
            + string.Join(", ", sobrantes));

        // El corpus tiene que seguir siendo un corpus. Es el mismo piso que sostenía
        // al banco negativo antes de que la tabla lo subsumiera.
        Assert.True(items.Count >= 30,
            $"El corpus quedó en {items.Count} ítems: demasiado chico para que la "
            + "cobertura signifique algo.");
    }

    // --------------------------------- consistencia de fraseo y el número

    [Fact]
    public async Task Cada_parafrasis_decide_lo_mismo_que_su_pregunta_de_origen()
    {
        // LA MITAD «CUÁNTAS VECES SE EQUIVOCA», Y SALE GRATIS. Cada ítem de
        // robustez.json declara su `origen` en capacidad.json: es la MISMA pregunta
        // dicha de otra manera —sin tildes, con un tipeo, con un sinónimo—, así que
        // el enrutador tiene que decidir lo mismo para las dos. Capturar el fraseo
        // canónico y no la paráfrasis, o al revés, es un error del enrutador medible
        // hoy, sin tráfico real y sin una llamada al modelo.
        //
        // NO AFIRMA QUE LA INTENCIÓN CAPTURADA SEA LA CORRECTA para la pregunta. Los
        // datasets llevan `sql_referencia`, no una intención esperada, y escribirla
        // sería redactar la clave de respuestas de lo que se está midiendo. Lo único
        // que se afirma acá es que las dos formas de preguntar lo mismo se deciden
        // igual; la corrección de la elección la juzga un humano leyendo un diff.
        await SembrarAsync();

        var items = await ItemsDeLosDosDatasetsAsync();
        var observado = await ObservadoAsync();
        var porId = items.ToDictionary(item => item.Id, StringComparer.Ordinal);

        var divergencias = items
            .Where(item => item.Origen is not null && porId.ContainsKey(item.Origen))
            .Where(item => observado[item.Id] != observado[item.Origen!])
            .Select(item =>
                $"{item.Id} «{item.Pregunta}» → {Decidido(observado[item.Id])}, "
                + $"pero su origen {item.Origen} «{porId[item.Origen!].Pregunta}» → "
                + $"{Decidido(observado[item.Origen!])}")
            .ToList();

        // Los dos ítems y las dos decisiones: sin la pregunta de cada lado, quien vea
        // el rojo no puede saber cuál de los dos fraseos es el que el catálogo no
        // tolera.
        Assert.True(divergencias.Count == 0,
            "El enrutador decide distinto para dos formas de decir la misma pregunta. "
            + "Es un error del enrutador, no de los datasets:\n"
            + string.Join("\n", divergencias));
    }

    [Fact]
    public async Task Todo_origen_de_robustez_apunta_a_un_item_de_capacidad()
    {
        // Sin esto, la consistencia de fraseo se degrada en silencio: un `origen` mal
        // escrito no rompe nada, simplemente deja al ítem sin par y lo saca de la
        // medición. El test de arriba lo saltearía sin decir una palabra.
        var items = await ItemsDeLosDosDatasetsAsync();
        var conocidos = items.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);

        var huerfanos = items
            .Where(item => item.Origen is not null && !conocidos.Contains(item.Origen))
            .Select(item => $"{item.Id} declara origen «{item.Origen}», que no existe")
            .ToList();

        Assert.True(huerfanos.Count == 0,
            "Hay paráfrasis cuyo origen no corresponde a ningún ítem, así que quedan "
            + "fuera de la medición de consistencia de fraseo:\n"
            + string.Join("\n", huerfanos));
    }

    [Fact]
    public async Task La_cobertura_del_catalogo_esta_escrita_en_la_tabla_y_verificada()
    {
        // EL NÚMERO, A LA VISTA Y NO DERIVADO A MANO. El pedido de los edges de
        // ARS-46 lo tiene que citar, y una métrica que hay que recalcular cada vez se
        // reconstruye distinta cada vez. Está escrito en el archivo —que es el
        // artefacto que el pedido cita— y este test lo compara contra las entradas y
        // contra los datasets, por el mismo criterio con que el repo trata a
        // `manifiesto-privilegios.json`: un valor documentado es un dato verificado,
        // no prosa.
        var (capturados, total) = await CoberturaDeclaradaAsync();
        var dorada = await TablaDoradaAsync();
        var items = await ItemsDeLosDosDatasetsAsync();

        var capturadosEnLaTabla = dorada.Count(entrada => entrada.Intencion is not null);

        Assert.True(total == items.Count,
            $"La tabla dorada declara un corpus de {total} ítems y los datasets tienen "
            + $"{items.Count}.");

        Assert.True(capturados == capturadosEnLaTabla,
            $"La tabla dorada declara {capturados} ítems capturados y sus entradas "
            + $"tienen {capturadosEnLaTabla}.");
    }

    // ------------------------------------------------------------ apoyo

    /// <summary>Ruta del archivo de tabla dorada, relativa a la raíz del repo.</summary>
    private const string RutaDeLaTablaDorada =
        "backend/tests/ArsDocendi.IntegrationTests/Asistente/tabla-dorada-enrutador.json";

    /// <summary>Una entrada de la tabla dorada: un ítem y lo que captura.</summary>
    private sealed record EntradaDorada(string Id, string? Intencion);

    /// <summary>Un ítem de los datasets, con el origen si es una paráfrasis.</summary>
    private sealed record ItemDelCorpus(string Id, string Pregunta, string? Origen);

    /// <summary>
    /// Cómo se lee una diferencia entre lo observado y lo fijado.
    /// </summary>
    /// <remarks>
    /// Las tres direcciones piden arreglos distintos, y decirlo en el mensaje es lo
    /// que evita que la tabla se actualice a ciegas para volver el rojo verde.
    /// </remarks>
    private static string Deriva(EntradaDorada fijada, string? observada) =>
        (fijada.Intencion, observada) switch
        {
            (null, not null) =>
                $"{fijada.Id}: nulo → «{observada}». POSIBLE LAXITUD: una intención "
                + "empezó a capturar una pregunta legítima y ajena al catálogo. Se "
                + "revisa la intención, no el dataset.",
            (not null, null) =>
                $"{fijada.Id}: «{fijada.Intencion}» → nulo. CAPTURA PERDIDA: el "
                + "catálogo dejó de reconocer algo que reconocía. O se estrechó una "
                + "intención sin querer, o cambió el fraseo del ítem.",
            var (antes, ahora) =>
                $"{fijada.Id}: «{antes}» → «{ahora}». CAMBIÓ QUIÉN LO CAPTURA: dos "
                + "intenciones se solapan y el orden del catálogo decide cuál gana.",
        };

    private async Task<Dictionary<string, string?>> ObservadoAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var enrutador = Enrutador();
        var observado = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var item in await ItemsDeLosDosDatasetsAsync())
        {
            var decidida = await enrutador.DecidirAsync(item.Pregunta, ct);
            observado[item.Id] = decidida?.Intencion.Nombre;
        }

        return observado;
    }

    private static async Task<List<EntradaDorada>> TablaDoradaAsync()
    {
        var documento = JsonDocument.Parse(
            await File.ReadAllTextAsync(
                Path.Combine([RaizRepositorio.Ruta(), .. RutaDeLaTablaDorada.Split('/')]),
                TestContext.Current.CancellationToken));

        return
        [
            .. documento.RootElement.GetProperty("entradas").EnumerateArray()
                .Select(entrada => new EntradaDorada(
                    entrada.GetProperty("id").GetString()!,
                    entrada.GetProperty("intencion").GetString())),
        ];
    }

    /// <summary>Cómo se nombra una decisión en un mensaje de fallo.</summary>
    private static string Decidido(string? intencion) =>
        intencion is null ? "nadie lo captura" : $"«{intencion}»";

    /// <summary>La cobertura que el archivo declara: capturados sobre el total.</summary>
    private static async Task<(int Capturados, int Total)> CoberturaDeclaradaAsync()
    {
        var documento = JsonDocument.Parse(
            await File.ReadAllTextAsync(
                Path.Combine([RaizRepositorio.Ruta(), .. RutaDeLaTablaDorada.Split('/')]),
                TestContext.Current.CancellationToken));

        var cobertura = documento.RootElement.GetProperty("cobertura");

        return (cobertura.GetProperty("capturados").GetInt32(),
                cobertura.GetProperty("total").GetInt32());
    }

    private static async Task<List<ItemDelCorpus>> ItemsDeLosDosDatasetsAsync()
    {
        List<ItemDelCorpus> corpus = [];

        foreach (var archivo in (string[])["capacidad.json", "robustez.json"])
        {
            corpus.AddRange((await ItemsAsync(archivo, "items")).Select(item =>
                new ItemDelCorpus(
                    item.GetProperty("id").GetString()!,
                    item.GetProperty("pregunta").GetString()!,
                    item.TryGetProperty("origen", out var origen) ? origen.GetString() : null)));
        }

        return corpus;
    }

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
