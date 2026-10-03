using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Asistente.Application;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Los vínculos del resultado: de dónde salen los candidatos y qué pasa con lo que
/// el resolutor conteste.
/// </summary>
/// <remarks>
/// LO QUE ESTOS TESTS CUIDAN es que el asistente no invente vínculos. Quién puede
/// abrir un trámite lo decide el módulo dueño; acá sólo se toman valores de celda,
/// se los somete a esa autoridad, y se ubica lo que haya vuelto. Un turno que
/// ofreciera un vínculo sin haber preguntado sería un botón que responde 403.
/// </remarks>
public sealed class VinculosDelTurnoTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_vinculos")
{
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");

    private const string DosTramites =
        "SELECT numero FROM designaciones.pedidos ORDER BY numero LIMIT 2";

    private const string SinFilas =
        "SELECT numero FROM designaciones.pedidos WHERE numero = 'no-existe'";

    // ------------------------------------------------------ el buscador, puro

    [Fact]
    public void Solo_se_proponen_valores_con_forma_de_identificador()
    {
        IReadOnlyList<IReadOnlyList<object?>> filas =
        [
            ["2026-9005", "Renuncia informada por nota", 42, null, "corto"],
        ];

        var candidatos = BuscadorDeVinculos.Candidatos(filas);

        // El texto libre queda afuera por los espacios, el número por no ser cadena,
        // y el nulo por serlo. El asistente no sabe qué forma tiene el identificador
        // de ningún módulo, así que el filtro es genérico y el que descarta de
        // verdad es el dueño del recurso.
        Assert.Equal(["2026-9005", "corto"], candidatos.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Un_texto_mas_largo_que_el_tope_no_es_candidato()
    {
        IReadOnlyList<IReadOnlyList<object?>> filas =
        [
            [new string('x', BuscadorDeVinculos.LargoMaximoDelCandidato + 1)],
            [new string('x', BuscadorDeVinculos.LargoMaximoDelCandidato)],
        ];

        Assert.Single(BuscadorDeVinculos.Candidatos(filas));
    }

    [Fact]
    public void El_mismo_identificador_en_dos_celdas_produce_dos_vinculos()
    {
        // Pasa de verdad: dos filas del historial del mismo trámite. Cada celda es
        // lo que el usuario aprieta, así que la que quedara sin enlace parecería
        // rota.
        IReadOnlyList<IReadOnlyList<object?>> filas =
        [
            ["2026-9005", "aceptar"],
            ["2026-9005", "devolver"],
        ];

        var destinos = new Dictionary<string, DestinoDelVinculo>(StringComparer.Ordinal)
        {
            ["2026-9005"] = new("pedido-designacion", "abc"),
        };

        var vinculos = BuscadorDeVinculos.Ubicar(filas, destinos);

        Assert.Equal(
            [(0, 0), (1, 0)],
            vinculos.Select(v => (v.Fila, v.Columna)));
    }

    [Fact]
    public void Un_candidato_que_no_resolvio_no_produce_vinculo()
    {
        // LA MITAD QUE MUERDE. Sin ella, una implementación que enlazara todo
        // candidato —sin mirar lo que contestó la autoridad— pasaría el test de
        // arriba: allá los dos candidatos habían resuelto.
        IReadOnlyList<IReadOnlyList<object?>> filas = [["2026-9005", "2026-9006"]];

        var destinos = new Dictionary<string, DestinoDelVinculo>(StringComparer.Ordinal)
        {
            ["2026-9005"] = new("pedido-designacion", "abc"),
        };

        var vinculo = Assert.Single(BuscadorDeVinculos.Ubicar(filas, destinos));

        Assert.Equal(0, vinculo.Columna);
        Assert.Equal("abc", vinculo.Id);
    }

    // ------------------------------------------------- el turno, punta a punta

    [Fact]
    public async Task Un_turno_con_filas_ofrece_lo_que_el_resolutor_haya_ubicado()
    {
        await SembrarAsync();
        var resolutor = new ResolutorEspia(("2026-9001", "abc-123"));
        var banco = Banco(resolutor, ProveedorGuionado.Generacion(DosTramites), "Hay dos trámites.");

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿qué trámites hay?", TestContext.Current.CancellationToken);

        var vinculo = Assert.Single(turno.Vinculos ?? []);
        Assert.Equal("pedido-designacion", vinculo.Tipo);
        Assert.Equal("abc-123", vinculo.Id);

        // El otro trámite viajó como candidato y no volvió: es una fila que se
        // muestra y no se puede abrir, que es exactamente el caso que el vínculo
        // no debe inventar.
        Assert.Contains("2026-9002", resolutor.Pedidos);
        Assert.Equal(2, turno.Filas.Count);
    }

    [Fact]
    public async Task Un_turno_sin_filas_no_le_pregunta_nada_al_resolutor()
    {
        // El caso mayoritario —abstenciones, saludos, aclaraciones— no tiene por qué
        // pagar una consulta contra otro módulo.
        await SembrarAsync();
        var resolutor = new ResolutorEspia();
        var banco = Banco(resolutor, ProveedorGuionado.Generacion(SinFilas));

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿existe el trámite inventado?", TestContext.Current.CancellationToken);

        Assert.Empty(turno.Vinculos ?? []);
        Assert.Equal(0, resolutor.Llamadas);
    }

    [Fact]
    public async Task Un_resolutor_que_revienta_no_tumba_el_turno()
    {
        // El vínculo es un atajo sobre la respuesta, no la respuesta. Responder sin
        // atajo es peor que tenerlo y muchísimo mejor que un error sobre una
        // pregunta que se contestó bien.
        await SembrarAsync();
        var banco = Banco(
            new ResolutorQueRevienta(),
            ProveedorGuionado.Generacion(DosTramites),
            "Hay dos trámites.");

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿qué trámites hay?", TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);
        Assert.Equal("Hay dos trámites.", turno.Respuesta);
        Assert.Equal(2, turno.Filas.Count);
        Assert.Empty(turno.Vinculos ?? []);
    }

    [Fact]
    public async Task Sin_adaptador_compuesto_el_turno_responde_y_no_ofrece_nada()
    {
        // Es el módulo sin el Host: `SinVinculos` es lo que registra
        // `AddAsistenteModule`, y la degradación correcta es responder sin vínculos.
        await SembrarAsync();
        var banco = Banco(
            new SinVinculos(), ProveedorGuionado.Generacion(DosTramites), "Hay dos trámites.");

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿qué trámites hay?", TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);
        Assert.Empty(turno.Vinculos ?? []);
    }

    // ------------------------------------------------------------------ apoyo

    private BancoDelAsistente Banco(IResolutorDeVinculos resolutor, params string[] guion)
    {
        var (basica, pii) = CadenasDeLectura();

        return BancoDelAsistente.Armar(
            basica, pii, ClasificadorDeSensibilidad(),
            Apertura, vinculos: resolutor, guion: guion);
    }

    /// <summary>Resuelve lo que se le dijo, y anota qué le preguntaron.</summary>
    private sealed class ResolutorEspia(params (string Numero, string Id)[] ubicables)
        : IResolutorDeVinculos
    {
        public int Llamadas { get; private set; }

        public List<string> Pedidos { get; } = [];

        public Task<IReadOnlyDictionary<string, DestinoDelVinculo>> ResolverAsync(
            IReadOnlyCollection<string> candidatos, CancellationToken ct)
        {
            Llamadas++;
            Pedidos.AddRange(candidatos);

            IReadOnlyDictionary<string, DestinoDelVinculo> destinos = ubicables
                .Where(u => candidatos.Contains(u.Numero, StringComparer.Ordinal))
                .ToDictionary(
                    u => u.Numero,
                    u => new DestinoDelVinculo("pedido-designacion", u.Id),
                    StringComparer.Ordinal);

            return Task.FromResult(destinos);
        }
    }

    private sealed class ResolutorQueRevienta : IResolutorDeVinculos
    {
        public Task<IReadOnlyDictionary<string, DestinoDelVinculo>> ResolverAsync(
            IReadOnlyCollection<string> candidatos, CancellationToken ct) =>
            throw new InvalidOperationException("El módulo dueño no contestó.");
    }
}
