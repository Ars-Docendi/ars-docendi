using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArsDocendi.Shared.Identity.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260929000000_EndurecerPermisosSelladoAuditoria")]
public sealed class EndurecerPermisosSelladoAuditoria : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(typeof(EndurecerPermisosSelladoAuditoria).Assembly,
            "audit/003_audit_seal_privileges.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("No restaurar privilegios inseguros; aplicar corrección forward-only.");
}
