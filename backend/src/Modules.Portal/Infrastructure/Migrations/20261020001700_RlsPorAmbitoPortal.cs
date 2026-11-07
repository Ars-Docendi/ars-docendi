using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Portal.Infrastructure.Migrations;

/// <summary>
/// El predicado de visibilidad de portal pasa a conjugar ámbito.
/// </summary>
/// <remarks>
/// Las policies invocan <c>identity.asistente_alcanza_a</c>, que crea
/// <see cref="Modules.Designaciones.Infrastructure.Migrations.FuncionAlcanceDePersonaAsistenteDesignaciones"/>
/// en la cadena de Designaciones: esa cadena corre antes que Portal, así que acá
/// la función ya existe.
/// </remarks>
[DbContext(typeof(PortalDbContext))]
[Migration("20261020001700_RlsPorAmbitoPortal")]
[RecursosMigracionSql("portal/20261020001700_RlsPorAmbitoPortal.sql")]
public sealed class RlsPorAmbitoPortal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(RlsPorAmbitoPortal));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Recuperación explícita desde respaldo; no revertir DDL automáticamente.");
}
