using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArsDocendi.Shared.Identity.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20261030151457_PermisosSeguimientoPedidosIdentity")]
[RecursosMigracionSql("identity/20261030151457_PermisosSeguimientoPedidosIdentity.sql")]
public sealed class PermisosSeguimientoPedidosIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(PermisosSeguimientoPedidosIdentity));

    // Quita el permiso de seguimiento y restaura los retirados con las asignaciones de los
    // roles de sistema del catálogo base (las ediciones hechas desde Roles no se recuperan).
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            DELETE FROM identity.rol_permisos
            WHERE permiso_id IN (SELECT id FROM identity.permisos WHERE code = 'designaciones.ver_seguimiento');
            DELETE FROM identity.permisos WHERE code = 'designaciones.ver_seguimiento';
            INSERT INTO identity.permisos (id, code, nombre, descripcion) VALUES
                ('b2000000-0000-4000-8000-000000000015', 'sistema.parametrizar', 'Parametrizar sistema', 'Configurar parámetros generales (umbrales, textos, fechas de corte).'),
                ('b2000000-0000-4000-8000-000000000019', 'portal.editar', 'Editar portal personal', 'Actualizar datos personales, horas disponibles y áreas de experticia.'),
                ('b2000000-0000-4000-8000-000000000020', 'reportes.ver', 'Ver reportes globales', 'Acceder a reportes consolidados de designaciones, aulas y actividad docente.')
            ON CONFLICT (code) DO NOTHING;
            INSERT INTO identity.rol_permisos (rol_id, permiso_id)
            SELECT r.id, p.id
            FROM (VALUES
                ('docente', 'portal.editar'), ('jefe_catedra', 'portal.editar'),
                ('coordinador_carrera', 'portal.editar'), ('coordinador_carrera', 'reportes.ver'),
                ('secretaria', 'portal.editar'), ('secretaria', 'reportes.ver'), ('secretaria', 'sistema.parametrizar'),
                ('decanato', 'portal.editar'), ('decanato', 'reportes.ver'),
                ('administrativo', 'portal.editar'),
                ('sys_admin', 'portal.editar'), ('sys_admin', 'reportes.ver'), ('sys_admin', 'sistema.parametrizar')
            ) AS a(rol, permiso)
            JOIN identity.roles r ON r.code = a.rol
            JOIN identity.permisos p ON p.code = a.permiso
            ON CONFLICT (rol_id, permiso_id) DO NOTHING;
            """);
}
