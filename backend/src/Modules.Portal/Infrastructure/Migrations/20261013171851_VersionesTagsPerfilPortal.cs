using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Portal.Infrastructure.Migrations;

[DbContext(typeof(PortalDbContext))]
[Migration("20261013171851_VersionesTagsPerfilPortal")]
[RecursosMigracionSql("portal/20261013171851_VersionesTagsPerfilPortal.sql")]
public sealed class VersionesTagsPerfilPortal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(VersionesTagsPerfilPortal));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Recuperación explícita desde respaldo; no revertir DDL automáticamente.");
}
