using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArsDocendi.Shared.Identity.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260928000000_SelladoVerificableAuditoria")]
public sealed class SelladoVerificableAuditoria : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(SelladoVerificableAuditoria).Assembly,
            "audit/002_audit_seal.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "El sellado puede tener manifiestos externos; la migración no es reversible. Use una corrección forward-only y conserve la evidencia.");
}
