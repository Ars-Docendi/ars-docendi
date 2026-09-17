using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArsDocendi.Shared.Identity.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260908000000_PermisosPantallas")]
public sealed class PermisosPantallas : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(PermisosPantallas).Assembly,
            "identity/011_identity_permisos_pantallas.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            DELETE FROM identity.rol_permisos
            WHERE permiso_id IN (
                SELECT id FROM identity.permisos
                WHERE code IN ('designaciones.revisar', 'docentes.ver')
            );
            DELETE FROM identity.permisos
            WHERE code IN ('designaciones.revisar', 'docentes.ver');
            """);
}
