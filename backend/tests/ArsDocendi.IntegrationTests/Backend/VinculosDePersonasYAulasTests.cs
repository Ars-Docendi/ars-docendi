using System.Security.Claims;
using ArsDocendi.Host.Asistente;
using Microsoft.AspNetCore.Http;
using Modules.Asistente.Application;
using Modules.Aulas.Contracts.Queries;

namespace ArsDocendi.IntegrationTests.Backend;

/// <summary>
/// Links from assistant rows to a room request, and the internal-key column that carries them.
/// The persona scope rule is covered by <c>AdministracionPersonasTests</c> against a real database.
/// </summary>
public sealed class VinculosDePersonasYAulasTests
{
    private static readonly Guid Propia = Guid.Parse("e1000000-0000-4000-8000-000000000001");
    private static readonly Guid Ajena = Guid.Parse("e1000000-0000-4000-8000-000000000002");

    [Fact]
    public async Task Aulas_ofrece_vinculo_solo_a_las_solicitudes_que_el_modulo_deja_abrir()
    {
        // Aulas decides (aprobar, or own + solicitar); the adapter must not derive the link from
        // the row having been returned.
        var adaptador = new VinculosDeAulas(new AulasFalso([Propia]));

        var destinos = await adaptador.ResolverAsync(
            [Propia.ToString(), Ajena.ToString(), "2026-9005", "no es un uuid"],
            TestContext.Current.CancellationToken);

        var destino = Assert.Single(destinos);
        Assert.Equal(Propia.ToString(), destino.Key);
        Assert.Equal(VinculosDeAulas.TipoDeLaSolicitud, destino.Value.Tipo);
        Assert.True(destino.Value.EsClave);
    }

    [Fact]
    public async Task Sin_uuids_entre_los_candidatos_no_se_le_pregunta_nada_a_aulas()
    {
        var adaptador = new VinculosDeAulas(new AulasQueReviente());

        Assert.Empty(await adaptador.ResolverAsync(["2026-9005"], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Sin_contexto_de_pedido_personas_no_resuelve_nada()
    {
        // A turn outside an HTTP request has no principal to authorize against: fail closed.
        var adaptador = new VinculosDePersonas(null!, null!, new ContextoFalso(null));

        Assert.Empty(await adaptador.ResolverAsync(
            [Propia.ToString()], TestContext.Current.CancellationToken));
    }

    [Fact]
    public void La_clave_interna_pasa_al_rotulo_y_su_columna_sale_de_la_tabla()
    {
        var resultado = Resultado(
            ["persona_id", "apellido", "nombre"],
            [[Propia.ToString(), "Lovelace", "Ada"], [Ajena.ToString(), "Hopper", "Grace"]]);
        IReadOnlyList<VinculoDelResultado> vinculos =
        [
            new(0, 0, "persona", "p1", EsClave: true),
            new(1, 0, "persona", "p2", EsClave: true),
        ];

        var (limpio, movidos) = ClavesInternas.Quitar(resultado, vinculos);

        Assert.Equal(["apellido", "nombre"], limpio.Columnas);
        Assert.Equal("Lovelace", limpio.Filas[0][0]);
        Assert.Equal([(0, 0), (1, 0)], movidos.Select(v => (v.Fila, v.Columna)));
        Assert.Equal(2, limpio.Sensibilidad.Count);
    }

    [Fact]
    public void Sin_celda_de_rotulo_la_columna_se_queda_y_el_vinculo_con_ella()
    {
        // there the link would have nowhere to go, so the column must stay.
        var sinRotulo = Resultado(["persona_id"], [[Propia.ToString()]]);
        IReadOnlyList<VinculoDelResultado> vinculos = [new(0, 0, "persona", "p1", EsClave: true)];

        var (limpio, movidos) = ClavesInternas.Quitar(sinRotulo, vinculos);

        Assert.Equal(["persona_id"], limpio.Columnas);
        Assert.Equal((0, 0), (movidos[0].Fila, movidos[0].Columna));
    }

    [Fact]
    public void Un_resultado_sin_claves_internas_no_se_toca()
    {
        var resultado = Resultado(["numero"], [["2026-9005"]]);
        IReadOnlyList<VinculoDelResultado> vinculos = [new(0, 0, "pedido-designacion", "x")];

        var (limpio, movidos) = ClavesInternas.Quitar(resultado, vinculos);

        Assert.Same(resultado, limpio);
        Assert.Same(vinculos, movidos);
    }

    [Fact]
    public async Task Varias_filas_del_mismo_docente_tienen_ficha_y_dos_docentes_no()
    {
        var fichas = new FichasQueCuentan();
        var mismo = Resultado(["persona_id", "materia"], [["a", "x"], ["a", "y"]]);
        IReadOnlyList<VinculoDelResultado> deUno =
            [new(0, 0, "persona", "p1", true), new(1, 0, "persona", "p1", true)];
        IReadOnlyList<VinculoDelResultado> deDos =
            [new(0, 0, "persona", "p1", true), new(1, 0, "persona", "p2", true)];

        var conFicha = await CierreDelTurno.PrincipalAsync(
            mismo, deUno, fichas, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            TestContext.Current.CancellationToken);
        var sinFicha = await CierreDelTurno.PrincipalAsync(
            mismo, deDos, fichas, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            TestContext.Current.CancellationToken);

        Assert.NotNull(conFicha);
        Assert.Null(sinFicha);
        Assert.Equal(1, fichas.Pedidas);
    }

    // ------------------------------------------------------------------ apoyo

    private static ResultadoDelTurno Resultado(
        string[] columnas, object?[][] filas) =>
        new(
            EstadoDelTurno.Respondida,
            "texto",
            "razonamiento",
            null,
            columnas,
            [.. filas.Select(f => (IReadOnlyList<object?>)f)],
            false,
            [.. columnas.Select(_ => SensibilidadDeColumna.Publica)],
            "consulta_simple",
            1);

    private sealed class AulasFalso(IEnumerable<Guid> abribles) : IAulasQueries
    {
        public Task<IReadOnlySet<Guid>> SolicitudesAbriblesAsync(
            IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult<IReadOnlySet<Guid>>(ids.Where(abribles.Contains).ToHashSet());
    }

    private sealed class AulasQueReviente : IAulasQueries
    {
        public Task<IReadOnlySet<Guid>> SolicitudesAbriblesAsync(
            IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            throw new InvalidOperationException("No debía consultarse.");
    }

    private sealed class FichasQueCuentan : IProveedorDeFichas
    {
        public int Pedidas { get; private set; }

        public Task<FichaDelElemento?> ObtenerAsync(string tipo, string id, CancellationToken ct)
        {
            Pedidas++;
            return Task.FromResult<FichaDelElemento?>(
                new FichaDelElemento(tipo, id, new Dictionary<string, string?>()));
        }
    }

    private sealed class ContextoFalso(HttpContext? contexto) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = contexto;
    }
}
