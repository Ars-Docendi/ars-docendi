using ArsDocendi.Host.Administracion;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Identity.Administracion;
using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore;
using Modules.Designaciones.Contracts.Administracion;
using Modules.Designaciones.Infrastructure;
using Modules.Designaciones.Repositories;
using Modules.Designaciones.Services;

namespace ArsDocendi.IntegrationTests.Identity;

public sealed class AdministracionPersonasTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "admin_personas")
{
    private static readonly Guid Materia = Guid.Parse("70000000-0000-4000-8000-000000000101");
    private static readonly Guid MateriaB = Guid.Parse("70000000-0000-4000-8000-000000000102");
    private static readonly Guid Carrera = Guid.Parse("c0000000-0000-4000-8000-000000000201");
    private static readonly Guid CargoTitular = Guid.Parse("c3000000-0000-4000-8000-000000000001");
    private static readonly Guid DedicacionSimple = Guid.Parse("d6000000-0000-4000-8000-000000000001");
    private static readonly Guid RolDocente = Guid.Parse("a1000000-0000-4000-8000-000000000001");
    private static readonly Guid RolJefe = Guid.Parse("a1000000-0000-4000-8000-000000000002");
    private static readonly Guid RolCoordinador = Guid.Parse("a1000000-0000-4000-8000-000000000003");
    private static readonly Guid RolSecretaria = Guid.Parse("a1000000-0000-4000-8000-000000000004");

    [Fact]
    public async Task Alta_con_rol_de_departamento_no_exige_asignaciones()
    {
        var ct = TestContext.Current.CancellationToken;
        var (servicio, identity, modulo) = await PrepararAsync(ct);
        await using var _i = identity; await using var _m = modulo;

        var creada = await servicio.GuardarAsync(null, Datos("70000001", "sec@unlam.edu.ar",
            [new GuardarAsignacionRolDto(RolSecretaria)], []), ct);

        Assert.True(creada.TieneCuenta);
        Assert.Equal(["secretaria"], creada.Roles.Select(r => r.Codigo));
        Assert.Empty(creada.Asignaciones);
    }

    [Fact]
    public async Task Persona_sin_rol_se_rechaza()
    {
        var ct = TestContext.Current.CancellationToken;
        var (servicio, identity, modulo) = await PrepararAsync(ct);
        await using var _i = identity; await using var _m = modulo;

        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() =>
            servicio.GuardarAsync(null, Datos("70000002", "sinrol@unlam.edu.ar", [], []), ct));

        Assert.Equal(TipoErrorAplicacion.Validacion, error.Tipo);
        Assert.True(error.Errores!.ContainsKey("membresias"));
    }

    [Fact]
    public async Task Docente_sin_asignaciones_se_rechaza()
    {
        var ct = TestContext.Current.CancellationToken;
        var (servicio, identity, modulo) = await PrepararAsync(ct);
        await using var _i = identity; await using var _m = modulo;

        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => servicio.GuardarAsync(null,
            Datos("70000003", "doc@unlam.edu.ar", [Docente()], []), ct));

        Assert.True(error.Errores!.ContainsKey("designaciones"));
    }

    [Fact]
    public async Task Dni_se_guarda_por_digitos_y_se_detecta_repetido()
    {
        var ct = TestContext.Current.CancellationToken;
        var (servicio, identity, modulo) = await PrepararAsync(ct);
        await using var _i = identity; await using var _m = modulo;
        var creada = await servicio.GuardarAsync(null, Datos("28.456.123", "dni@unlam.edu.ar",
            [new GuardarAsignacionRolDto(RolSecretaria)], []), ct);

        Assert.Equal("28456123", creada.Documento);
        var repetido = await servicio.VerificarDocumentoAsync("28 456 123", null, ct);
        Assert.True(repetido.Existe);
        Assert.Equal(creada.PersonaId, repetido.PersonaId);
        Assert.Equal("Lovelace, Ada", repetido.Nombre);
        Assert.False((await servicio.VerificarDocumentoAsync("28456123", creada.PersonaId, ct)).Existe);
        var conflicto = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => servicio.GuardarAsync(null,
            Datos("28456123", "otra@unlam.edu.ar", [new GuardarAsignacionRolDto(RolSecretaria)], []), ct));
        Assert.Equal("identity-document-conflict", conflicto.Codigo);
    }

    [Fact]
    public async Task Ambito_de_cada_rol_se_valida()
    {
        var ct = TestContext.Current.CancellationToken;
        var (servicio, identity, modulo) = await PrepararAsync(ct);
        await using var _i = identity; await using var _m = modulo;

        var coordinador = await servicio.GuardarAsync(null, Datos("70000004", "coord@unlam.edu.ar",
            [new GuardarAsignacionRolDto(RolCoordinador, CarreraId: Carrera)], []), ct);
        Assert.Equal(["coordinador_carrera"], coordinador.Roles.Select(r => r.Codigo));

        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => servicio.GuardarAsync(null,
            Datos("70000005", "jefe@unlam.edu.ar", [new GuardarAsignacionRolDto(RolJefe, MateriaId: Materia, CarreraId: Carrera)],
                [Designacion(Materia)]), ct));
        Assert.Equal("identity-role-scope-conflict", error.Codigo);
    }

    [Fact]
    public async Task Edicion_reemplaza_todos_los_roles_y_las_asignaciones()
    {
        var ct = TestContext.Current.CancellationToken;
        var (servicio, identity, modulo) = await PrepararAsync(ct);
        await using var _i = identity; await using var _m = modulo;
        var creada = await servicio.GuardarAsync(null, Datos("70000006", "edit@unlam.edu.ar",
            [Docente(), new GuardarAsignacionRolDto(RolSecretaria)], [Designacion(Materia)]), ct);

        var editada = await servicio.GuardarAsync(creada.PersonaId, Datos("70000006", "edit@unlam.edu.ar",
            [new GuardarAsignacionRolDto(RolSecretaria)], []) with { Version = creada.Version }, ct);

        Assert.Equal(["secretaria"], editada.Roles.Select(r => r.Codigo));
        Assert.Empty(editada.Asignaciones);
    }

    [Fact]
    public async Task Ambito_del_jefe_acota_listado_y_detalle()
    {
        var ct = TestContext.Current.CancellationToken;
        var (servicio, identity, modulo) = await PrepararAsync(ct);
        await using var _i = identity; await using var _m = modulo;
        var enA = await servicio.GuardarAsync(null, Datos("70000007", "a@unlam.edu.ar", [Docente()], [Designacion(Materia)]), ct);
        var enB = await servicio.GuardarAsync(null, Datos("70000008", "b@unlam.edu.ar",
            [new GuardarAsignacionRolDto(RolDocente, MateriaId: MateriaB, CarreraId: Carrera)], [Designacion(MateriaB)]), ct);
        IReadOnlySet<Guid> soloA = new HashSet<Guid> { Materia };

        var visibles = await servicio.ListarAsync(soloA, ct);

        Assert.Contains(visibles, p => p.PersonaId == enA.PersonaId);
        Assert.DoesNotContain(visibles, p => p.PersonaId == enB.PersonaId);
        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => servicio.ObtenerAsync(enB.PersonaId, soloA, ct));
        Assert.Equal(TipoErrorAplicacion.NoEncontrado, error.Tipo);
        Assert.True(await servicio.EstaEnAmbitoAsync(enA.PersonaId, soloA, ct));
        Assert.False(await servicio.EstaEnAmbitoAsync(enB.PersonaId, soloA, ct));
    }

    private async Task<(ServicioAdministracionPersonas, IdentityDbContext, DesignacionesDbContext)> PrepararAsync(CancellationToken ct)
    {
        var identity = PostgresFixture.CrearIdentity(Cadena);
        await PrepararCatalogoAsync(identity, ct);
        var modulo = PostgresFixture.CrearDesignaciones(Cadena);
        var servicio = new ServicioAdministracionPersonas(
            new RepositorioDocentes(identity),
            new ServicioAdministracionDesignaciones(new RepositorioDesignaciones(modulo)),
            new UnidadDeTrabajoAdministracion());
        return (servicio, identity, modulo);
    }

    private static GuardarAsignacionRolDto Docente() => new(RolDocente, MateriaId: Materia, CarreraId: Carrera);

    private static GuardarDesignacionVigenteDto Designacion(Guid materia) =>
        new(materia, Carrera, CargoTitular, DedicacionSimple, 12);

    private static GuardarPersonaDto Datos(
        string documento,
        string upn,
        IReadOnlyList<GuardarAsignacionRolDto> membresias,
        IReadOnlyList<GuardarDesignacionVigenteDto> designaciones) => new(
            "Ada", "Lovelace", documento, "L-" + documento.Replace(".", "").Replace(" ", ""), null,
            new DateOnly(1985, 12, 10), null, upn, membresias, designaciones);

    private static async Task PrepararCatalogoAsync(IdentityDbContext identity, CancellationToken ct)
    {
        if (await identity.Carreras.AnyAsync(c => c.Id == Carrera, ct)) return;
        identity.Carreras.Add(new Carrera { Id = Carrera, Codigo = "INFO", Nombre = "Ingeniería Informática", Activo = true, CreadoEn = DateTimeOffset.UtcNow });
        identity.Materias.Add(new Materia { Id = Materia, Codigo = "03500", Nombre = "Matemática Discreta", Activo = true, CreadoEn = DateTimeOffset.UtcNow });
        identity.Materias.Add(new Materia { Id = MateriaB, Codigo = "03620", Nombre = "Algoritmos", Activo = true, CreadoEn = DateTimeOffset.UtcNow });
        var plan = Guid.NewGuid();
        identity.Planes.Add(new Plan { Id = plan, CarreraId = Carrera, Codigo = "TEST", Nombre = "Plan test", Vigente = true, Activo = true, CreadoEn = DateTimeOffset.UtcNow });
        identity.MateriasPlan.Add(new MateriaPlan { Id = Guid.NewGuid(), PlanId = plan, MateriaId = Materia, Activo = true, CreadoEn = DateTimeOffset.UtcNow });
        identity.MateriasPlan.Add(new MateriaPlan { Id = Guid.NewGuid(), PlanId = plan, MateriaId = MateriaB, Activo = true, CreadoEn = DateTimeOffset.UtcNow });
        await identity.SaveChangesAsync(ct);
        identity.ChangeTracker.Clear();
    }
}
