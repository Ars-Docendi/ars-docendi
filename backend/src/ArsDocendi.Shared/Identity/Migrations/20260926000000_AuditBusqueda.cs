using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArsDocendi.Shared.Identity.Migrations;

/// <summary>
/// <c>unaccent</c> como dependencia explícita de la auditoría (sistema-seccion-unificada,
/// design.md D4): sigue el patrón de <see cref="PermisosAdministracionSistema"/> —
/// SQL versionado embebido, ejecutado por <see cref="IdentityDbContext"/> para que
/// el Host no dependa del orden de migración del asistente (que ya crea la misma
/// extensión, idempotente, por su cuenta).
/// </summary>
[DbContext(typeof(IdentityDbContext))]
[Migration("20260926000000_AuditBusqueda")]
public sealed class AuditBusqueda : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(AuditBusqueda).Assembly,
            "audit/002_audit_busqueda.sql"));

    /// <summary>
    /// No-op a propósito: el rollback de sistema-seccion-unificada (design.md,
    /// Migration Plan §4) deja la extensión instalada porque es inofensiva —
    /// el asistente ya la requiere por su cuenta (database/asistente/001), y
    /// un <c>DROP EXTENSION</c> acá podría romperlo si su propia migración se
    /// aplicó primero.
    /// </summary>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
