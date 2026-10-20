using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Designaciones.Infrastructure.Migrations;

[DbContext(typeof(DesignacionesDbContext))]
[Migration("20261013173721_CompletitudCircuitoDesignaciones")]
[RecursosMigracionSql("designaciones/20261013173721_CompletitudCircuitoDesignaciones.sql")]
public sealed class CompletitudCircuitoDesignaciones : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(CompletitudCircuitoDesignaciones));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Recuperación explícita desde respaldo; no revertir DDL automáticamente.");
}
