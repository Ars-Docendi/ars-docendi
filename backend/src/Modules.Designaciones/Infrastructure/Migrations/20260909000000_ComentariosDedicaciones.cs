using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Designaciones.Infrastructure.Migrations;

/// <summary>
/// Comentarios del catálogo de dedicaciones, para el prefijo del asistente.
/// </summary>
/// <remarks>
/// Va después de <c>CatalogoDedicaciones</c> y de <c>HorasComplementarias</c>
/// porque comenta columnas que ésas crean. Los comentarios del asistente sobre
/// designaciones son de agosto: escribir estos allá los haría correr antes que las
/// columnas que nombran.
/// </remarks>
[DbContext(typeof(DesignacionesDbContext))]
[Migration("20260909000000_ComentariosDedicaciones")]
public sealed class ComentariosDedicaciones : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(ComentariosDedicaciones).Assembly,
            "designaciones/012_designaciones_comentarios_dedicaciones.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Los comentarios de esquema no se revierten: son documentación del catálogo.");
}
