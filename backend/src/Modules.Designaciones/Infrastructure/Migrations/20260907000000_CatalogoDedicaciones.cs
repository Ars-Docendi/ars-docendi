using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Designaciones.Infrastructure.Migrations;

[DbContext(typeof(DesignacionesDbContext))]
[Migration("20260907000000_CatalogoDedicaciones")]
public sealed class CatalogoDedicaciones : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(CatalogoDedicaciones).Assembly,
            "designaciones/009_designaciones_dedicaciones.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Restaurar el respaldo y la versión conjunta anterior para revertir el catálogo.");
}
