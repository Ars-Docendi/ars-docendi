using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Designaciones.Infrastructure.Migrations;

[DbContext(typeof(DesignacionesDbContext))]
[Migration("20260928030000_MateriaCanonica")]
public sealed class MateriaCanonica : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(typeof(MateriaCanonica).Assembly,
            "designaciones/015_designaciones_materia_canonica.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "Restaurar el respaldo: la designación por materia canónica no se puede revertir sin perder la pertenencia de plan.");
}
