-- Los catálogos de Tareas pasan a clave numérica.
--
-- Las tablas de negocio dejan de guardar el código del estado/prioridad/tipo como texto
-- ('abierto', 'pendiente', ...) y lo referencian por el `id` del catálogo (SMALLINT). El
-- `codigo` sigue en el catálogo como identificador estable para la API y el código, y el
-- nombre visible sigue siendo dato editable.
--
-- La regla "una tarea Resuelta exige Solución" (tareas_resuelta_con_solucion) se apoyaba en
-- el texto 'resuelta': un CHECK no puede consultar el catálogo, así que se retira de la base
-- y queda garantizada por MaquinaEstadosTarea, que ya la exigía.

-- 1. Ids de los catálogos.
ALTER TABLE tareas.estados_proyecto ADD COLUMN id SMALLINT GENERATED ALWAYS AS IDENTITY;
ALTER TABLE tareas.estados_tarea    ADD COLUMN id SMALLINT GENERATED ALWAYS AS IDENTITY;
ALTER TABLE tareas.prioridades      ADD COLUMN id SMALLINT GENERATED ALWAYS AS IDENTITY;
ALTER TABLE tareas.tipos_tarea      ADD COLUMN id SMALLINT GENERATED ALWAYS AS IDENTITY;

-- 2. Columnas nuevas en las tablas de negocio, completadas desde el código actual.
ALTER TABLE tareas.proyectos ADD COLUMN estado_id SMALLINT;
UPDATE tareas.proyectos p SET estado_id = c.id
    FROM tareas.estados_proyecto c WHERE c.codigo = p.estado;

ALTER TABLE tareas.tareas
    ADD COLUMN prioridad_id SMALLINT,
    ADD COLUMN tipo_id SMALLINT,
    ADD COLUMN estado_id SMALLINT;
UPDATE tareas.tareas t SET prioridad_id = c.id FROM tareas.prioridades c WHERE c.codigo = t.prioridad;
UPDATE tareas.tareas t SET tipo_id = c.id FROM tareas.tipos_tarea c WHERE c.codigo = t.tipo;
UPDATE tareas.tareas t SET estado_id = c.id FROM tareas.estados_tarea c WHERE c.codigo = t.estado;

ALTER TABLE tareas.tarea_historial ADD COLUMN estado_id SMALLINT;
UPDATE tareas.tarea_historial h SET estado_id = c.id
    FROM tareas.estados_tarea c WHERE c.codigo = h.estado;

-- 3. Se retiran las columnas de texto (y con ellas su FK y el CHECK de la solución).
ALTER TABLE tareas.proyectos
    DROP CONSTRAINT proyectos_estado_fk,
    DROP COLUMN estado,
    ALTER COLUMN estado_id SET NOT NULL;

ALTER TABLE tareas.tareas
    DROP CONSTRAINT tareas_prioridad_fk,
    DROP CONSTRAINT tareas_tipo_fk,
    DROP CONSTRAINT tareas_estado_fk,
    DROP CONSTRAINT tareas_resuelta_con_solucion,
    DROP COLUMN prioridad,
    DROP COLUMN tipo,
    DROP COLUMN estado,
    ALTER COLUMN prioridad_id SET NOT NULL,
    ALTER COLUMN tipo_id SET NOT NULL,
    ALTER COLUMN estado_id SET NOT NULL;

ALTER TABLE tareas.tarea_historial
    DROP COLUMN estado,
    ALTER COLUMN estado_id SET NOT NULL;

-- 4. La clave primaria de cada catálogo pasa a ser el id; el código queda único.
ALTER TABLE tareas.estados_proyecto
    DROP CONSTRAINT estados_proyecto_pkey,
    ADD PRIMARY KEY (id),
    ADD CONSTRAINT estados_proyecto_codigo_uq UNIQUE (codigo);
ALTER TABLE tareas.estados_tarea
    DROP CONSTRAINT estados_tarea_pkey,
    ADD PRIMARY KEY (id),
    ADD CONSTRAINT estados_tarea_codigo_uq UNIQUE (codigo);
ALTER TABLE tareas.prioridades
    DROP CONSTRAINT prioridades_pkey,
    ADD PRIMARY KEY (id),
    ADD CONSTRAINT prioridades_codigo_uq UNIQUE (codigo);
ALTER TABLE tareas.tipos_tarea
    DROP CONSTRAINT tipos_tarea_pkey,
    ADD PRIMARY KEY (id),
    ADD CONSTRAINT tipos_tarea_codigo_uq UNIQUE (codigo);

-- 5. Claves foráneas contra el id.
ALTER TABLE tareas.proyectos
    ADD CONSTRAINT proyectos_estado_fk FOREIGN KEY (estado_id) REFERENCES tareas.estados_proyecto (id);

ALTER TABLE tareas.tareas
    ADD CONSTRAINT tareas_prioridad_fk FOREIGN KEY (prioridad_id) REFERENCES tareas.prioridades (id),
    ADD CONSTRAINT tareas_tipo_fk FOREIGN KEY (tipo_id) REFERENCES tareas.tipos_tarea (id),
    ADD CONSTRAINT tareas_estado_fk FOREIGN KEY (estado_id) REFERENCES tareas.estados_tarea (id);

ALTER TABLE tareas.tarea_historial
    ADD CONSTRAINT tarea_historial_estado_fk FOREIGN KEY (estado_id) REFERENCES tareas.estados_tarea (id);
