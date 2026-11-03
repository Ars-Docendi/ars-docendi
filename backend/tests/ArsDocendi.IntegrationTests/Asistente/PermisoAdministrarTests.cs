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
/// El permiso que gobierna presupuestos, tope organizacional y modo mantenimiento del asistente.
/// </summary>
public sealed class PermisoAdministrarTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_permiso_administrar")
{
    private const string Recurso = "identity/20261020000900_PermisoAsistenteAdministrarIdentity.sql";

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

    private Task EjecutarMigracionAsync() => EjecutarAsync(
        RecursosSql.Leer(typeof(IdentityDbContext).Assembly, Recurso));
}
