using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArsDocendi.Shared.Identity.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260824000000_PermisoAsistenteConsultar")]
public sealed class PermisoAsistenteConsultar : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(PermisoAsistenteConsultar).Assembly,
            "identity/011_identity_permiso_asistente.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            DELETE FROM identity.rol_permisos
             WHERE permiso_id IN (
                   SELECT id FROM identity.permisos WHERE code = 'asistente.consultar');

            DELETE FROM identity.permisos WHERE code = 'asistente.consultar';
            """);
}
