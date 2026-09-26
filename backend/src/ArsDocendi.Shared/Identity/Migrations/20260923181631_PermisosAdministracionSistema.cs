using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArsDocendi.Shared.Identity.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260923181631_PermisosAdministracionSistema")]
public sealed class PermisosAdministracionSistema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(PermisosAdministracionSistema).Assembly,
            "identity/012_identity_permisos_administracion_sistema.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            DELETE FROM identity.rol_permisos rp
            USING identity.roles r, identity.permisos p
            WHERE rp.rol_id = r.id
              AND rp.permiso_id = p.id
              AND r.code = 'sys_admin'
              AND p.code IN ('sistema.estado.ver', 'auditoria.ver');

            DELETE FROM identity.permisos p
            WHERE p.code IN ('sistema.estado.ver', 'auditoria.ver')
              AND NOT EXISTS (
                  SELECT 1 FROM identity.rol_permisos rp WHERE rp.permiso_id = p.id
              );
            """);
}
