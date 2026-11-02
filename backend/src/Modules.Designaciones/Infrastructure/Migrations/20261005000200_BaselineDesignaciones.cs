using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Designaciones.Infrastructure.Migrations;

/// <summary>Instala el modelo final en una base nueva; no convierte historias alpha.</summary>
[DbContext(typeof(DesignacionesDbContext))]
[Migration("20261005000200_BaselineDesignaciones")]
[RecursosMigracionSql(
    "designaciones/001_designaciones_catalogos.sql",
    "designaciones/002_designaciones_pedidos.sql",
    "designaciones/003_designaciones_documentacion_historial.sql",
    "designaciones/004_designaciones_vigencias.sql",
    "designaciones/005_designaciones_idempotencia.sql",
    "designaciones/006_designaciones_integridad.sql",
    "designaciones/007_designaciones_catalogos.sql",
    "designaciones/008_designaciones_audit_attach.sql")]
public sealed class BaselineDesignaciones : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(BaselineDesignaciones));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("El baseline no se revierte destructivamente; restaurar un respaldo en un destino aislado con una versión compatible.");
}
