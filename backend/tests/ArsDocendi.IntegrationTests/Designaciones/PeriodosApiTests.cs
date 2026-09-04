using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Aplicacion;
using Microsoft.AspNetCore.Mvc;
using Modules.Designaciones.Api;
using Modules.Designaciones.Repositories;
using Modules.Designaciones.Services;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Designaciones;

[Collection(ColeccionPostgres.Nombre)]
public sealed class PeriodosApiTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "periodos_api")
{
    [Fact]
    public async Task Endpoint_cubre_crear_editar_activar_desactivar_listar_y_eliminar()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var servicio = new ServicioPeriodos(new RepositorioPeriodos(db));
        var controller = new PeriodosController(servicio);

        var respuesta = await controller.Crear(Datos("Período inicial"), ct);
        var creado = Assert.IsType<PeriodoDto>(Assert.IsType<CreatedAtActionResult>(respuesta.Result).Value);
        var editado = await controller.Editar(creado.Id, Datos("Período editado") with
        {
            CargaHasta = new DateOnly(2026, 3, 15),
            Version = creado.Version,
        }, ct);
        Assert.Equal("Período editado", editado.Nombre);
        Assert.Equal(new DateOnly(2026, 3, 15), editado.CargaHasta);

        var activo = await controller.Activar(
            creado.Id, new CambiarEstadoPeriodoDto(editado.Version), ct);
        Assert.True(activo.Activo);
        var inactivo = await controller.Desactivar(
            creado.Id, new CambiarEstadoPeriodoDto(activo.Version), ct);
        Assert.False(inactivo.Activo);
        Assert.Single(await controller.Listar(ct));

        Assert.IsType<NoContentResult>(await controller.Eliminar(creado.Id, ct));
        Assert.Empty(await controller.Listar(ct));
    }

    [Fact]
    public async Task Segundo_periodo_activo_devuelve_conflicto_estable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var servicio = new ServicioPeriodos(new RepositorioPeriodos(db));
        await servicio.CrearAsync(Datos("Activo uno") with { Activo = true }, ct);

        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() =>
            servicio.CrearAsync(Datos("Activo dos") with { Activo = true }, ct));

        Assert.Equal("periodo-active-conflict", error.Codigo);
        Assert.Single(await servicio.ListarAsync(ct));
    }

    [Fact]
    public async Task Periodo_referenciado_no_se_elimina()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var servicio = new ServicioPeriodos(new RepositorioPeriodos(db));
        var periodoId = Guid.Parse("d4000000-0000-4000-8000-000000000001");

        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() =>
            servicio.EliminarAsync(periodoId, ct));

        Assert.Equal("periodo-in-use", error.Codigo);
        Assert.NotNull(await servicio.ObtenerAsync(periodoId, ct));
    }

    private static GuardarPeriodoDto Datos(string nombre) => new(
        nombre,
        new DateOnly(2026, 1, 1),
        new DateOnly(2026, 2, 28),
        new DateOnly(2026, 3, 1),
        new DateOnly(2026, 7, 31),
        false);

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
