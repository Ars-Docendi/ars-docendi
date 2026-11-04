using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Aulas.Infrastructure.Migrations;

[DbContext(typeof(AulasDbContext))]
[Migration("20261102000904_SolicitudesReservaAulas")]
[RecursosMigracionSql("aulas/20261102000904_SolicitudesReservaAulas.sql")]
public sealed class SolicitudesReservaAulas : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(SolicitudesReservaAulas));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Recuperación explícita desde respaldo; no revertir DDL automáticamente.");
}
