using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Designaciones.Infrastructure.Migrations;

[DbContext(typeof(DesignacionesDbContext))]
[Migration("20260928060000_CarreraDirecta")]
public sealed class CarreraDirecta : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(typeof(CarreraDirecta).Assembly,
            "designaciones/017_designaciones_carrera_directa.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "Restaurar el respaldo: materia_id + carrera_id directos no se pueden revertir sin perder a qué plan puntual pertenecía cada fila.");
}
