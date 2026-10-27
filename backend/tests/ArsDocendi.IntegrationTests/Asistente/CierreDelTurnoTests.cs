using Microsoft.Extensions.Logging.Abstractions;
using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Lo que se le agrega al resultado al salir: posiciones en el texto, principal y áreas de datos.
/// </summary>
public sealed class CierreDelTurnoTests
{
    private static readonly IReadOnlyList<IReadOnlyList<object?>> UnaFila = [["2026-9005", "devuelto"]];

    // ------------------------------------------------------------ orígenes

    [Fact]
    public void Una_consulta_sobre_pedidos_con_JOIN_a_materias_nombra_designaciones_e_identity()
    {
        const string sql =
            "SELECT p.numero, m.nombre FROM designaciones.pedidos p "
            + "JOIN identity.materias m ON m.id = p.materia_id";

        Assert.Equal(["designaciones", "identity"], OrigenesDeLosDatos.De(sql));
    }

    [Fact]
    public void Los_origenes_salen_sin_repetir_y_en_orden_estable_aunque_cambie_el_orden_de_la_consulta()
    {
        const string unaVez =
            "SELECT 1 FROM portal.perfiles x JOIN designaciones.pedidos p ON true";
        const string otraVez =
            "SELECT 1 FROM designaciones.pedidos p JOIN portal.perfiles x ON true "
            + "JOIN designaciones.cargos c ON true";

        Assert.Equal(["designaciones", "portal"], OrigenesDeLosDatos.De(unaVez));
        Assert.Equal(["designaciones", "portal"], OrigenesDeLosDatos.De(otraVez));
    }

    [Fact]
    public void Un_schema_en_un_comentario_un_literal_o_sin_calificar_no_cuenta()
    {
        const string sql =
            "SELECT 'identity.users' AS x, p.numero -- portal.perfiles\n"
            + "FROM designaciones.pedidos p WHERE p.designaciones IS NULL";

        // `p.designaciones` es una columna: va después del punto, no antes.
        Assert.Equal(["designaciones"], OrigenesDeLosDatos.De(sql));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sin_consulta_ejecutada_no_hay_origenes(string? sql)
    {
        Assert.Empty(OrigenesDeLosDatos.De(sql));
    }

    [Fact]
    public async Task Una_aclaracion_sin_consulta_no_trae_origenes()
    {
        var resultado = Resultado(EstadoDelTurno.NecesitaAclaracion, [], sqlEjecutado: null);

        var cerrado = await CierreDelTurno.CompletarAsync(
            resultado, "¿Cuál de las dos?", new SinVinculos(), new SinFichas(),
            NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.Empty(cerrado.Origenes ?? []);
    }

    // ----------------------------------------------- posiciones y principal

    [Fact]
    public async Task Las_posiciones_se_miden_sobre_el_texto_final_y_no_sobre_el_original()
    {
        // El rechazo reescribe el texto: las posiciones del original señalarían otra
        // cosa. `CompletarAsync` recibe el texto FINAL y es el único que mira.
        var resultado = Resultado(EstadoDelTurno.Respondida, UnaFila, "Original: 2026-9005 está devuelto.");
        const string final = "Otro texto, ahora largo: 2026-9005.";

        var cerrado = await CierreDelTurno.CompletarAsync(
            resultado, final, new ResolutorFijo("2026-9005", "abc"), new SinFichas(),
            NullLogger.Instance, TestContext.Current.CancellationToken);

        var ubicado = Assert.Single(cerrado.VinculosDelTexto!);
        Assert.Equal(final, cerrado.Respuesta);
        Assert.Equal("2026-9005", final.Substring(ubicado.Desde, ubicado.Largo));
    }

    [Fact]
    public async Task Una_fila_con_un_vinculo_pide_la_ficha_y_la_devuelve_como_principal()
    {
        var fichas = new FichasFijas(new FichaDelElemento(
            "pedido-designacion", "abc", new Dictionary<string, string?> { ["numero"] = "2026-9005" }));

        var cerrado = await CierreDelTurno.CompletarAsync(
            Resultado(EstadoDelTurno.Respondida, UnaFila), "Es el 2026-9005.",
            new ResolutorFijo("2026-9005", "abc"), fichas,
            NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.Equal("abc", cerrado.Principal!.Id);
        Assert.Equal(("pedido-designacion", "abc"), fichas.Pedida);
    }

    [Fact]
    public async Task Varias_filas_no_piden_ficha()
    {
        IReadOnlyList<IReadOnlyList<object?>> tres = [["2026-9005"], ["2026-9005"], ["2026-9005"]];
        var fichas = new FichasFijas(new FichaDelElemento("pedido-designacion", "abc", new Dictionary<string, string?>()));

        var cerrado = await CierreDelTurno.CompletarAsync(
            Resultado(EstadoDelTurno.Respondida, tres), "Tres.",
            new ResolutorFijo("2026-9005", "abc"), fichas,
            NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.Null(cerrado.Principal);
        Assert.Null(fichas.Pedida);
    }

    [Fact]
    public async Task Una_fila_con_dos_vinculos_no_pide_ficha()
    {
        IReadOnlyList<IReadOnlyList<object?>> unaConDos = [["2026-9005", "2026-9006"]];
        var fichas = new FichasFijas(new FichaDelElemento("pedido-designacion", "abc", new Dictionary<string, string?>()));
        var resolutor = new ResolutorFijo("2026-9005", "a", "2026-9006", "b");

        var cerrado = await CierreDelTurno.CompletarAsync(
            Resultado(EstadoDelTurno.Respondida, unaConDos), "Dos.", resolutor, fichas,
            NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.Null(cerrado.Principal);
        Assert.Null(fichas.Pedida);
    }

    [Fact]
    public async Task Una_fila_sin_vinculo_no_pide_ficha()
    {
        var fichas = new FichasFijas(new FichaDelElemento("pedido-designacion", "abc", new Dictionary<string, string?>()));

        var cerrado = await CierreDelTurno.CompletarAsync(
            Resultado(EstadoDelTurno.Respondida, UnaFila), "Una.", new SinVinculos(), fichas,
            NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.Null(cerrado.Principal);
        Assert.Null(fichas.Pedida);
    }

    [Fact]
    public async Task Un_proveedor_de_fichas_que_lanza_no_tumba_el_turno_ni_los_vinculos()
    {
        var cerrado = await CierreDelTurno.CompletarAsync(
            Resultado(EstadoDelTurno.Respondida, UnaFila), "Es el 2026-9005.",
            new ResolutorFijo("2026-9005", "abc"), new FichasQueRevientan(),
            NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.Null(cerrado.Principal);
        Assert.Single(cerrado.Vinculos!);
        Assert.Single(cerrado.VinculosDelTexto!);
    }

    [Fact]
    public async Task Sin_adaptador_de_fichas_no_hay_principal()
    {
        var cerrado = await CierreDelTurno.CompletarAsync(
            Resultado(EstadoDelTurno.Respondida, UnaFila), "Es el 2026-9005.",
            new ResolutorFijo("2026-9005", "abc"), new SinFichas(),
            NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.Null(cerrado.Principal);
        Assert.Single(cerrado.Vinculos!);
    }

    [Fact]
    public async Task La_cancelacion_no_se_traga()
    {
        await Assert.ThrowsAsync<OperationCanceledException>(() => CierreDelTurno.CompletarAsync(
            Resultado(EstadoDelTurno.Respondida, UnaFila), "x",
            new ResolutorFijo("2026-9005", "abc"), new FichasQueSeCancelan(),
            NullLogger.Instance, TestContext.Current.CancellationToken));
    }

    // ------------------------------------------------------------------ apoyo

    private static ResultadoDelTurno Resultado(
        EstadoDelTurno estado,
        IReadOnlyList<IReadOnlyList<object?>> filas,
        string respuesta = "texto",
        string? sqlEjecutado = "SELECT numero FROM designaciones.pedidos") =>
        new(
            estado,
            respuesta,
            Razonamiento: string.Empty,
            PreguntaInterpretada: null,
            ["numero"],
            filas,
            Truncado: false,
            [],
            "categoria",
            LlamadasAlModelo: 0,
            SqlEjecutado: sqlEjecutado);

    private sealed class ResolutorFijo(params string[] paresNumeroId) : IResolutorDeVinculos
    {
        public Task<IReadOnlyDictionary<string, DestinoDelVinculo>> ResolverAsync(
            IReadOnlyCollection<string> candidatos, CancellationToken ct)
        {
            var destinos = new Dictionary<string, DestinoDelVinculo>(StringComparer.Ordinal);

            for (var i = 0; i < paresNumeroId.Length; i += 2)
            {
                destinos[paresNumeroId[i]] = new DestinoDelVinculo("pedido-designacion", paresNumeroId[i + 1]);
            }

            return Task.FromResult<IReadOnlyDictionary<string, DestinoDelVinculo>>(destinos);
        }
    }

    private sealed class FichasFijas(FichaDelElemento ficha) : IProveedorDeFichas
    {
        public (string Tipo, string Id)? Pedida { get; private set; }

        public Task<FichaDelElemento?> ObtenerAsync(string tipo, string id, CancellationToken ct)
        {
            Pedida = (tipo, id);
            return Task.FromResult<FichaDelElemento?>(ficha);
        }
    }

    private sealed class FichasQueRevientan : IProveedorDeFichas
    {
        public Task<FichaDelElemento?> ObtenerAsync(string tipo, string id, CancellationToken ct) =>
            throw new InvalidOperationException("Designaciones no contestó.");
    }

    private sealed class FichasQueSeCancelan : IProveedorDeFichas
    {
        public Task<FichaDelElemento?> ObtenerAsync(string tipo, string id, CancellationToken ct) =>
            throw new OperationCanceledException();
    }
}
