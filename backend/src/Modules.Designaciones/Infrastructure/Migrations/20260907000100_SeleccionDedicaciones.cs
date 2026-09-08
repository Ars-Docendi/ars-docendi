using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Designaciones.Infrastructure.Migrations;

[DbContext(typeof(DesignacionesDbContext))]
[Migration("20260907000100_SeleccionDedicaciones")]
public sealed class SeleccionDedicaciones : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(SeleccionDedicaciones).Assembly,
            "designaciones/010_designaciones_seleccion_dedicaciones.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Restaurar el respaldo y la versión conjunta anterior para revertir las selecciones.");
}
