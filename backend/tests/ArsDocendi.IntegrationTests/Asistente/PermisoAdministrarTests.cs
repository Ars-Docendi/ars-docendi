using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Persistencia;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El permiso que gobierna presupuestos, tope organizacional y modo
/// mantenimiento del asistente.
/// </summary>
/// <remarks>
/// A diferencia de <c>asistente.ver_consulta</c> y
/// <c>asistente.leer_historial_ajeno</c> (sembrados vacíos, design.md D10/D13
/// de sus respectivos cambios), éste se siembra directamente a
/// <c>sys_admin</c> — es control operativo sobre disponibilidad y gasto del
/// módulo, no superficie de diagnóstico o soporte sobre datos de terceros
/// (design.md D13 de asistente-administracion-de-uso).
/// </remarks>
public sealed class PermisoAdministrarTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_permiso_administrar")
{
    private const string Recurso = "identity/020_identity_permiso_administrar.sql";

    [Fact]
    public async Task Existe_exactamente_una_fila_del_permiso()
    {
        var filas = await EscalarAsync<long>(
            "SELECT count(*) FROM identity.permisos WHERE code = @code",
            ("code", Permisos.AsistenteAdministrar));

        Assert.Equal(1, filas);
    }

    [Fact]
    public async Task Esta_concedido_a_sys_admin()
    {
        var concedidoASysAdmin = await EscalarAsync<long>(
            """
            SELECT count(*)
              FROM identity.rol_permisos rp
              JOIN identity.permisos p ON p.id = rp.permiso_id
              JOIN identity.roles r ON r.id = rp.rol_id
             WHERE p.code = @code
               AND r.code = 'sys_admin'
            """,
            ("code", Permisos.AsistenteAdministrar));

        Assert.Equal(1, concedidoASysAdmin);
    }

    [Fact]
    public async Task No_esta_concedido_a_ningun_otro_rol_por_default()
    {
        var concedidoATotal = await EscalarAsync<long>(
            """
            SELECT count(*)
              FROM identity.rol_permisos rp
              JOIN identity.permisos p ON p.id = rp.permiso_id
             WHERE p.code = @code
            """,
            ("code", Permisos.AsistenteAdministrar));

        // Exactamente un rol tiene el permiso — sys_admin, verificado arriba —
        // y no ninguno más.
        Assert.Equal(1, concedidoATotal);
    }

    [Fact]
    public async Task Reaplicar_la_migracion_no_duplica_filas_ni_agrega_concesiones()
    {
        var antesPermisos = await EscalarAsync<long>("SELECT count(*) FROM identity.permisos");
        var antesConcesiones = await EscalarAsync<long>(
            """
            SELECT count(*)
              FROM identity.rol_permisos rp
              JOIN identity.permisos p ON p.id = rp.permiso_id
             WHERE p.code = @code
            """,
            ("code", Permisos.AsistenteAdministrar));

        await EjecutarMigracionAsync();
        await EjecutarMigracionAsync();

        Assert.Equal(antesPermisos, await EscalarAsync<long>("SELECT count(*) FROM identity.permisos"));
        Assert.Equal(antesConcesiones, await EscalarAsync<long>(
            """
            SELECT count(*)
              FROM identity.rol_permisos rp
              JOIN identity.permisos p ON p.id = rp.permiso_id
             WHERE p.code = @code
            """,
            ("code", Permisos.AsistenteAdministrar)));
    }

    [Fact]
    public void La_constante_esta_declarada_y_registrada()
    {
        Assert.Equal("asistente.administrar", Permisos.AsistenteAdministrar);
        Assert.Contains(Permisos.AsistenteAdministrar, Permisos.Todos);
    }

    [Fact]
    public async Task La_politica_existe_en_el_Host_compuesto()
    {
        using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting($"ConnectionStrings:{CadenaDuena.Clave}", Cadena);
        });

        var proveedor = host.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var politica = await proveedor.GetPolicyAsync(Permisos.AsistenteAdministrar);

        Assert.NotNull(politica);
    }

    private Task EjecutarMigracionAsync() => EjecutarAsync(
        RecursosSql.Leer(typeof(IdentityDbContext).Assembly, Recurso));
}
