using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ArsDocendi.Shared.Identity.Migrations;
[DbContext(typeof(IdentityDbContext))]
[Migration("20260929010000_HeartbeatAuditoria")]
public sealed class HeartbeatAuditoria : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(typeof(HeartbeatAuditoria).Assembly, "audit/004_audit_heartbeat.sql"));
    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Conservar evidencia; migración forward-only.");
}
