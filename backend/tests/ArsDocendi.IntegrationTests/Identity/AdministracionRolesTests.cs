using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Identity.Administracion;
using Microsoft.EntityFrameworkCore;

namespace ArsDocendi.IntegrationTests.Identity;

[Collection(ColeccionPostgres.Nombre)]
public sealed class AdministracionRolesTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "admin_roles")
{
    private static readonly Guid RolDocente = Guid.Parse("a1000000-0000-4000-8000-000000000001");
    private static readonly Guid RolSecretaria = Guid.Parse("a1000000-0000-4000-8000-000000000004");

    [Fact]
    public async Task Listado_es_sin_tracking_y_creacion_copia_permisos_una_sola_vez()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        var servicio = CrearServicio(db);
        var baseRol = await servicio.ObtenerAsync(RolSecretaria, ct);
        db.ChangeTracker.Clear();

        var creado = await servicio.CrearAsync(new CrearRolDto(
            "Coordinación de Área", "Permisos operativos", "carrera", RolSecretaria), ct);

        Assert.Equal("coordinacion_de_area", creado.Codigo);
        Assert.False(creado.EsSistema);
        Assert.Equal(baseRol.Permisos.Select(p => p.Id).Order(), creado.Permisos.Select(p => p.Id).Order());
        db.ChangeTracker.Clear();
        var listado = await servicio.ListarAsync(ct);
        Assert.Contains(listado, r => r.Id == creado.Id);
        Assert.Empty(db.ChangeTracker.Entries());

        await servicio.ReemplazarPermisosAsync(
            RolSecretaria,
            new ReemplazarPermisosDto([], baseRol.Version),
            ct);
        db.ChangeTracker.Clear();
        var copia = await servicio.ObtenerAsync(creado.Id, ct);
        Assert.NotEmpty(copia.Permisos);
    }

    [Fact]
    public async Task Edicion_persiste_y_protege_el_ambito_de_roles_de_sistema()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        var servicio = CrearServicio(db);
        var sistema = await servicio.ObtenerAsync(RolDocente, ct);

        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => servicio.EditarAsync(
            RolDocente,
            new EditarRolDto("Docente modificado", "No debe persistir", "global", sistema.Version),
            ct));

        Assert.Equal("identity-protected-role", error.Codigo);
        db.ChangeTracker.Clear();
        var sinCambios = await servicio.ObtenerAsync(RolDocente, ct);
        Assert.Equal("materia", sinCambios.Ambito);
        Assert.Equal("Docente", sinCambios.Nombre);

        var creado = await servicio.CrearAsync(
            new CrearRolDto("Soporte Académico", "Inicial", "global"), ct);
        var editado = await servicio.EditarAsync(creado.Id,
            new EditarRolDto("Soporte Curricular", "Actualizado", "carrera", creado.Version), ct);
        Assert.Equal("Soporte Curricular", editado.Nombre);
        Assert.Equal("carrera", editado.Ambito);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Reemplazo_invalido_no_modifica_la_membresia(bool duplicado)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        var servicio = CrearServicio(db);
        var rol = await servicio.ObtenerAsync(RolSecretaria, ct);
        var originales = rol.Permisos.Select(p => p.Id).Order().ToArray();
        var ids = duplicado
            ? new[] { originales[0], originales[0] }
            : new[] { Guid.Parse("ffffffff-ffff-4fff-8fff-ffffffffffff") };

        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() =>
            servicio.ReemplazarPermisosAsync(
                rol.Id,
                new ReemplazarPermisosDto(ids, rol.Version),
                ct));

        Assert.Equal("identity-permission-invalid", error.Codigo);
        db.ChangeTracker.Clear();
        var posterior = await servicio.ObtenerAsync(rol.Id, ct);
        Assert.Equal(originales, posterior.Permisos.Select(p => p.Id).Order().ToArray());
    }

    [Fact]
    public async Task Reemplazo_de_permisos_detecta_version_concurrente()
    {
        var ct = TestContext.Current.CancellationToken;
        Guid rolId;
        uint version;
        Guid permiso1;
        Guid permiso2;
        await using (var db = PostgresFixture.CrearIdentity(Cadena))
        {
            var servicio = CrearServicio(db);
            var rol = await servicio.CrearAsync(new CrearRolDto(
                "Auditoría Local", "Rol de prueba", "global"), ct);
            var permisos = await servicio.ListarPermisosAsync(ct);
            rolId = rol.Id;
            version = rol.Version;
            permiso1 = permisos[0].Id;
            permiso2 = permisos[1].Id;
        }
        await using (var primero = PostgresFixture.CrearIdentity(Cadena))
        {
            await CrearServicio(primero).ReemplazarPermisosAsync(
                rolId, new ReemplazarPermisosDto([permiso1], version), ct);
        }
        await using (var segundo = PostgresFixture.CrearIdentity(Cadena))
        {
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
                CrearServicio(segundo).ReemplazarPermisosAsync(
                    rolId, new ReemplazarPermisosDto([permiso2], version), ct));
        }
        await using var verificacion = PostgresFixture.CrearIdentity(Cadena);
        var guardado = await CrearServicio(verificacion).ObtenerAsync(rolId, ct);
        Assert.Equal([permiso1], guardado.Permisos.Select(p => p.Id));
    }

    private static ServicioRoles CrearServicio(ArsDocendi.Shared.Identity.IdentityDbContext db) =>
        new(db, new RepositorioRoles(db));
}
