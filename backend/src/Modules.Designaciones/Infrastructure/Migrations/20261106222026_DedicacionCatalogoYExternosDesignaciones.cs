using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Designaciones.Infrastructure.Migrations;

[DbContext(typeof(DesignacionesDbContext))]
[Migration("20261106222026_DedicacionCatalogoYExternosDesignaciones")]
[RecursosMigracionSql("designaciones/20261106222026_DedicacionCatalogoYExternosDesignaciones.sql")]
public sealed class DedicacionCatalogoYExternosDesignaciones : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(DedicacionCatalogoYExternosDesignaciones));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Recuperación explícita desde respaldo; no revertir DDL automáticamente.");
}
