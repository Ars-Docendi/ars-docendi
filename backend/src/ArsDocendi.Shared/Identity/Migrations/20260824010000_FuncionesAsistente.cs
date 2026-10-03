using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArsDocendi.Shared.Identity.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260824010000_FuncionesAsistente")]
public sealed class FuncionesAsistente : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(FuncionesAsistente).Assembly,
            "identity/012_identity_funciones_asistente.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            DROP FUNCTION IF EXISTS identity.asistente_tiene_permiso(TEXT);
            DROP FUNCTION IF EXISTS identity.asistente_materias_visibles();
            DROP FUNCTION IF EXISTS identity.asistente_es_global();
            DROP FUNCTION IF EXISTS identity.asistente_actor();
            """);
}
