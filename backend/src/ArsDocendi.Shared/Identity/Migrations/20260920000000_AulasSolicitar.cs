using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArsDocendi.Shared.Identity.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260920000000_AulasSolicitar")]
public sealed class AulasSolicitar : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(AulasSolicitar).Assembly,
            "identity/012_identity_aulas_solicitar.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            DELETE FROM identity.rol_permisos
            WHERE permiso_id IN (SELECT id FROM identity.permisos WHERE code = 'aulas.solicitar');
            DELETE FROM identity.rol_permisos
            WHERE rol_id IN (SELECT id FROM identity.roles WHERE code = 'docente')
              AND permiso_id IN (SELECT id FROM identity.permisos WHERE code = 'aulas.ver');
            DELETE FROM identity.permisos
            WHERE code = 'aulas.solicitar';
            """);
}
