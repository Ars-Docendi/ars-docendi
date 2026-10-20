using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Designaciones.Infrastructure.Migrations;

/// <summary>
/// <c>identity.asistente_alcanza_a(persona)</c>: el predicado de visibilidad de
/// portal para el asistente.
/// </summary>
/// <remarks>
/// <b>Crea una función del schema <c>identity</c> desde la cadena de Designaciones,
/// y eso pide explicación.</b> Su cuerpo referencia <c>designaciones.designaciones</c>,
/// y una función <c>LANGUAGE sql</c> se parsea al crearse: en la cadena de Identity
/// —que corre primero— ese schema todavía no existe. Cada cadena sólo aplica
/// recursos de su propia carpeta (<c>scripts/migrations/comun.mjs</c>), así que el
/// SQL vive en <c>database/designaciones/</c> aunque el objeto sea de
/// <c>identity</c>. Portal, que es quien la invoca, corre después.
///
/// <b>No se pasó a plpgsql para esquivarlo.</b> Diferir el parseo haría que un typo
/// en el nombre de una función hermana no falle al migrar sino en la primera
/// consulta, y este predicado es el que decide quién ve el perfil de quién.
/// </remarks>
[DbContext(typeof(DesignacionesDbContext))]
[Migration("20261020001300_FuncionAlcanceDePersonaAsistenteDesignaciones")]
[RecursosMigracionSql("designaciones/20261020001300_FuncionAlcanceDePersonaAsistenteDesignaciones.sql")]
public sealed class FuncionAlcanceDePersonaAsistenteDesignaciones : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(FuncionAlcanceDePersonaAsistenteDesignaciones));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Recuperación explícita desde respaldo; no revertir DDL automáticamente.");
}
