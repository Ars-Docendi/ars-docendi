using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Designaciones.Infrastructure.Migrations;

[DbContext(typeof(DesignacionesDbContext))]
[Migration("20260824040000_ComentariosAsistenteDesignaciones")]
public sealed class ComentariosAsistenteDesignaciones : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(ComentariosAsistenteDesignaciones).Assembly,
            "designaciones/010_designaciones_comentarios_asistente.sql"));

    // Ver la nota equivalente en ComentariosAsistenteIdentity: COMMENT ON no
    // tiene DROP, se revierte poniendo el comentario en nulo.
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            DO $comentarios_down$
            DECLARE
                objetivo text;
                columna  text;
            BEGIN
                FOREACH objetivo IN ARRAY ARRAY[
                    'designaciones.cargos', 'designaciones.periodos',
                    'designaciones.pedidos', 'designaciones.pedido_adjuntos',
                    'designaciones.pedido_historial', 'designaciones.designaciones']
                LOOP
                    EXECUTE format('COMMENT ON TABLE %s IS NULL', objetivo);

                    FOR columna IN
                        SELECT a.attname
                          FROM pg_attribute a
                         WHERE a.attrelid = objetivo::regclass
                           AND a.attnum > 0
                           AND NOT a.attisdropped
                    LOOP
                        EXECUTE format('COMMENT ON COLUMN %s.%I IS NULL', objetivo, columna);
                    END LOOP;
                END LOOP;
            END
            $comentarios_down$;
            """);
}
