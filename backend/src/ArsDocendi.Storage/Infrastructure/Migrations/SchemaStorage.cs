using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArsDocendi.Storage.Infrastructure.Migrations;

[DbContext(typeof(AlmacenamientoDbContext))]
[Migration("20260917000000_SchemaStorage")]
public sealed class SchemaStorage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(typeof(SchemaStorage).Assembly, "storage/001_storage_archivos.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP SCHEMA IF EXISTS storage CASCADE;");
}
