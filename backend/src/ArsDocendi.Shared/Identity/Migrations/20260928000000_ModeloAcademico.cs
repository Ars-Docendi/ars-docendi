using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArsDocendi.Shared.Identity.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260928000000_ModeloAcademico")]
public sealed class ModeloAcademico : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(ModeloAcademico).Assembly,
            "identity/013_identity_modelo_academico.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            DROP INDEX identity.user_roles_materia_plan_idx;
            DROP INDEX identity.user_roles_unique_assignment;

            ALTER TABLE identity.user_roles DROP COLUMN materia_plan_id;
            ALTER TABLE identity.user_roles
                ADD CONSTRAINT user_roles_materia_requires_carrera
                CHECK (materia_id IS NULL OR carrera_id IS NOT NULL);

            CREATE UNIQUE INDEX user_roles_unique_assignment
                ON identity.user_roles (user_id, role_id, materia_id, carrera_id)
                NULLS NOT DISTINCT
                WHERE deleted_at IS NULL;

            ALTER TABLE identity.materias ADD COLUMN carrera_id UUID REFERENCES identity.carreras(id) ON DELETE RESTRICT;

            UPDATE identity.materias m
               SET carrera_id = p.carrera_id
              FROM identity.materias_plan mp
              JOIN identity.planes p ON p.id = mp.plan_id
             WHERE mp.materia_id = m.id;

            ALTER TABLE identity.materias
                ALTER COLUMN carrera_id SET NOT NULL,
                DROP CONSTRAINT materias_code_formato,
                DROP CONSTRAINT materias_code_unico,
                ADD CONSTRAINT materias_code_unique_per_carrera UNIQUE (carrera_id, code);

            DROP TABLE identity.materias_plan;
            DROP TABLE identity.planes;

            CREATE OR REPLACE FUNCTION identity.enforce_role_scope()
            RETURNS TRIGGER
            LANGUAGE plpgsql
            AS $$
            DECLARE
                role_scope TEXT;
            BEGIN
                SELECT scope INTO role_scope FROM identity.roles WHERE id = NEW.role_id;

                IF role_scope IS NULL THEN
                    RAISE EXCEPTION 'unknown role_id %', NEW.role_id;
                END IF;

                IF role_scope = 'global' THEN
                    IF NEW.materia_id IS NOT NULL OR NEW.carrera_id IS NOT NULL THEN
                        RAISE EXCEPTION 'role_id % is global; materia_id and carrera_id must both be NULL', NEW.role_id;
                    END IF;
                ELSIF role_scope = 'materia' THEN
                    IF NEW.materia_id IS NULL OR NEW.carrera_id IS NULL THEN
                        RAISE EXCEPTION 'role_id % is materia-scoped; materia_id and carrera_id are both required', NEW.role_id;
                    END IF;
                ELSIF role_scope = 'carrera' THEN
                    IF NEW.carrera_id IS NULL OR NEW.materia_id IS NOT NULL THEN
                        RAISE EXCEPTION 'role_id % is carrera-scoped; carrera_id required, materia_id must be NULL', NEW.role_id;
                    END IF;
                END IF;

                RETURN NEW;
            END;
            $$;
            """);
}
