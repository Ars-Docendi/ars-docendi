using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Aulas.Infrastructure.Migrations;

[DbContext(typeof(AulasDbContext))]
[Migration("20261107125238_RlsAsistenteSolicitudesAulas")]
[RecursosMigracionSql("aulas/20261107125238_RlsAsistenteSolicitudesAulas.sql")]
public sealed class RlsAsistenteSolicitudesAulas : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(RlsAsistenteSolicitudesAulas));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Recuperación explícita desde respaldo; no revertir DDL automáticamente.");
}
