using System.Net;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Modules.Designaciones.Contracts.Queries;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure;
using Modules.Designaciones.Repositories;
using Modules.Designaciones.Services;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Designaciones;

/// <summary>
/// La ficha de un trámite, acotada a lo que el actor puede abrir.
/// </summary>
/// <remarks>
/// La ficha es lo que el asistente muestra con un botón «Abrir pedido». Si existiera
/// donde <c>GET /api/designaciones/pedidos/{id}</c> responde 403, el botón sería UI
/// ficticia: por eso el último test compara los dos caminos para el mismo actor y
/// los mismos trámites, en vez de repetir la regla.
/// </remarks>
public sealed class FichaDePedidoTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "ficha_pedido")
{
    /// <summary>Jefe de cátedra de Ingeniería de Software (materia 101).</summary>
    private static readonly Guid Jefe = Guid.Parse("a0000000-0000-4000-8000-000000000002");

    /// <summary>Coordinador de la carrera de Informática (carrera 201).</summary>
    private static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");

    /// <summary>Secretaría Académica: ámbito departamental.</summary>
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");

    private const string DeSuCatedra = "2026-9005";
    private const string DeOtraCatedra = "2026-9006";
    private const string DeOtraCarrera = "2026-9099";

    [Fact]
    public async Task El_jefe_obtiene_la_ficha_de_su_catedra_y_no_la_de_una_ajena()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await DejarAlJefeSoloConLaCatedraDeAsync(DeSuCatedra);
        var suyo = await IdDeAsync(DeSuCatedra);
        var ajeno = await IdDeAsync(DeOtraCatedra);
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var consultas = Consultas(Jefe, identityDb, db);

        var ficha = await consultas.FichaDePedidoAsync(suyo, ct);

        Assert.NotNull(ficha);
        Assert.Equal(suyo, ficha.Id);
        Assert.Equal(DeSuCatedra, ficha.Numero);
        Assert.False(string.IsNullOrWhiteSpace(ficha.Novedad));
        Assert.False(string.IsNullOrWhiteSpace(ficha.Materia));
        Assert.False(string.IsNullOrWhiteSpace(ficha.Periodo));
        Assert.False(string.IsNullOrWhiteSpace(ficha.EstadoEtiqueta));
        Assert.False(string.IsNullOrWhiteSpace(ficha.Cargo));
        Assert.False(string.IsNullOrWhiteSpace(ficha.Dedicacion));
        Assert.NotEqual(default, ficha.UltimaActividad);
        Assert.Null(await consultas.FichaDePedidoAsync(ajeno, ct));
    }

    [Fact]
    public async Task El_coordinador_obtiene_la_de_su_carrera_y_no_la_de_otra()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await AgregarPedidoDeOtraCarreraAsync(ct);
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var consultas = Consultas(Coordinador, identityDb, db);

        Assert.NotNull(await consultas.FichaDePedidoAsync(await IdDeAsync(DeSuCatedra), ct));
        Assert.NotNull(await consultas.FichaDePedidoAsync(await IdDeAsync(DeOtraCatedra), ct));
        Assert.Null(await consultas.FichaDePedidoAsync(await IdDeAsync(DeOtraCarrera), ct));
    }

    [Fact]
    public async Task Un_rol_global_obtiene_cualquiera()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await AgregarPedidoDeOtraCarreraAsync(ct);
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var consultas = Consultas(Secretaria, identityDb, db);

        foreach (var numero in new[] { DeSuCatedra, DeOtraCatedra, DeOtraCarrera })
        {
            Assert.NotNull(await consultas.FichaDePedidoAsync(await IdDeAsync(numero), ct));
        }
    }

    [Fact]
    public async Task Un_identificador_inexistente_devuelve_null_sin_romper()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);

        Assert.Null(await Consultas(Secretaria, identityDb, db).FichaDePedidoAsync(Guid.NewGuid(), ct));
        Assert.Null(await Consultas(Secretaria, identityDb, db).FichaDePedidoAsync(Guid.Empty, ct));
    }

    [Fact]
    public async Task La_ficha_incluye_cargo_y_dedicacion_solo_si_el_tramite_los_tiene()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        // Una baja no pide cargo ni dedicación (la base sólo se los exige a un alta o
        // a un cambio), así que es el trámite que la ficha tiene que dejar sin ellos.
        var id = await IdDeAsync("2026-9002");
        await EjecutarAsync(
            "UPDATE designaciones.pedidos SET novedad = 'Baja', cargo_solicitado_id = NULL, dedicacion_solicitada = NULL, "
            + "dedicacion_solicitada_id = NULL WHERE id = @id",
            ("id", id));
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);

        var ficha = await Consultas(Secretaria, identityDb, db).FichaDePedidoAsync(id, ct);

        Assert.NotNull(ficha);
        Assert.Null(ficha.Cargo);
        Assert.Null(ficha.Dedicacion);
    }

    [Fact]
    public async Task La_ficha_existe_si_y_solo_si_el_detalle_responde_200_para_el_mismo_actor()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await AgregarPedidoDeOtraCarreraAsync(ct);
        await DejarAlJefeSoloConLaCatedraDeAsync(DeSuCatedra);
        var numeros = new[] { DeSuCatedra, DeOtraCatedra, DeOtraCarrera };
        var ids = new Dictionary<string, Guid>();

        foreach (var numero in numeros)
        {
            ids[numero] = await IdDeAsync(numero);
        }

        var actores = new (Guid Usuario, string Rol)[]
        {
            (Jefe, RolesCircuito.JefeCatedra),
            (Coordinador, RolesCircuito.CoordinadorCarrera),
            (Secretaria, RolesCircuito.Secretaria),
        };

        using var host = CrearHost();

        foreach (var (usuario, rol) in actores)
        {
            using var cliente = host.CreateClient();
            cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, usuario.ToString());
            cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, rol);
            await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
            await using var db = PostgresFixture.CrearDesignaciones(Cadena);
            var consultas = Consultas(usuario, identityDb, db);

            foreach (var numero in numeros)
            {
                using var detalle = await cliente.GetAsync($"/api/designaciones/pedidos/{ids[numero]}", ct);
                var ficha = await consultas.FichaDePedidoAsync(ids[numero], ct);

                Assert.True(
                    (detalle.StatusCode == HttpStatusCode.OK) == (ficha is not null),
                    $"{rol} sobre {numero}: detalle {(int)detalle.StatusCode}, ficha {(ficha is null ? "ausente" : "presente")}.");
            }
        }
    }

    // ------------------------------------------------------------------ apoyo

    private static ServicioConsultasDesignaciones Consultas(
        Guid usuarioId,
        IdentityDbContext identityDb,
        DesignacionesDbContext db)
    {
        var identity = new ConsultasIdentity(identityDb);

        return new ServicioConsultasDesignaciones(
            new RepositorioPedidos(db),
            new ResolutorActor(new UsuarioActualFalso(usuarioId), identity),
            identity);
    }

    private WebApplicationFactory<Program> CrearHost() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:ArsDocendi", Cadena);
            builder.UseSetting($"{AutenticacionDesarrolloOptions.Seccion}:Enabled", "true");
        });

    private Task<Guid> IdDeAsync(string numero) =>
        EscalarAsync<Guid>("SELECT id FROM designaciones.pedidos WHERE numero = @numero", ("numero", numero));

    /// <summary>
    /// El seed le da al jefe varias materias; acá se lo deja a cargo únicamente de
    /// la cátedra del pedido dado, para que «ajena» lo sea de verdad.
    /// </summary>
    private Task DejarAlJefeSoloConLaCatedraDeAsync(string numero) =>
        EjecutarAsync(
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
            ("suyo", numero));

    /// <summary>Un pedido en Organización Industrial, de la OTRA carrera. Terminal, para no ocupar cupo.</summary>
    private async Task AgregarPedidoDeOtraCarreraAsync(CancellationToken ct)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO designaciones.pedidos
                (id, numero, periodo_id, persona_id, materia_id, carrera_id, novedad, estado, prioritario)
            VALUES
                ('d5000000-0000-4000-8000-000000000099',
                 '2026-9099',
                 'd4000000-0000-4000-8000-000000000001',
                 'd0000000-0000-4000-8000-000000000005',
                 '70000000-0000-4000-8000-000000000201',
                 'c0000000-0000-4000-8000-000000000202',
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
