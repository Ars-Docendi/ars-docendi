using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArsDocendi.Shared.Identity.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260928020000_MateriasPlanUnicidad")]
public sealed class MateriasPlanUnicidad : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(MateriasPlanUnicidad).Assembly,
            "identity/014_identity_materias_plan_unicidad.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            ALTER TABLE identity.materias_plan DROP CONSTRAINT materias_plan_id_materia_unico;
            """);
}
