using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Tareas.Infrastructure.Migrations;

[DbContext(typeof(TareasDbContext))]
[Migration("20261013172733_IntegridadJerarquiaYSolucionTareas")]
[RecursosMigracionSql("tareas/20261013172733_IntegridadJerarquiaYSolucionTareas.sql")]
public sealed class IntegridadJerarquiaYSolucionTareas : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(IntegridadJerarquiaYSolucionTareas));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Recuperación explícita desde respaldo; no revertir DDL automáticamente.");
}
