using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArsDocendi.Storage.Infrastructure.Migrations;

/// <summary>Instala el modelo final en una base nueva; no convierte historias alpha.</summary>
[DbContext(typeof(AlmacenamientoDbContext))]
[Migration("20261005000100_BaselineStorage")]
[RecursosMigracionSql(
    "storage/001_storage_archivos.sql",
    "storage/002_storage_audit_attach.sql")]
public sealed class BaselineStorage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(BaselineStorage));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("El baseline no se revierte destructivamente; restaurar un respaldo en un destino aislado con una versión compatible.");
}
