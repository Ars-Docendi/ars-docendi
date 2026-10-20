using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArsDocendi.Shared.Identity.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20261020000700_PermisoAsistenteLeerHistorialAjenoIdentity")]
[RecursosMigracionSql("identity/20261020000700_PermisoAsistenteLeerHistorialAjenoIdentity.sql")]
public sealed class PermisoAsistenteLeerHistorialAjenoIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(PermisoAsistenteLeerHistorialAjenoIdentity));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Recuperación explícita desde respaldo; no revertir DDL automáticamente.");
}
