using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Portal.Infrastructure.Migrations;

/// <summary>Instala el modelo final en una base nueva; no convierte historias alpha.</summary>
[DbContext(typeof(PortalDbContext))]
[Migration("20261005000300_BaselinePortal")]
[RecursosMigracionSql(
    "portal/001_portal_perfil.sql",
    "portal/002_portal_trayectoria.sql",
    "portal/003_portal_proyectos_habilidades.sql",
    "portal/004_portal_integridad.sql",
    "portal/005_portal_audit_attach.sql")]
public sealed class BaselinePortal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(BaselinePortal));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("El baseline no se revierte destructivamente; restaurar un respaldo en un destino aislado con una versión compatible.");
}
