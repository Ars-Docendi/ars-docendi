using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Aulas.Infrastructure.Migrations;

[DbContext(typeof(AulasDbContext))]
[Migration("20261102030524_MotivoCancelacionYRolSolicitanteAulas")]
[RecursosMigracionSql("aulas/20261102030524_MotivoCancelacionYRolSolicitanteAulas.sql")]
public sealed class MotivoCancelacionYRolSolicitanteAulas : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(MotivoCancelacionYRolSolicitanteAulas));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Recuperación explícita desde respaldo; no revertir DDL automáticamente.");
}
