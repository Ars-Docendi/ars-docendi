using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ArsDocendi.Shared.Identity.Migrations;
[DbContext(typeof(IdentityDbContext))]
[Migration("20260929020000_CheckpointEstadoAuditoria")]
public sealed class CheckpointEstadoAuditoria : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(typeof(CheckpointEstadoAuditoria).Assembly, "audit/005_audit_state_checkpoint.sql"));
    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Conservar checkpoints; migración forward-only.");
}
