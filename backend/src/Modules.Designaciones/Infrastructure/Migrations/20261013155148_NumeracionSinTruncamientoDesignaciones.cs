using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Designaciones.Infrastructure.Migrations;

[DbContext(typeof(DesignacionesDbContext))]
[Migration("20261013155148_NumeracionSinTruncamientoDesignaciones")]
[RecursosMigracionSql("designaciones/20261013155148_NumeracionSinTruncamientoDesignaciones.sql")]
public sealed class NumeracionSinTruncamientoDesignaciones : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(NumeracionSinTruncamientoDesignaciones));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Recuperación explícita desde respaldo; no revertir DDL automáticamente.");
}
