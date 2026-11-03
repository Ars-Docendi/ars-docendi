using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Notificaciones.Infrastructure.Migrations;

[DbContext(typeof(NotificacionesDbContext))]
[Migration("20261102032905_CrearSchemaNotificaciones")]
[RecursosMigracionSql("notificaciones/20261102032905_CrearSchemaNotificaciones.sql")]
public sealed class CrearSchemaNotificaciones : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(CrearSchemaNotificaciones));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Recuperación explícita desde respaldo; no revertir DDL automáticamente.");
}
