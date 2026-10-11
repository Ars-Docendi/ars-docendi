using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Identity.Administracion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArsDocendi.IntegrationTests.Identity;

/// <summary>Alta del administrador inicial que declara el despliegue (arranque --migrate).</summary>
public sealed class AdministradorInicialTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "admin_inicial")
{
    private static readonly DatosAdministradorInicial Datos = new()
    {
        Upn = " Admin@Dominio.edu.ar ",
        Nombre = "Ada",
        Apellido = "Lovelace",
        Documento = "30999888",
    };

    [Fact]
    public async Task Crea_persona_usuario_activo_y_rol_de_administracion()
    {
        var ct = TestContext.Current.CancellationToken;

        await AsegurarAsync(Datos, ct);

        await using var db = PostgresFixture.CrearIdentity(Cadena);
        var usuario = await db.Usuarios.AsNoTracking()
            .Include(u => u.Persona)
            .Include(u => u.Roles).ThenInclude(a => a.Rol)
            .SingleAsync(u => u.Upn == "admin@dominio.edu.ar", ct);
        Assert.True(usuario.Activo);
        Assert.Null(usuario.AzureOid);
        Assert.Equal("30999888", usuario.Persona!.Documento);
        Assert.Equal("sys_admin", Assert.Single(usuario.Roles).Rol!.Codigo);
    }

    [Fact]
    public async Task No_modifica_ni_reactiva_un_administrador_existente()
    {
        var ct = TestContext.Current.CancellationToken;
        await AsegurarAsync(Datos, ct);
        await using (var db = PostgresFixture.CrearIdentity(Cadena))
        {
            await db.Usuarios.Where(u => u.Upn == "admin@dominio.edu.ar")
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.Activo, false), ct);
        }

        await AsegurarAsync(Datos, ct);

        await using var verificacion = PostgresFixture.CrearIdentity(Cadena);
        var usuario = await verificacion.Usuarios.AsNoTracking().SingleAsync(u => u.Upn == "admin@dominio.edu.ar", ct);
        Assert.False(usuario.Activo);
        Assert.Equal(1, await verificacion.Personas.CountAsync(p => p.Documento == "30999888", ct));
    }

    [Fact]
    public async Task Sin_administrador_declarado_no_crea_nada()
    {
        var ct = TestContext.Current.CancellationToken;

        await AsegurarAsync(null, ct);
        await AsegurarAsync(new DatosAdministradorInicial(), ct);

        await using var db = PostgresFixture.CrearIdentity(Cadena);
        Assert.False(await db.Usuarios.AnyAsync(ct));
    }

    [Fact]
    public async Task Datos_incompletos_fallan_sin_crear_nada()
    {
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<InvalidOperationException>(() => AsegurarAsync(
            new DatosAdministradorInicial { Upn = "admin@dominio.edu.ar", Nombre = "Ada" }, ct));

        await using var db = PostgresFixture.CrearIdentity(Cadena);
        Assert.False(await db.Usuarios.AnyAsync(ct));
    }

    private async Task AsegurarAsync(DatosAdministradorInicial? datos, CancellationToken ct)
    {
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        await new ServicioAdministradorInicial(db, NullLogger<ServicioAdministradorInicial>.Instance)
            .AsegurarAsync(datos, ct);
    }
}
