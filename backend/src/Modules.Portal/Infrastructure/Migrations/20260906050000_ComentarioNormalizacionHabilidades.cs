using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Portal.Infrastructure.Migrations;

/// <summary>
/// Reaplica los comentarios de portal con la convención de normalización explícita.
/// </summary>
/// <remarks>
/// LO ENCONTRÓ LA CORRIDA FINANCIADA, y es el tipo de defecto que sólo aparece
/// midiendo. El modelo tradujo «¿qué docentes saben Kubernetes?» a
/// <c>termino_norm = lower('Kubernetes')</c> y no devolvió nada: la normalización
/// real es a MAYÚSCULAS (<c>RepositorioPortal.Normalizar</c>), y el comentario decía
/// que había que comparar por esa columna sin decir en qué caja.
///
/// No es un error del modelo: adivinó, porque el esquema no se lo decía. Un
/// comentario que dice «compará por acá» sin decir cómo está normalizado hace fallar
/// TODA pregunta por habilidad, en silencio y con cero filas.
/// </remarks>
[DbContext(typeof(PortalDbContext))]
[Migration("20260906050000_ComentarioNormalizacionHabilidades")]
public sealed partial class ComentarioNormalizacionHabilidades : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(ComentarioNormalizacionHabilidades).Assembly,
            "portal/004_portal_comentarios_asistente.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
