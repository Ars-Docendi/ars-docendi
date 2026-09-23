using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Portal.Infrastructure.Migrations;

[DbContext(typeof(PortalDbContext))]
[Migration("20260917000200_ReferenciasArchivosPortal")]
public sealed class ReferenciasArchivosPortal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(typeof(ReferenciasArchivosPortal).Assembly, "portal/002_portal_archivos.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("ALTER TABLE portal.cvs DROP COLUMN IF EXISTS archivo_id; ALTER TABLE portal.proyecto_documentos DROP COLUMN IF EXISTS archivo_id;");
}
