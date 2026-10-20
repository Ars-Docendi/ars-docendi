using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArsDocendi.Storage.Infrastructure.Migrations;

[DbContext(typeof(AlmacenamientoDbContext))]
[Migration("20261013165221_ReservasInmutablesStorage")]
[RecursosMigracionSql("storage/20261013165221_ReservasInmutablesStorage.sql")]
public sealed class ReservasInmutablesStorage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(ReservasInmutablesStorage));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Recuperación explícita desde respaldo; no revertir DDL automáticamente.");
}
