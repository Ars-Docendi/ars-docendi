using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Designaciones.Infrastructure.Migrations;

[DbContext(typeof(DesignacionesDbContext))]
[Migration("20260824020000_RlsAsistente")]
public sealed class RlsAsistente : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(RlsAsistente).Assembly,
            "designaciones/009_designaciones_rls_asistente.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            DROP POLICY IF EXISTS asistente_ve_pedido_adjuntos ON designaciones.pedido_adjuntos;
            DROP POLICY IF EXISTS asistente_ve_pedido_historial ON designaciones.pedido_historial;
            DROP POLICY IF EXISTS asistente_ve_designaciones ON designaciones.designaciones;
            DROP POLICY IF EXISTS asistente_ve_pedidos ON designaciones.pedidos;

            ALTER TABLE designaciones.pedido_adjuntos  DISABLE ROW LEVEL SECURITY;
            ALTER TABLE designaciones.pedido_historial DISABLE ROW LEVEL SECURITY;
            ALTER TABLE designaciones.designaciones    DISABLE ROW LEVEL SECURITY;
            ALTER TABLE designaciones.pedidos          DISABLE ROW LEVEL SECURITY;
            """);
}
