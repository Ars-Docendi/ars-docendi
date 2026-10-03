using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Tareas.Infrastructure.Migrations;

[DbContext(typeof(TareasDbContext))]
[Migration("20260925000000_CatalogosTareas")]
public sealed partial class CatalogosTareas : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(typeof(CatalogosTareas).Assembly, "tareas/002_tareas_catalogos.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            ALTER TABLE tareas.proyectos
                DROP CONSTRAINT proyectos_estado_fk,
                ALTER COLUMN estado SET DEFAULT 'abierto',
                ADD CONSTRAINT proyectos_estado_valido CHECK (estado IN ('abierto', 'finalizado', 'cancelado'));
            ALTER TABLE tareas.tareas
                DROP CONSTRAINT tareas_prioridad_fk,
                DROP CONSTRAINT tareas_tipo_fk,
                DROP CONSTRAINT tareas_estado_fk,
                ALTER COLUMN estado SET DEFAULT 'pendiente',
                ADD CONSTRAINT tareas_prioridad_valida CHECK (prioridad IN ('alta', 'media', 'baja')),
                ADD CONSTRAINT tareas_tipo_valido CHECK (tipo IN
                    ('extension', 'administrativa', 'posgrado', 'investigacion', 'academica', 'decanato')),
                ADD CONSTRAINT tareas_estado_valido CHECK (estado IN
                    ('pendiente', 'en_curso', 'pausa', 'resuelta', 'cancelada'));
            DROP TABLE tareas.tipos_tarea, tareas.prioridades, tareas.estados_tarea, tareas.estados_proyecto;
            """);
}
