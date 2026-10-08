using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArsDocendi.Shared.Identity.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260925000000_PermisoAsistenteLeerHistorialAjeno")]
public sealed class PermisoAsistenteLeerHistorialAjeno : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(PermisoAsistenteLeerHistorialAjeno).Assembly,
            "identity/019_identity_permiso_leer_historial_ajeno.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            DELETE FROM identity.rol_permisos
             WHERE permiso_id IN (
                   SELECT id FROM identity.permisos WHERE code = 'asistente.leer_historial_ajeno');

            DELETE FROM identity.permisos WHERE code = 'asistente.leer_historial_ajeno';
            """);
}
