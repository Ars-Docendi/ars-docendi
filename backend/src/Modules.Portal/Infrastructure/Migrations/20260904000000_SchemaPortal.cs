using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Modules.Portal.Infrastructure.Migrations;

[DbContext(typeof(PortalDbContext))]
[Migration("20260904000000_SchemaPortal")]
public sealed partial class SchemaPortal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(typeof(SchemaPortal).Assembly, "portal/001_portal_perfil.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP SCHEMA IF EXISTS portal CASCADE;");
}
