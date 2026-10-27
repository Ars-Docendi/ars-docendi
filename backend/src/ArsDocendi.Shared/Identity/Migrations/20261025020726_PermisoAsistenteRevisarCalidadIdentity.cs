using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArsDocendi.Shared.Identity.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20261025020726_PermisoAsistenteRevisarCalidadIdentity")]
[RecursosMigracionSql("identity/20261025020726_PermisoAsistenteRevisarCalidadIdentity.sql")]
public sealed class PermisoAsistenteRevisarCalidadIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(PermisoAsistenteRevisarCalidadIdentity));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Recuperación explícita desde respaldo; no revertir DDL automáticamente.");
}
