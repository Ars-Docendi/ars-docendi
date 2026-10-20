using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArsDocendi.Shared.Identity.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20261013165425_AuditoriaPrecisaIdentity")]
[RecursosMigracionSql("identity/20261013165425_AuditoriaPrecisaIdentity.sql")]
public sealed class AuditoriaPrecisaIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(AuditoriaPrecisaIdentity));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Recuperación explícita desde respaldo; no revertir DDL automáticamente.");
}
