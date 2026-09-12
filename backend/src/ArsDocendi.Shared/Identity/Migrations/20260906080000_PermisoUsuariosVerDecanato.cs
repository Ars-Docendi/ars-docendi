using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArsDocendi.Shared.Identity.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260906080000_PermisoUsuariosVerDecanato")]
public sealed class PermisoUsuariosVerDecanato : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(PermisoUsuariosVerDecanato).Assembly,
            "identity/018_identity_permiso_usuarios_ver_decanato.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            DELETE FROM identity.rol_permisos rp
            USING identity.roles r, identity.permisos p
            WHERE rp.rol_id = r.id AND rp.permiso_id = p.id
              AND r.code = 'decanato' AND p.code = 'usuarios.ver';
            """);
}
