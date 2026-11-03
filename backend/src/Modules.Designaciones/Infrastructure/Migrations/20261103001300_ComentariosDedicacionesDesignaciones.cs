using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Designaciones.Infrastructure.Migrations;

/// <summary>
/// Comentarios del catálogo de dedicaciones, para el prefijo del asistente.
/// </summary>
[DbContext(typeof(DesignacionesDbContext))]
[Migration("20261103001300_ComentariosDedicacionesDesignaciones")]
[RecursosMigracionSql("designaciones/20261103001300_ComentariosDedicacionesDesignaciones.sql")]
public sealed class ComentariosDedicacionesDesignaciones : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(ComentariosDedicacionesDesignaciones));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Recuperación explícita desde respaldo; no revertir DDL automáticamente.");
}
