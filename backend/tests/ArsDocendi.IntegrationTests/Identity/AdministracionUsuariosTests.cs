using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Identity.Administracion;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Identity;

[Collection(ColeccionPostgres.Nombre)]
public sealed class AdministracionUsuariosTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "admin_usuarios")
{
    private static readonly Guid RolSecretaria = Guid.Parse("a1000000-0000-4000-8000-000000000004");
    private static readonly Guid RolJefe = Guid.Parse("a1000000-0000-4000-8000-000000000002");

    [Fact]
    public async Task Listado_incluye_persona_roles_y_ambitos_sin_tracking()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        var servicio = CrearServicio(db);

        var usuarios = await servicio.ListarAsync(ct);

        Assert.Equal(8, usuarios.Count);
        Assert.DoesNotContain(usuarios, u => u.Upn == "demo@unlam.edu.ar");
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Alta_valida_se_persiste_con_persona_y_rol()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        var servicio = CrearServicio(db);

        var creado = await servicio.CrearAsync(DatosValidos("alta@unlam.edu.ar", "50111222"), ct);

        db.ChangeTracker.Clear();
        var recuperado = await servicio.ObtenerAsync(creado.Id, ct);
        Assert.True(recuperado.Activo);
        Assert.Equal("Ada", recuperado.Nombre);
        Assert.Single(recuperado.Roles, r => r.Codigo == "secretaria");
    }

    [Fact]
    public async Task Scope_invalido_no_escribe_persona_ni_usuario()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        var servicio = CrearServicio(db);
        var datos = DatosValidos("scope@unlam.edu.ar", "50222333") with
        {
            Roles = [new GuardarAsignacionRolDto(RolJefe)],
        };

        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => servicio.CrearAsync(datos, ct));

        Assert.Equal("identity-role-scope-conflict", error.Codigo);
        Assert.False(await db.Usuarios.AnyAsync(u => u.Upn == datos.Upn, ct));
        Assert.False(await db.Personas.AnyAsync(p => p.Documento == datos.Documento, ct));
    }

    [Fact]
    public async Task Edicion_invalida_conserva_datos_y_roles_anteriores()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        var servicio = CrearServicio(db);
        var creado = await servicio.CrearAsync(DatosValidos("editar@unlam.edu.ar", "50333444"), ct);
        var invalido = DatosValidos("editar@unlam.edu.ar", "50333444") with
        {
            Nombre = "Nombre cambiado",
            Version = creado.Version,
            Roles = [new GuardarAsignacionRolDto(RolJefe)],
        };

        await Assert.ThrowsAsync<ExcepcionAplicacion>(() => servicio.EditarAsync(creado.Id, invalido, ct));

        db.ChangeTracker.Clear();
        var recuperado = await servicio.ObtenerAsync(creado.Id, ct);
        Assert.Equal("Ada", recuperado.Nombre);
        Assert.Single(recuperado.Roles, r => r.Codigo == "secretaria");
    }

    [Fact]
    public async Task Dos_altas_concurrentes_traducen_el_conflicto_de_upn()
    {
        var ct = TestContext.Current.CancellationToken;
        var datos1 = DatosValidos("race@unlam.edu.ar", "50444555");
        var datos2 = DatosValidos("race@unlam.edu.ar", "50444556") with { Legajo = "R-2" };

        var resultados = await Task.WhenAll(
            IntentarCrearAsync(datos1, ct),
            IntentarCrearAsync(datos2, ct));

        Assert.Single(resultados, r => r is null);
        var error = Assert.Single(resultados.OfType<ExcepcionAplicacion>());
        Assert.Equal("identity-upn-conflict", error.Codigo);
        await using var verificacion = PostgresFixture.CrearIdentity(Cadena);
        Assert.Equal(1, await verificacion.Usuarios.CountAsync(u => u.Upn == "race@unlam.edu.ar", ct));
    }

    [Fact]
    public async Task Documento_duplicado_no_deja_escrituras_parciales()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        var servicio = CrearServicio(db);
        await servicio.CrearAsync(DatosValidos("primera@unlam.edu.ar", "50666777"), ct);
        db.ChangeTracker.Clear();

        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => servicio.CrearAsync(
            DatosValidos("segunda@unlam.edu.ar", "50666777") with { Legajo = "L-2" }, ct));

        Assert.Equal("identity-document-conflict", error.Codigo);
        Assert.False(await db.Usuarios.AnyAsync(u => u.Upn == "segunda@unlam.edu.ar", ct));
        Assert.Equal(1, await db.Personas.CountAsync(p => p.Documento == "50666777", ct));
    }

    [Fact]
    public async Task Activar_y_desactivar_persiste_entre_contextos()
    {
        var ct = TestContext.Current.CancellationToken;
        Guid id;
        await using (var db = PostgresFixture.CrearIdentity(Cadena))
        {
            var servicio = CrearServicio(db);
            var creado = await servicio.CrearAsync(DatosValidos("estado@unlam.edu.ar", "50555666"), ct);
            id = creado.Id;
            Assert.False((await servicio.CambiarEstadoAsync(id, false, creado.Version, ct)).Activo);
        }
        await using (var db = PostgresFixture.CrearIdentity(Cadena))
        {
            var servicio = CrearServicio(db);
            var inactivo = await servicio.ObtenerAsync(id, ct);
            Assert.False(inactivo.Activo);
            var consultas = new ConsultasIdentity(db);
            Assert.False(await consultas.TieneRolGlobalAsync(id, "secretaria", ct));
            Assert.Empty(await consultas.ObtenerCodigosDePermisosAsync(id, ct));
            Assert.True((await servicio.CambiarEstadoAsync(id, true, inactivo.Version, ct)).Activo);
        }
    }

    [Fact]
    public async Task Cambio_de_estado_con_version_obsoleta_detecta_concurrencia()
    {
        var ct = TestContext.Current.CancellationToken;
        uint versionInicial;
        Guid id;
        await using (var db = PostgresFixture.CrearIdentity(Cadena))
        {
            var creado = await CrearServicio(db).CrearAsync(
                DatosValidos("concurrencia@unlam.edu.ar", "50777888"), ct);
            id = creado.Id;
            versionInicial = creado.Version;
        }
        await using (var primero = PostgresFixture.CrearIdentity(Cadena))
        {
            await CrearServicio(primero).CambiarEstadoAsync(id, false, versionInicial, ct);
        }
        await using (var segundo = PostgresFixture.CrearIdentity(Cadena))
        {
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
                CrearServicio(segundo).CambiarEstadoAsync(id, true, versionInicial, ct));
        }
    }

    private async Task<Exception?> IntentarCrearAsync(GuardarUsuarioDto datos, CancellationToken ct)
    {
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        try
        {
            await CrearServicio(db).CrearAsync(datos, ct);
            return null;
        }
        catch (Exception error)
        {
            return error;
        }
    }

    private static ServicioUsuarios CrearServicio(ArsDocendi.Shared.Identity.IdentityDbContext db) =>
        new(db, new RepositorioUsuarios(db));

    private static GuardarUsuarioDto DatosValidos(string upn, string documento) => new(
        "Ada", "Lovelace", documento, $"L-{documento}", null,
        new DateOnly(1990, 1, 1), null, upn,
        [new GuardarAsignacionRolDto(RolSecretaria)]);

    private async Task EjecutarSeedAsync(CancellationToken ct)
    {
        var sql = await File.ReadAllTextAsync(
            Path.Combine(BuscarRaizRepositorio(), "infra", "scripts", "seed-data", "sintetico.sql"), ct);
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = 60 };
        await comando.ExecuteNonQueryAsync(ct);
    }

    private static string BuscarRaizRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null)
        {
            if (File.Exists(Path.Combine(directorio.FullName, "CLAUDE.md"))) return directorio.FullName;
            directorio = directorio.Parent;
        }
        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
    }
}
