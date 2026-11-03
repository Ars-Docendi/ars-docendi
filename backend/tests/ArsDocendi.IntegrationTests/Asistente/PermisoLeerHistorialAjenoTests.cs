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

/// <summary>El permiso de soporte que habilita leer el historial ajeno del asistente.</summary>
public sealed class PermisoLeerHistorialAjenoTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_permiso_historial_ajeno")
{
    private const string Recurso = "identity/20261103000600_PermisoAsistenteLeerHistorialAjenoIdentity.sql";

    [Fact]
    public async Task Ningun_rol_lo_tiene_ni_siquiera_sys_admin()
    {
        var conAcceso = await EscalarAsync<long>(
            """
            SELECT count(*)
              FROM identity.rol_permisos rp
              JOIN identity.permisos p ON p.id = rp.permiso_id
             WHERE p.code = @code
            """,
            ("code", Permisos.AsistenteLeerHistorialAjeno));

        Assert.Equal(0, conAcceso);
    }

    private Task EjecutarMigracionAsync() => EjecutarAsync(
        RecursosSql.Leer(typeof(IdentityDbContext).Assembly, Recurso));
}
