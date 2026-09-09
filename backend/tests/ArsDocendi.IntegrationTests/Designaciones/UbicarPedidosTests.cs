using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using Modules.Designaciones.Contracts.Queries;
using Modules.Designaciones.Infrastructure;
using Modules.Designaciones.Repositories;
using Modules.Designaciones.Services;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Designaciones;

/// <summary>
/// Ubicar un trámite por su número, acotado a lo que el actor puede abrir.
/// </summary>
/// <remarks>
/// ES LA AUTORIDAD DEL VÍNCULO QUE OFRECE EL ASISTENTE, y por eso tiene que dar
/// exactamente lo mismo que <c>GET /api/designaciones/pedidos/{id}</c>. El asistente
/// llega hasta acá con números que sacó de un resultado propio, filtrado por SUS
/// policies de RLS — que no son esta regla y ya divergen de ella. Si esta consulta
/// contestara que sí donde el endpoint contesta 403, el botón sería fake UI.
///
/// El ámbito se decide con <c>MaquinaEstadosPedido.AlcanzaAmbito</c>, la misma
/// función que usa el endpoint. Estos tests fijan que el resultado sea el que esa
/// función manda, para que una copia de la regla acá se note.
/// </remarks>
[Collection(ColeccionPostgres.Nombre)]
public sealed class UbicarPedidosTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "ubicar_pedidos")
{
    /// <summary>Jefe de cátedra de Ingeniería de Software (materia 101).</summary>
    private static readonly Guid Jefe = Guid.Parse("a0000000-0000-4000-8000-000000000002");

    /// <summary>Coordinador de la carrera de Informática (carrera 201).</summary>
    private static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");

    /// <summary>Secretaría Académica: ámbito departamental.</summary>
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");

    /// <summary>2026-9005, materia 101 — la cátedra del jefe.</summary>
    private const string DeSuCatedra = "2026-9005";

    /// <summary>2026-9006, materia 102 — otra cátedra, misma carrera.</summary>
    private const string DeOtraCatedra = "2026-9006";

    /// <summary>2026-9007, materia 103 — otra cátedra más, misma carrera.</summary>
    private const string DeUnaTercera = "2026-9007";

    /// <summary>El que agrega este test: materia 201, carrera 202.</summary>
    private const string DeOtraCarrera = "2026-9099";

    [Fact]
    public async Task El_jefe_ubica_el_de_su_catedra_y_no_los_ajenos()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);

        // El seed le da al jefe varias materias, entre ellas las de los otros dos
        // pedidos — así que «de otra cátedra» dejaba de ser de otra cátedra y el
        // test fallaba SIN que nada estuviera roto. La premisa es del test: acá se
        // lo deja a cargo únicamente de la cátedra de su pedido.
        await EjecutarAsync(
            """
            UPDATE identity.user_roles
               SET deleted_at = now()
             WHERE user_id = @actor
               AND materia_id IS NOT NULL
               AND deleted_at IS NULL
               AND materia_id <> (SELECT p.materia_id
                                    FROM designaciones.pedidos p
                                   WHERE p.numero = @suyo)
            """,
            ("actor", Jefe),
            ("suyo", DeSuCatedra));

        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);

        var ubicados = await Consultas(Jefe, identityDb, db).UbicarPedidosAsync(
            [DeSuCatedra, DeOtraCatedra, DeUnaTercera], ct);

        // Las DOS mitades. Sin la segunda, una implementación que devolviera todo lo
        // que existe pasaría la primera sin acotar nada.
        Assert.Equal([DeSuCatedra], ubicados.Select(p => p.Numero));
        Assert.NotEqual(Guid.Empty, Assert.Single(ubicados).Id);
    }

    [Fact]
    public async Task El_coordinador_ubica_los_de_su_carrera_y_no_los_de_otra()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await AgregarPedidoDeOtraCarreraAsync(ct);
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);

        var ubicados = await Consultas(Coordinador, identityDb, db).UbicarPedidosAsync(
            [DeSuCatedra, DeOtraCatedra, DeUnaTercera, DeOtraCarrera], ct);

        // Las tres materias de su carrera, y no la de la otra. El pedido de la otra
        // carrera lo agrega este test: el padrón sintético no trae ninguno, así que
        // sin él «el coordinador acota» no se podría distinguir de «ve todo».
        Assert.Equal(
            [DeSuCatedra, DeOtraCatedra, DeUnaTercera],
            ubicados.Select(p => p.Numero).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Secretaria_ubica_todos_los_del_departamento()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await AgregarPedidoDeOtraCarreraAsync(ct);
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);

        var ubicados = await Consultas(Secretaria, identityDb, db).UbicarPedidosAsync(
            [DeSuCatedra, DeOtraCatedra, DeUnaTercera, DeOtraCarrera], ct);

        Assert.Equal(4, ubicados.Count);
    }

    [Fact]
    public async Task Un_numero_que_no_existe_no_devuelve_nada()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);

        // Tiene forma válida, así que llega a la base; sencillamente no está. Quien
        // pregunta no puede distinguir esto de «existe y no lo alcanzás», y eso es
        // deliberado: decírselo sería un canal de inferencia.
        var ubicados = await Consultas(Secretaria, identityDb, db).UbicarPedidosAsync(
            ["2026-0000"], ct);

        Assert.Empty(ubicados);
    }

    [Fact]
    public async Task Un_texto_sin_forma_de_numero_no_llega_a_la_base()
    {
        var ct = TestContext.Current.CancellationToken;

        // LOS CONTEXTOS VAN CERRADOS A PROPÓSITO. Cualquier acceso a la base
        // —consultar los pedidos o resolver el actor— revienta con ObjectDisposed.
        // Que el test pase es la prueba de que no hubo ninguno; con la aserción sola
        // sobre el resultado vacío, una implementación que consultara igual y no
        // encontrara nada pasaría lo mismo.
        var identityDb = PostgresFixture.CrearIdentity(Cadena);
        var db = PostgresFixture.CrearDesignaciones(Cadena);
        await identityDb.DisposeAsync();
        await db.DisposeAsync();

        var ubicados = await Consultas(Secretaria, identityDb, db).UbicarPedidosAsync(
            ["Ingeniería", "devuelto", "2026", "9005", "2026-90", "", "  "], ct);

        Assert.Empty(ubicados);
    }

    // ------------------------------------------------------------------ apoyo

    private static IDesignacionesQueries Consultas(
        Guid usuarioId,
        IdentityDbContext identityDb,
        DesignacionesDbContext db) =>
        new ServicioConsultasDesignaciones(
            new RepositorioPedidos(db),
            new ResolutorActor(new UsuarioActualFalso(usuarioId), new ConsultasIdentity(identityDb)));

    /// <summary>
    /// Un pedido en Organización Industrial, que es de la OTRA carrera.
    /// </summary>
    /// <remarks>
    /// El padrón sintético pone sus ocho pedidos en la carrera de Informática, así
    /// que sin esta fila el ámbito del coordinador no se puede distinguir del
    /// departamental. Va en estado terminal para no ocupar cupo y no chocar con el
    /// índice de BR-designaciones-001.
    /// </remarks>
    private async Task AgregarPedidoDeOtraCarreraAsync(CancellationToken ct)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO designaciones.pedidos
                (id, numero, periodo_id, persona_id, materia_id, novedad, estado, prioritario)
            VALUES
                ('d5000000-0000-4000-8000-000000000099',
                 '2026-9099',
                 'd4000000-0000-4000-8000-000000000001',
                 'd0000000-0000-4000-8000-000000000005',
                 '70000000-0000-4000-8000-000000000201',
                 'Sin novedad', 'cancelado', FALSE)
            """, conexion);

        await comando.ExecuteNonQueryAsync(ct);
    }

    private sealed class UsuarioActualFalso(Guid id) : ICurrentUser
    {
        public string UserId => id.ToString();
        public string? Email => "test@unlam.edu.ar";
        public IReadOnlyList<string> Roles => [];
        public bool IsAuthenticated => true;
    }
}
