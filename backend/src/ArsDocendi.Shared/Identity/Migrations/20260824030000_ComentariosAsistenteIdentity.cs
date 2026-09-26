using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArsDocendi.Shared.Identity.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260824030000_ComentariosAsistenteIdentity")]
public sealed class ComentariosAsistenteIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(ComentariosAsistenteIdentity).Assembly,
            "identity/013_identity_comentarios_asistente.sql"));

    // Los comentarios se quitan poniéndolos en nulo: COMMENT ON no tiene DROP.
    // Se listan tabla por tabla y no con un barrido del catálogo porque revertir
    // esta migración no debería borrar comentarios que haya escrito otro cambio.
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            DO $comentarios_down$
            DECLARE
                objetivo text;
                columna  text;
            BEGIN
                FOREACH objetivo IN ARRAY ARRAY[
                    'identity.carreras', 'identity.materias', 'identity.personas',
                    'identity.users', 'identity.roles', 'identity.user_roles',
                    'identity.permisos', 'identity.rol_permisos']
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
