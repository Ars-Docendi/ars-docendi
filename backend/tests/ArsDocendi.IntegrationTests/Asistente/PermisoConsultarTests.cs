using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Identity.Desarrollo;
using ArsDocendi.Shared.Persistencia;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica el permiso de admisión al asistente y su siembra.
/// </summary>
/// <remarks>
/// El permiso es persistido —y no una lista de roles en código— porque la matriz
/// rol → permiso se edita desde /membresia-roles sin desplegar, y porque
/// `identity.roles` no es un catálogo cerrado: una lista embebida falla ABIERTA
/// con cualquier rol que no conozca.
/// </remarks>
[Collection(ColeccionPostgres.Nombre)]
public sealed class PermisoConsultarTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_permiso")
{
    private const string Recurso = "identity/011_identity_permiso_asistente.sql";

    private static readonly string[] RolesConAcceso =
    [
        "jefe_catedra", "coordinador_carrera", "secretaria",
        "decanato", "administrativo", "sys_admin",
    ];

    private static readonly Guid Jefe = Guid.Parse("a0000000-0000-4000-8000-000000000002");

    [Fact]
    public async Task Existe_exactamente_una_fila_del_permiso()
    {
        var filas = await EscalarAsync<long>(
            "SELECT count(*) FROM identity.permisos WHERE code = @code",
            ("code", Permisos.AsistenteConsultar));

        Assert.Equal(1, filas);
    }

    [Fact]
    public async Task Los_seis_roles_no_docente_lo_tienen_y_docente_no()
    {
        var conAcceso = await LeerRolesConElPermisoAsync();

        // sys_admin va explícito y no por herencia: su matriz se sembró con un
        // ARRAY(SELECT code FROM identity.permisos) evaluado cuando corrió aquella
        // migración, así que un permiso nuevo no le llega solo.
        Assert.Equal(RolesConAcceso.Order(), conAcceso.Order());
        Assert.DoesNotContain("docente", conAcceso);
    }

    [Fact]
    public async Task Los_siete_roles_de_sistema_tienen_una_decision_tomada()
    {
        var deSistema = await LeerColumnaAsync(
            "SELECT code FROM identity.roles WHERE es_sistema ORDER BY code");
        var conAcceso = await LeerRolesConElPermisoAsync();

        // Ninguno queda sin decisión: o está en la lista de acceso, o es docente,
        // que está excluido a propósito.
        var sinDecision = deSistema
            .Where(rol => !conAcceso.Contains(rol) && rol != "docente")
            .ToArray();

        Assert.Equal(7, deSistema.Count);
        Assert.Empty(sinDecision);
    }

    [Fact]
    public async Task Reaplicar_la_migracion_no_duplica_filas()
    {
        var antesPermisos = await EscalarAsync<long>("SELECT count(*) FROM identity.permisos");
        var antesMembresia = await EscalarAsync<long>(
            "SELECT count(*) FROM identity.rol_permisos rp "
            + "JOIN identity.permisos p ON p.id = rp.permiso_id WHERE p.code = @code",
            ("code", Permisos.AsistenteConsultar));

        await EjecutarMigracionAsync();

        Assert.Equal(antesPermisos, await EscalarAsync<long>("SELECT count(*) FROM identity.permisos"));
        Assert.Equal(antesMembresia, await EscalarAsync<long>(
            "SELECT count(*) FROM identity.rol_permisos rp "
            + "JOIN identity.permisos p ON p.id = rp.permiso_id WHERE p.code = @code",
            ("code", Permisos.AsistenteConsultar)));
    }

    [Fact]
    public async Task Un_rol_de_sistema_nuevo_sin_decision_rompe_la_migracion()
    {
        // La guarda es lo que hace que «los siete» signifique algo. Sin ella, un rol
        // de sistema agregado más adelante quedaría sin acceso y sin que nadie lo
        // haya decidido — el mismo modo de falla silencioso que la trampa de sys_admin.
        await EjecutarAsync(
            """
            INSERT INTO identity.roles (id, code, name, scope, es_sistema)
            VALUES (gen_random_uuid(), 'rol_nuevo_de_sistema', 'Rol nuevo', 'global', TRUE)
            """);

        var error = await Assert.ThrowsAsync<PostgresException>(EjecutarMigracionAsync);

        Assert.Contains("rol_nuevo_de_sistema", error.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    public void La_constante_esta_declarada_y_registrada()
    {
        // Una constante declarada pero ausente de Todos no produce política, y el
        // [Authorize] correspondiente lanza en el primer request, no al compilar.
        Assert.Equal("asistente.consultar", Permisos.AsistenteConsultar);
        Assert.Contains(Permisos.AsistenteConsultar, Permisos.Todos);
    }

    [Fact]
    public async Task La_politica_existe_en_el_Host_compuesto()
    {
        using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting($"ConnectionStrings:{CadenaDuena.Clave}", Cadena);
        });

        var proveedor = host.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var politica = await proveedor.GetPolicyAsync(Permisos.AsistenteConsultar);

        Assert.NotNull(politica);
    }

    [Fact]
    public async Task Conceder_y_revocar_cambia_la_autorizacion_sin_redesplegar()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);

        Assert.Contains(Permisos.AsistenteConsultar, await PermisosDelJefeAsync(ct));

        await EjecutarAsync(
            """
            DELETE FROM identity.rol_permisos
             WHERE rol_id = (SELECT id FROM identity.roles WHERE code = 'jefe_catedra')
               AND permiso_id = (SELECT id FROM identity.permisos WHERE code = @code)
            """, ("code", Permisos.AsistenteConsultar));

        // Mismo proceso, sin reiniciar nada: la autorización sale de la base en cada
        // request, no de una lista compilada.
        Assert.DoesNotContain(Permisos.AsistenteConsultar, await PermisosDelJefeAsync(ct));

        await EjecutarMigracionAsync();

        Assert.Contains(Permisos.AsistenteConsultar, await PermisosDelJefeAsync(ct));
    }

    // ------------------------------------------------------------------------------ apoyo

    private async Task<IReadOnlyList<string>> PermisosDelJefeAsync(CancellationToken ct)
    {
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        var identidad = await new ServicioIdentidadesDesarrollo(db)
            .ValidarAsync(Jefe, "jefe_catedra", ct);

        Assert.NotNull(identidad);
        return identidad.Permisos;
    }

    private Task EjecutarMigracionAsync() => EjecutarAsync(
        RecursosSql.Leer(typeof(IdentityDbContext).Assembly, Recurso));

    private async Task<IReadOnlyList<string>> LeerRolesConElPermisoAsync() =>
        await LeerColumnaAsync(
            """
            SELECT r.code
              FROM identity.rol_permisos rp
              JOIN identity.roles r ON r.id = rp.rol_id
              JOIN identity.permisos p ON p.id = rp.permiso_id
             WHERE p.code = @code
             ORDER BY r.code
            """, ("code", Permisos.AsistenteConsultar));

    private async Task<IReadOnlyList<string>> LeerColumnaAsync(
        string sql, params (string Nombre, object Valor)[] parametros)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = Preparar(conexion, sql, parametros);
        await using var lector = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        var valores = new List<string>();
        while (await lector.ReadAsync(TestContext.Current.CancellationToken))
        {
            valores.Add(lector.GetString(0));
        }

        return valores;
    }

    private async Task<T> EscalarAsync<T>(string sql, params (string Nombre, object Valor)[] parametros)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = Preparar(conexion, sql, parametros);
        return (T)(await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private async Task EjecutarAsync(string sql, params (string Nombre, object Valor)[] parametros)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = Preparar(conexion, sql, parametros);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static NpgsqlCommand Preparar(
        NpgsqlConnection conexion, string sql, (string Nombre, object Valor)[] parametros)
    {
        var comando = new NpgsqlCommand(sql, conexion);
        foreach (var (nombre, valor) in parametros)
        {
            comando.Parameters.AddWithValue(nombre, valor);
        }

        return comando;
    }

    private async Task EjecutarSeedAsync(CancellationToken ct)
    {
        var sql = await File.ReadAllTextAsync(
            Path.Combine(RaizRepositorio.Ruta(), "infra", "scripts", "seed-data", "sintetico.sql"), ct);
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = 60 };
        await comando.ExecuteNonQueryAsync(ct);
    }
}
