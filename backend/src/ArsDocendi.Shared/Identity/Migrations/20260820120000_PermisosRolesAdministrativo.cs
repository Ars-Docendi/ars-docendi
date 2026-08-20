using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArsDocendi.Shared.Identity.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260820120000_PermisosRolesAdministrativo")]
public sealed class PermisosRolesAdministrativo : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var assembly = typeof(PermisosRolesAdministrativo).Assembly;
        migrationBuilder.Sql(RecursosSql.Leer(
            assembly,
            "identity/010_identity_permisos_roles_administrativo.sql"));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM identity.rol_permisos
            WHERE rol_id = (
                SELECT id FROM identity.roles WHERE code = 'administrativo'
            )
            AND permiso_id IN (
                SELECT id FROM identity.permisos
                WHERE code IN (
                    'roles.ver',
                    'roles.administrar',
                    'roles.gestionar_membresia'
                )
            );
            """);
    }
}
