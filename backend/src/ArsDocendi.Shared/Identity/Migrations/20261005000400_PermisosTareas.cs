using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArsDocendi.Shared.Identity.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20261005000400_PermisosTareas")]
[RecursosMigracionSql("identity/012_identity_permisos_tareas.sql")]
public sealed class PermisosTareas : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(PermisosTareas));

    // Sólo revierte lo que agrega esta migración: `tareas.ver`/`tareas.gestionar`
    // ya existen en el baseline para Secretaría, Administrativo y sys_admin.
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            DELETE FROM identity.rol_permisos
            WHERE permiso_id IN (SELECT id FROM identity.permisos WHERE code = 'proyectos.gestionar')
               OR (permiso_id IN (SELECT id FROM identity.permisos WHERE code IN ('tareas.ver', 'tareas.gestionar'))
                   AND rol_id IN (SELECT id FROM identity.roles
                                  WHERE code IN ('docente', 'jefe_catedra', 'coordinador_carrera', 'decanato')));
            DELETE FROM identity.permisos WHERE code = 'proyectos.gestionar';
            """);
}
