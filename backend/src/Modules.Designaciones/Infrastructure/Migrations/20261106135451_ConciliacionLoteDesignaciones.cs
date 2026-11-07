using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Designaciones.Infrastructure.Migrations;

[DbContext(typeof(DesignacionesDbContext))]
[Migration("20261106135451_ConciliacionLoteDesignaciones")]
[RecursosMigracionSql("designaciones/20261106135451_ConciliacionLoteDesignaciones.sql")]
public sealed class ConciliacionLoteDesignaciones : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(ConciliacionLoteDesignaciones));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Recuperación explícita desde respaldo; no revertir DDL automáticamente.");
}
