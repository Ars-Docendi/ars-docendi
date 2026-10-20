using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Designaciones.Infrastructure.Migrations;

[DbContext(typeof(DesignacionesDbContext))]
[Migration("20261020001100_ComentariosAsistenteDesignaciones")]
[RecursosMigracionSql("designaciones/20261020001100_ComentariosAsistenteDesignaciones.sql")]
public sealed class ComentariosAsistenteDesignaciones : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(ComentariosAsistenteDesignaciones));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Recuperación explícita desde respaldo; no revertir DDL automáticamente.");
}
