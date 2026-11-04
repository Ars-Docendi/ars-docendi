using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArsDocendi.Shared.Identity.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20261024193835_PermisosPersonasIdentity")]
[RecursosMigracionSql("identity/20261024193835_PermisosPersonasIdentity.sql")]
public sealed class PermisosPersonasIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(PermisosPersonasIdentity));

    // Sólo revierte lo que agrega esta migración: los permisos nuevos y sus asignaciones.
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            DELETE FROM identity.rol_permisos
            WHERE permiso_id IN (SELECT id FROM identity.permisos
                                 WHERE code IN ('personas.ver', 'personas.ver_ambito', 'personas.administrar', 'portal.administrar'));
            DELETE FROM identity.permisos
            WHERE code IN ('personas.ver', 'personas.ver_ambito', 'personas.administrar', 'portal.administrar');
            """);
}
