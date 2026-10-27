using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArsDocendi.Shared.Identity.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20261020000600_PermisoUsuariosVerDecanatoIdentity")]
[RecursosMigracionSql("identity/20261020000600_PermisoUsuariosVerDecanatoIdentity.sql")]
public sealed class PermisoUsuariosVerDecanatoIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(PermisoUsuariosVerDecanatoIdentity));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Recuperación explícita desde respaldo; no revertir DDL automáticamente.");
}
