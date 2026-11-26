using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Notificaciones.Infrastructure.Migrations;

[DbContext(typeof(NotificacionesDbContext))]
[Migration("20261103190503_CrearSuscripcionesYPreferenciasPushNotificaciones")]
[RecursosMigracionSql("notificaciones/20261103190503_CrearSuscripcionesYPreferenciasPushNotificaciones.sql")]
public sealed class CrearSuscripcionesYPreferenciasPushNotificaciones : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(CrearSuscripcionesYPreferenciasPushNotificaciones));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Recuperación explícita desde respaldo; no revertir DDL automáticamente.");
}
