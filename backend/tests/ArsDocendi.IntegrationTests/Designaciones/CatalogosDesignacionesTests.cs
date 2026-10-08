using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using Modules.Designaciones.Repositories;
using Modules.Designaciones.Services;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Modules.Designaciones.Domain;

namespace ArsDocendi.IntegrationTests.Designaciones;

public sealed class CatalogosDesignacionesTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "catalogos_designaciones")
{
    private static readonly Guid PersonaConPedidoVivo = Guid.Parse("d0000000-0000-4000-8000-000000000001");
    private static readonly Guid PersonaConPedidoRechazado = Guid.Parse("d0000000-0000-4000-8000-000000000015");
    private static readonly Guid MateriaVisible = Guid.Parse("70000000-0000-4000-8000-000000000101");
    private static readonly Guid MateriaAjena = Guid.Parse("70000000-0000-4000-8000-000000000201");
    private static readonly Guid Cargo = Guid.Parse("c3000000-0000-4000-8000-000000000003");
    private static readonly Guid Dedicacion = Guid.Parse("d6000000-0000-4000-8000-000000000002");

    [Fact]
    public async Task Catalogos_respetan_materias_y_carreras_del_actor()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
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

        Assert.Equal(3, jefe.Materias.Count);
        Assert.All(jefe.Materias, m => Assert.True(new[]
        {
            Guid.Parse("70000000-0000-4000-8000-000000000101"),
            Guid.Parse("70000000-0000-4000-8000-000000000102"),
            Guid.Parse("70000000-0000-4000-8000-000000000103"),
        }.Contains(m.Id)));
        Assert.Equal(4, coordinador.Materias.Count);
        Assert.All(coordinador.Materias, m => Assert.Equal(
            Guid.Parse("c0000000-0000-4000-8000-000000000201"), m.CarreraId));
        Assert.Equal(6, secretaria.Materias.Count);
    }

    [Fact]
    public async Task Catalogo_acotado_no_expone_designaciones_ni_personas_ajenas()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        await using var designacionesDb = PostgresFixture.CrearDesignaciones(Cadena);
        var personaMixta = Guid.NewGuid();
        var personaAjena = Guid.NewGuid();

        identityDb.Personas.AddRange(
            new Persona
            {
                Id = personaMixta,
                Documento = $"M-{personaMixta:N}",
                Nombre = "Persona",
                Apellido = "Mixta",
                CreadoEn = DateTimeOffset.UtcNow,
            },
            new Persona
            {
                Id = personaAjena,
                Documento = $"A-{personaAjena:N}",
                Nombre = "Persona",
                Apellido = "Ajena",
                CreadoEn = DateTimeOffset.UtcNow,
            });
        await identityDb.SaveChangesAsync(ct);
        designacionesDb.Designaciones.AddRange(
            CrearDesignacion(personaMixta, MateriaVisible),
            CrearDesignacion(personaMixta, MateriaAjena),
            CrearDesignacion(personaAjena, MateriaAjena));
        await designacionesDb.SaveChangesAsync(ct);

        var catalogos = await CrearServicio(
            Guid.Parse("a0000000-0000-4000-8000-000000000002"), identityDb, designacionesDb)
            .ObtenerAsync(ct);

        var mixta = Assert.Single(catalogos.Personas, p => p.Id == personaMixta);
        Assert.Contains(mixta.DesignacionesVigentes, d => d.MateriaId == MateriaVisible);
        Assert.DoesNotContain(mixta.DesignacionesVigentes, d => d.MateriaId == MateriaAjena);
        Assert.DoesNotContain(catalogos.Personas, p => p.Id == personaAjena);
    }

    [Fact]
    public async Task Catalogos_devuelven_periodo_personas_elegibles_y_cargos_activos()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
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
        Assert.Equal(6, catalogos.Dedicaciones.Count);
        Assert.Contains(catalogos.Dedicaciones, d => d.Nombre == "Categoría 6");
        Assert.Contains("Cambio de cargo o dedicación", catalogos.Novedades);
        Assert.DoesNotContain(Novedades.SinNovedad, catalogos.Novedades);
        Assert.Equal([1, 2, 3, 4, 5, 6], catalogos.Dedicaciones.Select(d => (int)d.Codigo));
        var dedicacion = await designacionesDb.Dedicaciones.SingleAsync(d => d.Codigo == 6, ct);
        dedicacion.Activo = false;
        await designacionesDb.SaveChangesAsync(ct);
        var actualizado = await CrearServicio(
            Guid.Parse("a0000000-0000-4000-8000-000000000004"), identityDb, designacionesDb).ObtenerAsync(ct);
        Assert.DoesNotContain(actualizado.Dedicaciones, d => d.Id == dedicacion.Id);
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

    private static Designacion CrearDesignacion(Guid personaId, Guid materiaId) => new()
    {
        Id = Guid.NewGuid(),
        PersonaId = personaId,
        MateriaId = materiaId,
        CargoId = Cargo,
        DedicacionId = Dedicacion,
        Horas = 10,
        VigenteDesde = new DateOnly(2026, 8, 1),
        CreadoEn = DateTimeOffset.UtcNow,
    };

    private sealed class UsuarioActualFalso(Guid id) : ICurrentUser
    {
        public string UserId => id.ToString();
        public string? Email => "test@unlam.edu.ar";
        public IReadOnlyList<string> Roles => [];
        public bool IsAuthenticated => true;
    }
}
