using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Aulas.Infrastructure.Migrations;

[DbContext(typeof(AulasDbContext))]
[Migration("20261107125238_ComentariosAsistenteSolicitudesAulas")]
[RecursosMigracionSql("aulas/20261107125238_ComentariosAsistenteSolicitudesAulas.sql")]
public sealed class ComentariosAsistenteSolicitudesAulas : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(ComentariosAsistenteSolicitudesAulas));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Recuperación explícita desde respaldo; no revertir DDL automáticamente.");
}
