using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArsDocendi.Shared.Identity.Migrations;

/// <summary>
/// <c>unaccent</c> como dependencia explícita de la auditoría (sistema-seccion-unificada,
/// design.md D4): SQL versionado embebido, ejecutado por <see cref="IdentityDbContext"/> para que
/// el Host no dependa del orden de migración del asistente (que ya crea la misma
/// extensión, idempotente, por su cuenta).
/// </summary>
[DbContext(typeof(IdentityDbContext))]
[Migration("20261020000800_AuditBusquedaIdentity")]
[RecursosMigracionSql("audit/20261020000800_AuditBusquedaIdentity.sql")]
public sealed class AuditBusquedaIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(AuditBusquedaIdentity));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Recuperación explícita desde respaldo; no revertir DDL automáticamente.");
}
