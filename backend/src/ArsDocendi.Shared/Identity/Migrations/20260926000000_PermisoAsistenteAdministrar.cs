using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArsDocendi.Shared.Identity.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260926000000_PermisoAsistenteAdministrar")]
public sealed class PermisoAsistenteAdministrar : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(PermisoAsistenteAdministrar).Assembly,
            "identity/020_identity_permiso_administrar.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            DELETE FROM identity.rol_permisos
             WHERE permiso_id IN (
                   SELECT id FROM identity.permisos WHERE code = 'asistente.administrar');

            DELETE FROM identity.permisos WHERE code = 'asistente.administrar';
            """);
}
