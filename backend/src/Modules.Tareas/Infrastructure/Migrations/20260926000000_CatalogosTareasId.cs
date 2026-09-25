using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Tareas.Infrastructure.Migrations;

[DbContext(typeof(TareasDbContext))]
[Migration("20260926000000_CatalogosTareasId")]
public sealed partial class CatalogosTareasId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(typeof(CatalogosTareasId).Assembly, "tareas/003_tareas_catalogos_id.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            ALTER TABLE tareas.proyectos DROP CONSTRAINT proyectos_estado_fk;
            ALTER TABLE tareas.tareas
                DROP CONSTRAINT tareas_prioridad_fk,
                DROP CONSTRAINT tareas_tipo_fk,
                DROP CONSTRAINT tareas_estado_fk;
            ALTER TABLE tareas.tarea_historial DROP CONSTRAINT tarea_historial_estado_fk;

            ALTER TABLE tareas.proyectos ADD COLUMN estado TEXT;
            UPDATE tareas.proyectos p SET estado = c.codigo FROM tareas.estados_proyecto c WHERE c.id = p.estado_id;
            ALTER TABLE tareas.proyectos DROP COLUMN estado_id, ALTER COLUMN estado SET NOT NULL;

            ALTER TABLE tareas.tareas ADD COLUMN prioridad TEXT, ADD COLUMN tipo TEXT, ADD COLUMN estado TEXT;
            UPDATE tareas.tareas t SET prioridad = c.codigo FROM tareas.prioridades c WHERE c.id = t.prioridad_id;
            UPDATE tareas.tareas t SET tipo = c.codigo FROM tareas.tipos_tarea c WHERE c.id = t.tipo_id;
            UPDATE tareas.tareas t SET estado = c.codigo FROM tareas.estados_tarea c WHERE c.id = t.estado_id;
            ALTER TABLE tareas.tareas
                DROP COLUMN prioridad_id, DROP COLUMN tipo_id, DROP COLUMN estado_id,
                ALTER COLUMN prioridad SET NOT NULL, ALTER COLUMN tipo SET NOT NULL, ALTER COLUMN estado SET NOT NULL,
                ADD CONSTRAINT tareas_resuelta_con_solucion CHECK (
                    estado <> 'resuelta' OR btrim(COALESCE(solucion, '')) <> '');

            ALTER TABLE tareas.tarea_historial ADD COLUMN estado TEXT;
            UPDATE tareas.tarea_historial h SET estado = c.codigo FROM tareas.estados_tarea c WHERE c.id = h.estado_id;
            ALTER TABLE tareas.tarea_historial DROP COLUMN estado_id, ALTER COLUMN estado SET NOT NULL;

            ALTER TABLE tareas.estados_proyecto
                DROP CONSTRAINT estados_proyecto_pkey, DROP CONSTRAINT estados_proyecto_codigo_uq,
                ADD PRIMARY KEY (codigo), DROP COLUMN id;
            ALTER TABLE tareas.estados_tarea
                DROP CONSTRAINT estados_tarea_pkey, DROP CONSTRAINT estados_tarea_codigo_uq,
                ADD PRIMARY KEY (codigo), DROP COLUMN id;
            ALTER TABLE tareas.prioridades
                DROP CONSTRAINT prioridades_pkey, DROP CONSTRAINT prioridades_codigo_uq,
                ADD PRIMARY KEY (codigo), DROP COLUMN id;
            ALTER TABLE tareas.tipos_tarea
                DROP CONSTRAINT tipos_tarea_pkey, DROP CONSTRAINT tipos_tarea_codigo_uq,
                ADD PRIMARY KEY (codigo), DROP COLUMN id;

            ALTER TABLE tareas.proyectos
                ADD CONSTRAINT proyectos_estado_fk FOREIGN KEY (estado) REFERENCES tareas.estados_proyecto (codigo);
            ALTER TABLE tareas.tareas
                ADD CONSTRAINT tareas_prioridad_fk FOREIGN KEY (prioridad) REFERENCES tareas.prioridades (codigo),
                ADD CONSTRAINT tareas_tipo_fk FOREIGN KEY (tipo) REFERENCES tareas.tipos_tarea (codigo),
                ADD CONSTRAINT tareas_estado_fk FOREIGN KEY (estado) REFERENCES tareas.estados_tarea (codigo);
            """);
}
