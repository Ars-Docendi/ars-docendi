using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Portal.Infrastructure.Migrations;

[DbContext(typeof(PortalDbContext))]
[Migration("20260905000000_FkPersonaDelPerfil")]
public sealed partial class FkPersonaDelPerfil : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(FkPersonaDelPerfil).Assembly, "portal/002_portal_fk_persona.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            "ALTER TABLE portal.perfiles DROP CONSTRAINT IF EXISTS perfiles_persona_fk;");
}
