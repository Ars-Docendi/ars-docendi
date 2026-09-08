using ArsDocendi.Host.Administracion;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Identity.Administracion;
using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore;
using Modules.Designaciones.Contracts.Administracion;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure;
using Modules.Designaciones.Repositories;
using Modules.Designaciones.Services;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Identity;

[Collection(ColeccionPostgres.Nombre)]
public sealed class AdministracionDocentesTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "admin_docentes")
{
    private static readonly Guid Materia = Guid.Parse("70000000-0000-4000-8000-000000000101");
    private static readonly Guid Carrera = Guid.Parse("c0000000-0000-4000-8000-000000000201");
    private static readonly Guid CargoTitular = Guid.Parse("c3000000-0000-4000-8000-000000000001");
    private static readonly Guid CargoAdjunto = Guid.Parse("c3000000-0000-4000-8000-000000000003");
    private static readonly Guid RolDocente = Guid.Parse("a1000000-0000-4000-8000-000000000001");
    private static readonly Guid RolJefe = Guid.Parse("a1000000-0000-4000-8000-000000000002");
    private static readonly Guid MateriaB = Guid.Parse("70000000-0000-4000-8000-000000000102");
    private static readonly Guid PersonaActiva = Guid.Parse("d0000000-0000-4000-8000-000000000001");
    private static readonly Guid PersonaInactiva = Guid.Parse("d0000000-0000-4000-8000-000000000008");
    private static readonly Guid PersonaSinCuenta = Guid.Parse("d0000000-0000-4000-8000-000000000010");

    [Fact]
    public async Task Alta_de_persona_nueva_confirma_cuenta_rol_y_designacion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        await PrepararCatalogoAsync(identity, ct);
        await using var modulo = PostgresFixture.CrearDesignaciones(Cadena);
        var servicio = CrearServicio(identity, modulo);

        var creado = await servicio.GuardarAsync(null, DatosValidos(null), ct);

        Assert.True(creado.TieneCuenta);
        Assert.True(creado.Activo);
        Assert.Equal(["docente"], creado.Roles.Select(r => r.Codigo));
        var asignacion = Assert.Single(creado.Asignaciones);
        Assert.Equal(CargoTitular, asignacion.CargoId);
        Assert.Equal(12, asignacion.Horas);
        identity.ChangeTracker.Clear();
        modulo.ChangeTracker.Clear();
        Assert.True(await identity.Personas.AnyAsync(p => p.Id == creado.PersonaId, ct));
        Assert.True(await modulo.Designaciones.AnyAsync(d =>
            d.PersonaId == creado.PersonaId && d.VigenteHasta == null, ct));
    }

    [Fact]
    public async Task Alta_sobre_persona_existente_crea_cuenta_sin_duplicar_persona()
    {
        var ct = TestContext.Current.CancellationToken;
        var personaId = Guid.NewGuid();
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        await PrepararCatalogoAsync(identity, ct);
        identity.Personas.Add(new Persona
        {
            Id = personaId,
            Documento = "60111222",
            Nombre = "Grace",
            Apellido = "Hopper",
            CreadoEn = DateTimeOffset.UtcNow,
        });
        await identity.SaveChangesAsync(ct);
        identity.ChangeTracker.Clear();
        await using var modulo = PostgresFixture.CrearDesignaciones(Cadena);
        var servicio = CrearServicio(identity, modulo);

        var creado = await servicio.GuardarAsync(null, DatosValidos(personaId) with
        {
            Documento = "60111222",
            Upn = "grace.hopper@unlam.edu.ar",
        }, ct);

        Assert.Equal(personaId, creado.PersonaId);
        Assert.True(creado.TieneCuenta);
        Assert.Equal(1, await identity.Personas.CountAsync(p => p.Id == personaId, ct));
        Assert.True(await identity.Usuarios.AnyAsync(u => u.PersonaId == personaId, ct));
    }

    [Fact]
    public async Task Edicion_reemplaza_datos_roles_cargo_y_horas()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        await PrepararCatalogoAsync(identity, ct);
        await using var modulo = PostgresFixture.CrearDesignaciones(Cadena);
        var servicio = CrearServicio(identity, modulo);
        var creado = await servicio.GuardarAsync(null, DatosValidos(null), ct);

        var editado = await servicio.GuardarAsync(creado.PersonaId, DatosValidos(creado.PersonaId) with
        {
            Nombre = "Ada editada",
            Version = creado.Version,
            Membresias =
            [
                new GuardarAsignacionRolDto(RolDocente, Materia, Carrera),
                new GuardarAsignacionRolDto(RolJefe, Materia, Carrera),
            ],
            Designaciones = [new GuardarDesignacionVigenteDto(Materia, CargoAdjunto, Guid.Parse("d6000000-0000-4000-8000-000000000002"), 20)],
        }, ct);

        Assert.Equal("Ada editada", editado.Nombre);
        Assert.Equal(["docente", "jefe_catedra"], editado.Roles.Select(r => r.Codigo));
        var asignacion = Assert.Single(editado.Asignaciones);
        Assert.Equal(CargoAdjunto, asignacion.CargoId);
        Assert.Equal(20, asignacion.Horas);
    }

    [Fact]
    public async Task Error_en_designaciones_revierte_toda_el_alta_de_identity()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        await PrepararCatalogoAsync(identity, ct);
        await using var modulo = PostgresFixture.CrearDesignaciones(Cadena);
        var servicio = CrearServicio(identity, modulo);
        var datos = DatosValidos(null) with
        {
            Upn = "rollback@unlam.edu.ar",
            Documento = "60999888",
            Legajo = "ROLLBACK",
            Designaciones = [new GuardarDesignacionVigenteDto(Materia, CargoTitular, Guid.Parse("d6000000-0000-4000-8000-000000000001"), 10)],
        };

        await modulo.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION designaciones.fallar_escritura_prueba() RETURNS trigger
            LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Falla de persistencia de prueba'; END $$;
            CREATE TRIGGER fallar_escritura_prueba BEFORE INSERT OR UPDATE ON designaciones.designaciones
            FOR EACH ROW EXECUTE FUNCTION designaciones.fallar_escritura_prueba();
            """, ct);
        await Assert.ThrowsAsync<DbUpdateException>(() => servicio.GuardarAsync(null, datos, ct));

        identity.ChangeTracker.Clear();
        modulo.ChangeTracker.Clear();
        Assert.False(await identity.Usuarios.AnyAsync(u => u.Upn == datos.Upn, ct));
        Assert.False(await identity.Personas.AnyAsync(p => p.Documento == datos.Documento, ct));
        Assert.Empty(await modulo.Designaciones.ToListAsync(ct));
    }

    [Fact]
    public async Task Error_en_designaciones_revierte_la_edicion_previa_de_identity()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        await PrepararCatalogoAsync(identity, ct);
        await using var modulo = PostgresFixture.CrearDesignaciones(Cadena);
        var servicio = CrearServicio(identity, modulo);
        var creado = await servicio.GuardarAsync(null, DatosValidos(null), ct);
        var invalido = DatosValidos(creado.PersonaId) with
        {
            Nombre = "No debe quedar",
            Version = creado.Version,
            Designaciones = [new GuardarDesignacionVigenteDto(Materia, CargoAdjunto, Guid.Parse("d6000000-0000-4000-8000-000000000002"), 30)],
        };

        await modulo.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION designaciones.fallar_escritura_prueba() RETURNS trigger
            LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Falla de persistencia de prueba'; END $$;
            CREATE TRIGGER fallar_escritura_prueba BEFORE INSERT OR UPDATE ON designaciones.designaciones
            FOR EACH ROW EXECUTE FUNCTION designaciones.fallar_escritura_prueba();
            """, ct);
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            servicio.GuardarAsync(creado.PersonaId, invalido, ct));

        await using var identidadVerificacion = PostgresFixture.CrearIdentity(Cadena);
        await using var moduloVerificacion = PostgresFixture.CrearDesignaciones(Cadena);
        var posterior = await CrearServicio(identidadVerificacion, moduloVerificacion)
            .ObtenerAsync(creado.PersonaId, ct);
        Assert.Equal("Ada", posterior.Nombre);
        var asignacion = Assert.Single(posterior.Asignaciones);
        Assert.Equal(CargoTitular, asignacion.CargoId);
        Assert.Equal(12, asignacion.Horas);
    }

    [Fact]
    public async Task Listado_y_filtros_incluyen_multiples_designaciones_y_estados_de_cuenta()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        var segundaMateria = Guid.Parse("70000000-0000-4000-8000-000000000104");
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        await using var modulo = PostgresFixture.CrearDesignaciones(Cadena);
        modulo.Designaciones.Add(new Designacion
        {
            Id = Guid.NewGuid(),
            PersonaId = PersonaActiva,
            MateriaId = segundaMateria,
            CargoId = CargoAdjunto,
            DedicacionId = Guid.Parse("d6000000-0000-4000-8000-000000000002"),
            Horas = 6,
            VigenteDesde = new DateOnly(2026, 8, 1),
            CreadoEn = DateTimeOffset.UtcNow,
        });
        await modulo.SaveChangesAsync(ct);
        modulo.ChangeTracker.Clear();
        var servicio = CrearServicio(identity, modulo);

        var todos = await servicio.ListarAsync(null, null, null, null, ct);

        Assert.Equal(2, Assert.Single(todos, d => d.PersonaId == PersonaActiva).Asignaciones.Count);
        Assert.False(Assert.Single(todos, d => d.PersonaId == PersonaInactiva).Activo);
        Assert.False(Assert.Single(todos, d => d.PersonaId == PersonaSinCuenta).TieneCuenta);
        Assert.All(await servicio.ListarAsync(null, segundaMateria, null, null, ct),
            d => Assert.Contains(d.Asignaciones, a => a.MateriaId == segundaMateria));
        Assert.All(await servicio.ListarAsync(null, null, "jefe_catedra", null, ct),
            d => Assert.Contains(d.Roles, r => r.Codigo == "jefe_catedra"));
        Assert.Contains(await servicio.ListarAsync("Sofía", null, null, false, ct),
            d => d.PersonaId == PersonaInactiva);
    }

    [Fact]
    public async Task Roles_distintos_por_materia_no_se_propagan_a_materias_ajenas()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        await PrepararCatalogoAsync(identity, ct);
        await using var modulo = PostgresFixture.CrearDesignaciones(Cadena);
        var servicio = CrearServicio(identity, modulo);

        var creado = await servicio.GuardarAsync(null, DatosValidos(null) with
        {
            Membresias =
            [
                new GuardarAsignacionRolDto(RolDocente, Materia, Carrera),
                new GuardarAsignacionRolDto(RolJefe, MateriaB, Carrera),
            ],
            Designaciones =
            [
                new GuardarDesignacionVigenteDto(Materia, CargoTitular, Guid.Parse("d6000000-0000-4000-8000-000000000001"), 12),
                new GuardarDesignacionVigenteDto(MateriaB, CargoAdjunto, Guid.Parse("d6000000-0000-4000-8000-000000000002"), 8),
            ],
        }, ct);

        Assert.Equal(2, creado.Membresias.Count);
        Assert.Contains(creado.Membresias, m => m.Codigo == "docente" && m.MateriaId == Materia);
        Assert.Contains(creado.Membresias, m => m.Codigo == "jefe_catedra" && m.MateriaId == MateriaB);
        Assert.DoesNotContain(creado.Membresias, m => m.Codigo == "jefe_catedra" && m.MateriaId == Materia);
        Assert.DoesNotContain(creado.Membresias, m => m.Codigo == "docente" && m.MateriaId == MateriaB);
    }

    [Fact]
    public async Task Dedicaciones_ofrece_seis_opciones_y_rechaza_referencias_invalidas_e_inactivas()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        await PrepararCatalogoAsync(identity, ct);
        await using var modulo = PostgresFixture.CrearDesignaciones(Cadena);
        var servicio = CrearServicio(identity, modulo);
        var catalogo = await modulo.Dedicaciones.OrderBy(d => d.Codigo).ToArrayAsync(ct);
        Assert.Equal([1, 2, 3, 4, 5, 6], catalogo.Select(d => (int)d.Codigo));
        var creado = await servicio.GuardarAsync(null, DatosValidos(null), ct);
        foreach (var id in new[] { Guid.NewGuid(), catalogo[5].Id })
        {
            catalogo[5].Activo = false;
            await modulo.SaveChangesAsync(ct);
            await Assert.ThrowsAsync<ErrorDominioPedido>(() => servicio.GuardarAsync(
                creado.PersonaId, DatosValidos(creado.PersonaId) with
                {
                    Version = creado.Version,
                    Designaciones = [new GuardarDesignacionVigenteDto(Materia, CargoTitular, id, 12)],
                }, ct));
        }
        Assert.Equal(catalogo[0].Id, Assert.Single((await servicio.ObtenerAsync(creado.PersonaId, ct)).Asignaciones).DedicacionId);
    }

    private static ServicioDocentes CrearServicio(
        IdentityDbContext identity,
        DesignacionesDbContext designaciones)
    {
        var repositorioDesignaciones = new RepositorioDesignaciones(designaciones);
        return new ServicioDocentes(
            new RepositorioDocentes(identity),
            new ServicioAdministracionDesignaciones(repositorioDesignaciones),
            new UnidadDeTrabajoAdministracion());
    }

    private static async Task PrepararCatalogoAsync(IdentityDbContext identity, CancellationToken ct)
    {
        identity.Carreras.Add(new Carrera
        {
            Id = Carrera,
            Codigo = "INFO",
            Nombre = "Ingeniería Informática",
            Activo = true,
            CreadoEn = DateTimeOffset.UtcNow,
        });
        identity.Materias.Add(new Materia
        {
            Id = Materia,
            Codigo = "03500",
            Nombre = "Matemática Discreta",
            CarreraId = Carrera,
            Activo = true,
            CreadoEn = DateTimeOffset.UtcNow,
        });
        identity.Materias.Add(new Materia
        {
            Id = MateriaB,
            Codigo = "03620",
            Nombre = "Algoritmos",
            CarreraId = Carrera,
            Activo = true,
            CreadoEn = DateTimeOffset.UtcNow,
        });
        await identity.SaveChangesAsync(ct);
        identity.ChangeTracker.Clear();
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
            if (File.Exists(Path.Combine(directorio.FullName, "AGENTS.md"))) return directorio.FullName;
            directorio = directorio.Parent;
        }
        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
    }

    private static GuardarDocenteDto DatosValidos(Guid? personaId) => new(
        personaId,
        "Ada",
        "Lovelace",
        "60000111",
        "DOC-1",
        null,
        new DateOnly(1985, 12, 10),
        null,
        "ada.docente@unlam.edu.ar",
        [new GuardarAsignacionRolDto(RolDocente, Materia, Carrera)],
        [new GuardarDesignacionVigenteDto(Materia, CargoTitular, Guid.Parse("d6000000-0000-4000-8000-000000000001"), 12)]);
}
