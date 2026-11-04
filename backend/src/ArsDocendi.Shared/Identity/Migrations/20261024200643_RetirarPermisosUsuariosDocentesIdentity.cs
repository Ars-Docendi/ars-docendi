using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArsDocendi.Shared.Identity.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20261024200643_RetirarPermisosUsuariosDocentesIdentity")]
[RecursosMigracionSql("identity/20261024200643_RetirarPermisosUsuariosDocentesIdentity.sql")]
public sealed class RetirarPermisosUsuariosDocentesIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(RetirarPermisosUsuariosDocentesIdentity));

    // Restaura los permisos retirados y los asigna desde sus equivalentes de personas.
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            INSERT INTO identity.permisos (id, code, nombre, descripcion) VALUES
                ('b2000000-0000-4000-8000-000000000009', 'usuarios.ver', 'Ver usuarios', 'Consultar el listado de usuarios registrados en el sistema.'),
                ('b2000000-0000-4000-8000-000000000010', 'usuarios.administrar', 'Administrar usuarios', 'Crear, editar, activar y desactivar cuentas de usuario del sistema.'),
                ('b2000000-0000-4000-8000-000000000022', 'docentes.ver', 'Ver docentes', 'Consultar docentes según el ámbito autorizado del actor.')
            ON CONFLICT (code) DO NOTHING;
            INSERT INTO identity.rol_permisos (rol_id, permiso_id)
            SELECT DISTINCT rp.rol_id, viejo.id
            FROM identity.rol_permisos rp
            JOIN identity.permisos nuevo ON nuevo.id = rp.permiso_id
            JOIN identity.permisos viejo ON viejo.code = ANY (
                CASE nuevo.code
                    WHEN 'personas.ver' THEN ARRAY['usuarios.ver']
                    WHEN 'personas.administrar' THEN ARRAY['usuarios.administrar', 'docentes.ver']
                    WHEN 'personas.ver_ambito' THEN ARRAY['docentes.ver']
                    ELSE ARRAY[]::TEXT[]
                END)
            ON CONFLICT (rol_id, permiso_id) DO NOTHING;
            """);
}
