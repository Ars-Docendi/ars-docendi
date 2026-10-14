using System.Data.Common;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Storage.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Modules.Portal.Application;
using Modules.Portal.Contracts.Dtos;
using Modules.Portal.Domain;
using Modules.Portal.Infrastructure;
using Modules.Portal.Repositories;

namespace ArsDocendi.IntegrationTests.Portal;

public sealed class ConcurrenciaSeccionesTests(PostgresFixture postgres) : ClasePostgresAislada(postgres, "portal_cierre_carreras")
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dos_contactos_simultaneos_tienen_un_ganador_y_un_conflicto(bool existente)
    {
        var ct = TestContext.Current.CancellationToken;
        var persona = await SembrarAsync(ct);
        uint? version = null;
        if (existente)
        {
            await using var inicial = PostgresFixture.CrearPortal(Cadena);
            var contacto = new Contacto { Id = Guid.NewGuid(), PerfilId = persona, Telefono = "Inicial" };
            inicial.Contactos.Add(contacto); await inicial.SaveChangesAsync(ct); version = contacto.Version;
        }
        var barrera = new BarreraLecturasPortal("portal.contactos");
        await using var a = Contexto(barrera); await using var b = Contexto(barrera);
        var sa = Servicio(a, persona); var sb = Servicio(b, persona);
        var resultados = await Task.WhenAll(
            IntentarAsync(() => sa.GuardarContactoAsync(new("Primero", null, version), ct)),
            IntentarAsync(() => sb.GuardarContactoAsync(new("Segundo", null, version), ct)));
        Assert.Single(resultados.Where(x => x is null));
        Assert.Equal("concurrency-conflict", Assert.Single(resultados.OfType<ExcepcionAplicacion>()).Codigo);
        await using var leer = PostgresFixture.CrearPortal(Cadena);
        var guardado = Assert.Single(await leer.Contactos.ToListAsync(ct));
        Assert.Contains(guardado.Telefono, new[] { "Primero", "Segundo" });
        Assert.Single(await leer.Perfiles.ToListAsync(ct));
    }

    [Fact]
    public async Task Dos_ediciones_de_experiencia_simultaneas_no_cargan_colecciones_ni_pierden_otras_secciones()
    {
        var ct = TestContext.Current.CancellationToken;
        var persona = await SembrarAsync(ct);
        Guid id; uint version;
        await using (var inicial = PostgresFixture.CrearPortal(Cadena))
        {
            var experiencia = new Experiencia { Id = Guid.NewGuid(), PerfilId = persona, Puesto = "Inicial", Organizacion = "Org", Descripcion = "Desc", Desde = new(2020, 1, 1) };
            inicial.Experiencias.Add(experiencia);
            inicial.Educaciones.Add(new() { Id = Guid.NewGuid(), PerfilId = persona, Nivel = "Grado", Carrera = "Conservada", Institucion = "Inst", Desde = new(2020, 1, 1) });
            await inicial.SaveChangesAsync(ct); id = experiencia.Id; version = experiencia.Version;
        }
        var barrera = new BarreraLecturasPortal("portal.experiencias");
        await using var a = Contexto(barrera); await using var b = Contexto(barrera);
        var resultados = await Task.WhenAll(
            IntentarAsync(() => Servicio(a, persona).EditarAsync(id, new GuardarExperienciaDto("Primero", "Org", "Desc", new(2020, 1, 1), null) { Version = version }, ct)),
            IntentarAsync(() => Servicio(b, persona).EditarAsync(id, new GuardarExperienciaDto("Segundo", "Org", "Desc", new(2020, 1, 1), null) { Version = version }, ct)));
        Assert.Single(resultados.Where(x => x is null));
        Assert.Equal("concurrency-conflict", Assert.Single(resultados.OfType<ExcepcionAplicacion>()).Codigo);
        Assert.All(barrera.Sql, sql => { Assert.DoesNotContain("portal.educaciones", sql); Assert.DoesNotContain("portal.proyectos", sql); });
        await using var leer = PostgresFixture.CrearPortal(Cadena);
        Assert.Contains((await leer.Experiencias.SingleAsync(ct)).Puesto, new[] { "Primero", "Segundo" });
        Assert.Equal("Conservada", (await leer.Educaciones.SingleAsync(ct)).Carrera);
    }

    private async Task<Guid> SembrarAsync(CancellationToken ct)
    {
        var id = Guid.NewGuid();
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        identity.Personas.Add(new() { Id = id, Documento = id.ToString("N"), Nombre = "Sintetica", Apellido = "Portal" });
        await identity.SaveChangesAsync(ct);
        await using var portal = PostgresFixture.CrearPortal(Cadena);
        portal.Perfiles.Add(new() { Id = id, PersonaId = id }); await portal.SaveChangesAsync(ct);
        return id;
    }

    private PortalDbContext Contexto(DbCommandInterceptor barrera) => new(new DbContextOptionsBuilder<PortalDbContext>().UseNpgsql(Cadena).AddInterceptors(barrera).Options);
    private static ServicioPortal Servicio(PortalDbContext db, Guid persona)
    {
        var identity = SondaContrato.Crear<IConsultasIdentity>((n, args) => Task.FromResult<Usuario?>(new()
        { Id = persona, Upn = "portal@example.test", NombreParaMostrar = "Sintetica Portal", Persona = new() { Id = persona, Nombre = "Sintetica", Apellido = "Portal", Documento = "Sintetico" } }));
        var storage = SondaContrato.Crear<IAlmacenamientoArchivos>((n, args) => throw new InvalidOperationException("Una mutación no documental no llama Storage."));
        return new(new RepositorioPortal(db), identity, new ActorPortal(persona), storage);
    }
    private static async Task<Exception?> IntentarAsync<T>(Func<Task<T>> operacion)
    {
        try { await operacion(); return null; } catch (ExcepcionAplicacion e) { return e; }
    }
}

internal sealed class BarreraLecturasPortal(string tabla) : DbCommandInterceptor
{
    private int llegadas;
    private readonly TaskCompletionSource liberar = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public List<string> Sql { get; } = [];
    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default)
    {
        lock (Sql) Sql.Add(command.CommandText);
        if (command.CommandText.StartsWith("SELECT", StringComparison.Ordinal) && command.CommandText.Contains(tabla, StringComparison.Ordinal))
        {
            if (Interlocked.Increment(ref llegadas) == 2) liberar.TrySetResult();
            await liberar.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }
        return result;
    }
}
