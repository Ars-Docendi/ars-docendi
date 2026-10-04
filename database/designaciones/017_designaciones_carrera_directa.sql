-- Pedidos y designaciones vuelven a referenciar materia_id (canónica) + carrera_id directos,
-- en vez de la pertenencia materia_plan_id. El plan sigue existiendo como catálogo informativo
-- en identity (qué materias dicta cada carrera y si su plan está vigente/activo), pero deja de
-- ser la FK de la que depende el enrutamiento. Ver change rediseno-modelo-academico.

-- pedidos -----------------------------------------------------------------------------------

ALTER TABLE designaciones.pedidos
    ADD COLUMN materia_id UUID,
    ADD COLUMN carrera_id UUID;

UPDATE designaciones.pedidos p
   SET materia_id = mp.materia_id,
       carrera_id = pl.carrera_id
  FROM identity.materias_plan mp
  JOIN identity.planes pl ON pl.id = mp.plan_id
 WHERE p.materia_plan_id = mp.id;

DROP INDEX designaciones.pedidos_materia_plan_idx;

ALTER TABLE designaciones.pedidos
    DROP CONSTRAINT pedidos_materia_plan_id_fkey,
    DROP COLUMN materia_plan_id,
    ALTER COLUMN materia_id SET NOT NULL,
    ALTER COLUMN carrera_id SET NOT NULL,
    ADD CONSTRAINT pedidos_materia_id_fkey
        FOREIGN KEY (materia_id) REFERENCES identity.materias(id) ON DELETE RESTRICT,
    ADD CONSTRAINT pedidos_carrera_id_fkey
        FOREIGN KEY (carrera_id) REFERENCES identity.carreras(id) ON DELETE RESTRICT;

-- Guard de ámbito del Jefe de Cátedra (por materia) y del Coordinador (por carrera).
CREATE INDEX pedidos_materia_idx ON designaciones.pedidos (materia_id);
CREATE INDEX pedidos_carrera_idx ON designaciones.pedidos (carrera_id);

-- designaciones -----------------------------------------------------------------------------

ALTER TABLE designaciones.designaciones
    ADD COLUMN carrera_id UUID;

UPDATE designaciones.designaciones d
   SET carrera_id = pl.carrera_id
  FROM identity.materias_plan mp
  JOIN identity.planes pl ON pl.id = mp.plan_id
 WHERE d.materia_plan_id = mp.id;

ALTER TABLE designaciones.designaciones
    DROP CONSTRAINT designaciones_materia_plan_fkey,
    DROP COLUMN materia_plan_id,
    ALTER COLUMN carrera_id SET NOT NULL,
    ADD CONSTRAINT designaciones_carrera_id_fkey
        FOREIGN KEY (carrera_id) REFERENCES identity.carreras(id) ON DELETE RESTRICT;

-- `designaciones_materia_plan_vigente_idx` (sobre materia_plan_id) ya se fue sola: Postgres
-- dropea en cascada los índices de una columna al borrarla con DROP COLUMN, arriba.
CREATE INDEX designaciones_carrera_idx ON designaciones.designaciones (carrera_id);

-- Última referencia a esta constraint (ambas FK compuestas de arriba ya se borraron):
-- identity.materias_plan queda libre para perderla.
ALTER TABLE identity.materias_plan
    DROP CONSTRAINT materias_plan_id_materia_unico;
