using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArsDocendi.Shared.Identity.Migrations;

/// <summary>Instala el modelo final en una base nueva; no convierte historias alpha.</summary>
[DbContext(typeof(IdentityDbContext))]
[Migration("20261005000000_BaselineIdentityYAudit")]
[RecursosMigracionSql(
    "identity/001_identity_users.sql",
    "audit/001_audit_schema.sql",
    "identity/002_identity_autorizacion.sql",
    "identity/003_identity_catalogo_academico.sql",
    "identity/004_identity_personas_ambitos.sql",
    "identity/005_identity_integridad.sql",
    "identity/006_identity_catalogos.sql",
    "identity/007_identity_audit_attach.sql")]
public sealed class BaselineIdentityYAudit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(BaselineIdentityYAudit));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("El baseline no se revierte destructivamente; restaurar un respaldo en un destino aislado con una versión compatible.");
}
