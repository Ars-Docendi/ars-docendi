using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArsDocendi.Shared.Identity.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260906020000_FuncionPersonaAsistente")]
public sealed class FuncionPersonaAsistente : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(FuncionPersonaAsistente).Assembly,
            "identity/016_identity_funcion_persona_asistente.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS identity.asistente_persona();");
}
