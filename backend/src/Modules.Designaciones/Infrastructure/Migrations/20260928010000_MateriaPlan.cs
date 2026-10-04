using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Designaciones.Infrastructure.Migrations;

[DbContext(typeof(DesignacionesDbContext))]
[Migration("20260928010000_MateriaPlan")]
public sealed class MateriaPlan : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(typeof(MateriaPlan).Assembly,
            "designaciones/014_designaciones_materia_plan.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "Restaurar el respaldo: la pertenencia materia–plan no se puede revertir sin elegir una materia canónica por fila.");
}
