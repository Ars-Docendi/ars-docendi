using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Aulas.Infrastructure.Migrations;

[DbContext(typeof(AulasDbContext))]
[Migration("20261102181055_CatalogoAulasAulas")]
[RecursosMigracionSql("aulas/20261102181055_CatalogoAulasAulas.sql")]
public sealed class CatalogoAulasAulas : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(CatalogoAulasAulas));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Recuperación explícita desde respaldo; no revertir DDL automáticamente.");
}
