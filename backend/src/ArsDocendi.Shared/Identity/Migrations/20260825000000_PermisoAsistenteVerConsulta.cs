using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArsDocendi.Shared.Identity.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260825000000_PermisoAsistenteVerConsulta")]
public sealed class PermisoAsistenteVerConsulta : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(PermisoAsistenteVerConsulta).Assembly,
            "identity/014_identity_permiso_ver_consulta.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            DELETE FROM identity.rol_permisos
             WHERE permiso_id IN (
                   SELECT id FROM identity.permisos WHERE code = 'asistente.ver_consulta');

            DELETE FROM identity.permisos WHERE code = 'asistente.ver_consulta';
            """);
}
