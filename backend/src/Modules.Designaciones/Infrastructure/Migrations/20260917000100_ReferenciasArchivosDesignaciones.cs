using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Designaciones.Infrastructure.Migrations;

[DbContext(typeof(DesignacionesDbContext))]
[Migration("20260917000100_ReferenciasArchivosDesignaciones")]
public sealed class ReferenciasArchivosDesignaciones : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(typeof(ReferenciasArchivosDesignaciones).Assembly, "designaciones/012_designaciones_archivos.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("ALTER TABLE designaciones.pedido_adjuntos DROP COLUMN IF EXISTS archivo_id;");
}
