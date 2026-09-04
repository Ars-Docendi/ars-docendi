using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using Modules.Designaciones.Repositories;
using Modules.Designaciones.Services;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Designaciones;

[Collection(ColeccionPostgres.Nombre)]
public sealed class CatalogosDesignacionesTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "catalogos_designaciones")
{
    private static readonly Guid PersonaConPedidoVivo = Guid.Parse("d0000000-0000-4000-8000-000000000001");
    private static readonly Guid PersonaConPedidoRechazado = Guid.Parse("d0000000-0000-4000-8000-000000000015");

    [Fact]
    public async Task Catalogos_respetan_materias_y_carreras_del_actor()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var designacionesDb = PostgresFixture.CrearDesignaciones(Cadena);

        var jefe = await CrearServicio(
            Guid.Parse("a0000000-0000-4000-8000-000000000002"), identityDb, designacionesDb)
            .ObtenerAsync(ct);
        var coordinador = await CrearServicio(
            Guid.Parse("a0000000-0000-4000-8000-000000000003"), identityDb, designacionesDb)
            .ObtenerAsync(ct);
        var secretaria = await CrearServicio(
            Guid.Parse("a0000000-0000-4000-8000-000000000004"), identityDb, designacionesDb)
            .ObtenerAsync(ct);

        Assert.Single(jefe.Materias);
        Assert.All(jefe.Materias, m => Assert.Equal(
            Guid.Parse("70000000-0000-4000-8000-000000000101"), m.Id));
        Assert.Equal(4, coordinador.Materias.Count);
        Assert.All(coordinador.Materias, m => Assert.Equal(
            Guid.Parse("c0000000-0000-4000-8000-000000000201"), m.CarreraId));
        Assert.Equal(6, secretaria.Materias.Count);
    }

    [Fact]
    public async Task Catalogos_devuelven_periodo_personas_elegibles_y_cargos_activos()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var designacionesDb = PostgresFixture.CrearDesignaciones(Cadena);
        var catalogos = await CrearServicio(
            Guid.Parse("a0000000-0000-4000-8000-000000000004"), identityDb, designacionesDb)
            .ObtenerAsync(ct);

        Assert.NotNull(catalogos.PeriodoActivo);
        Assert.Equal(3, catalogos.Periodos.Count);
        Assert.Equal(6, catalogos.Cargos.Count);
        Assert.DoesNotContain(catalogos.Personas, p => p.Id == PersonaConPedidoVivo);
        Assert.Contains(catalogos.Personas, p => p.Id == PersonaConPedidoRechazado);
        Assert.Contains("Categoría 6", catalogos.Dedicaciones);
        Assert.Contains("Cambio de cargo o dedicación", catalogos.Novedades);
    }

    private static ServicioCatalogosDesignaciones CrearServicio(
        Guid usuarioId,
        IdentityDbContext identityDb,
        Modules.Designaciones.Infrastructure.DesignacionesDbContext designacionesDb)
    {
        var identity = new ConsultasIdentity(identityDb);
        return new ServicioCatalogosDesignaciones(
            new RepositorioCatalogosDesignaciones(designacionesDb),
            identity,
            new ResolutorActor(new UsuarioActualFalso(usuarioId), identity));
    }

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

    private sealed class UsuarioActualFalso(Guid id) : ICurrentUser
    {
        public string UserId => id.ToString();
        public string? Email => "test@unlam.edu.ar";
        public IReadOnlyList<string> Roles => [];
        public bool IsAuthenticated => true;
    }
}
