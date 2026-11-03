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
/// The permission that lets someone review the anonymous list of problem answers.
/// </summary>
public sealed class PermisoRevisarCalidadTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_permiso_revisar_calidad")
{
    private const string Recurso = "identity/20261103001000_PermisoAsistenteRevisarCalidadIdentity.sql";

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
            ("code", Permisos.AsistenteRevisarCalidad));

        Assert.Equal(0, conAcceso);
    }

    private Task EjecutarMigracionAsync() => EjecutarAsync(
        RecursosSql.Leer(typeof(IdentityDbContext).Assembly, Recurso));
}
