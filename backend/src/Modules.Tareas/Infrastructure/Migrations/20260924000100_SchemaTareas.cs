using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Tareas.Infrastructure.Migrations;

[DbContext(typeof(TareasDbContext))]
[Migration("20260924000100_SchemaTareas")]
public sealed partial class SchemaTareas : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(typeof(SchemaTareas).Assembly, "tareas/001_tareas.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP SCHEMA IF EXISTS tareas CASCADE;");
}
