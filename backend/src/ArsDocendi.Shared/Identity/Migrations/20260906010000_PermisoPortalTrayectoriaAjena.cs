using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArsDocendi.Shared.Identity.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260906010000_PermisoPortalTrayectoriaAjena")]
public sealed class PermisoPortalTrayectoriaAjena : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(PermisoPortalTrayectoriaAjena).Assembly,
            "identity/015_identity_permiso_portal_trayectoria.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            DELETE FROM identity.rol_permisos
             WHERE permiso_id IN (
                   SELECT id FROM identity.permisos
                    WHERE code = 'portal.ver_trayectoria_ajena');

            DELETE FROM identity.permisos WHERE code = 'portal.ver_trayectoria_ajena';
            """);
}
