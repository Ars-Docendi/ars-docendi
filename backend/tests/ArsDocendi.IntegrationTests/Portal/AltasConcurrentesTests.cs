using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Portal.Domain;
using Modules.Portal.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Portal;

public sealed class AltasConcurrentesTests(PostgresFixture postgres) : ClasePostgresAislada(postgres, "portal_altas")
{
    [Fact]
    public async Task Perfil_incidental_rechaza_destino_persona_inexistente_sin_escribir()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = PostgresFixture.CrearPortal(Cadena);
        var persona = Guid.NewGuid();
        var error = await Assert.ThrowsAsync<ArsDocendi.Shared.Aplicacion.ExcepcionAplicacion>(() =>
            new RepositorioPortal(db).ObtenerOCrearAsync(persona, ct));
        Assert.Equal("identity-destination-conflict", error.Codigo);
        Assert.False(await db.Perfiles.AnyAsync(p => p.PersonaId == persona, ct));
    }

    [Fact]
    public async Task Perfil_referenciado_bloquea_borrado_fisico_de_persona()
    {
        var ct = TestContext.Current.CancellationToken;
        var persona = await CrearPersonaAsync(ct);
        await using var portal = PostgresFixture.CrearPortal(Cadena);
        var perfil = await new RepositorioPortal(portal).ObtenerOCrearAsync(persona, ct);

        await using var conexion = await AbrirConexionAsync();
        await using var eliminar = new NpgsqlCommand(
            "DELETE FROM identity.personas WHERE id=@id", conexion);
        eliminar.Parameters.AddWithValue("id", persona);
        var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(
            async () => await eliminar.ExecuteNonQueryAsync(ct));

        Assert.Equal("identity_destino_borrado_protegido", error.ConstraintName);
        Assert.True(await portal.Perfiles.AnyAsync(p => p.Id == perfil.Id && p.PersonaId == persona, ct));
        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        Assert.True(await identity.Personas.AnyAsync(p => p.Id == persona, ct));
    }

    [Fact]
    public async Task Dos_perfiles_sugieren_un_termino_normalizado_y_conservan_tipos_independientes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var a = PostgresFixture.CrearPortal(Cadena);
        await using var b = PostgresFixture.CrearPortal(Cadena);
        var ra = new RepositorioPortal(a); var rb = new RepositorioPortal(b);
        var personaA = await CrearPersonaAsync(ct);
        var personaB = await CrearPersonaAsync(ct);
        var perfiles = await Task.WhenAll(ra.ObtenerOCrearAsync(personaA, ct), rb.ObtenerOCrearAsync(personaB, ct));
        var pa = perfiles[0]; var pb = perfiles[1];
        await Task.WhenAll(ra.GuardarAsync(ct), rb.GuardarAsync(ct));
        await Task.WhenAll(ra.ReemplazarTagsAsync(pa, "habilidad", ["Término concurrente"], ct),
            rb.ReemplazarTagsAsync(pb, "interes", [" término CONCURRENTE "], ct));
        await using var lectura = PostgresFixture.CrearPortal(Cadena);
        Assert.Single(await lectura.Habilidades.Where(h => h.TerminoNorm == "TÉRMINO CONCURRENTE").ToListAsync(ct));
        Assert.True(await lectura.DocenteHabilidades.AnyAsync(h => h.PerfilId == pa.Id && h.Tipo == "habilidad", ct));
        Assert.True(await lectura.DocenteHabilidades.AnyAsync(h => h.PerfilId == pb.Id && h.Tipo == "interes", ct));
    }

    [Fact]
    public async Task Dos_primeras_secciones_comparten_perfil_sin_unique()
    {
        var ct = TestContext.Current.CancellationToken;
        var persona = await CrearPersonaAsync(ct);
        var barrera = new BarreraPrimerasLecturas();
        await using var a = CrearContextoConBarrera(barrera);
        await using var b = CrearContextoConBarrera(barrera);
        var ra = new RepositorioPortal(a); var rb = new RepositorioPortal(b);
        var perfiles = await Task.WhenAll(ra.ObtenerOCrearAsync(persona, ct), rb.ObtenerOCrearAsync(persona, ct));
        var pa = perfiles[0]; var pb = perfiles[1];
        ra.Agregar(new Contacto { Id = Guid.NewGuid(), PerfilId = pa.Id, Telefono = "Primera" });
        rb.Agregar(new Experiencia { Id = Guid.NewGuid(), PerfilId = pb.Id, Puesto = "Puesto", Organizacion = "Org", Descripcion = "Desc", Desde = new(2020,1,1) });
        await Task.WhenAll(ra.GuardarAsync(ct), rb.GuardarAsync(ct));
        await using var lectura = PostgresFixture.CrearPortal(Cadena);
        Assert.Single(await lectura.Perfiles.Where(p => p.PersonaId == persona).ToListAsync(ct));
        Assert.Equal(pa.Id, pb.Id);
        Assert.True(await lectura.Contactos.AnyAsync(c => c.PerfilId == pa.Id, ct));
        Assert.True(await lectura.Experiencias.AnyAsync(c => c.PerfilId == pa.Id, ct));
    }

    private async Task<Guid> CrearPersonaAsync(CancellationToken ct)
    {
        var id = Guid.NewGuid();
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        db.Personas.Add(new() { Id = id, Documento = "PORTAL-" + id.ToString("N"), Nombre = "Persona", Apellido = "Sintetica" });
        await db.SaveChangesAsync(ct);
        return id;
    }

    private Modules.Portal.Infrastructure.PortalDbContext CrearContextoConBarrera(BarreraPrimerasLecturas barrera) =>
        new(new DbContextOptionsBuilder<Modules.Portal.Infrastructure.PortalDbContext>()
            .UseNpgsql(Cadena).AddInterceptors(barrera).Options);

    private sealed class BarreraPrimerasLecturas : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
    {
        private int llegadas;
        private readonly TaskCompletionSource liberar = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<System.Data.Common.DbDataReader> ReaderExecutedAsync(
            System.Data.Common.DbCommand command,
            Microsoft.EntityFrameworkCore.Diagnostics.CommandExecutedEventData eventData,
            System.Data.Common.DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("EXISTS", StringComparison.Ordinal)
                && command.CommandText.Contains("portal.perfiles", StringComparison.Ordinal))
            {
                if (Interlocked.Increment(ref llegadas) == 2) liberar.TrySetResult();
                await liberar.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }
}
