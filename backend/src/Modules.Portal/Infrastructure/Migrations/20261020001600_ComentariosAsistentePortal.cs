using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Portal.Infrastructure.Migrations;

[DbContext(typeof(PortalDbContext))]
[Migration("20261020001600_ComentariosAsistentePortal")]
[RecursosMigracionSql("portal/20261020001600_ComentariosAsistentePortal.sql")]
public sealed class ComentariosAsistentePortal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(ComentariosAsistentePortal));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Recuperación explícita desde respaldo; no revertir DDL automáticamente.");
}
