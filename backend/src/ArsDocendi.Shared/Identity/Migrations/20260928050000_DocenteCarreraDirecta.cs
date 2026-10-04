using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArsDocendi.Shared.Identity.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260928050000_DocenteCarreraDirecta")]
public sealed class DocenteCarreraDirecta : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(DocenteCarreraDirecta).Assembly,
            "identity/016_identity_docente_carrera_directa.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "Restaurar el respaldo: el docente por materia_id + carrera_id no se puede revertir sin perder a qué plan puntual pertenecía.");
}
