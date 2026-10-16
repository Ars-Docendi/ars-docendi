-- La clave foránea que faltaba entre el portal y el padrón.
--
-- `portal.perfiles.persona_id` es el ÚNICO camino del portal hacia una persona, y
-- nació sin `REFERENCES`. Eso deja dos cosas rotas a la vez, y las dos importan:
--
-- 1. La integridad. Un perfil puede apuntar a una persona que no existe, y nadie se
--    entera hasta que alguien hace el join y le faltan filas.
--
-- 2. El asistente. Su lector de catálogo emite las relaciones entre tablas leyendo
--    `pg_constraint` con `contype = 'f'`: sin la constraint, el prefijo del prompt no
--    le dice al modelo cómo cruzar portal con identity, que es el join de toda
--    pregunta útil —«¿quién tiene doctorado?» necesita llegar a un nombre—. La
--    alternativa sería escribir el camino a mano en un COMMENT, o sea poner una
--    barrera de prosa donde puede haber una del motor.
--
-- SIN `ON DELETE`, a propósito. Las FK internas de portal cascadean porque un perfil
-- sin dueño no significa nada; ésta no, porque borrar una persona que tiene perfil
-- cargado tiene que fallar y que alguien lo mire. `identity.personas` no se borra en
-- el flujo normal: se desactiva.
DO $portal_fk_persona$
DECLARE
    huerfanos BIGINT;
BEGIN
    IF EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'perfiles_persona_fk'
           AND conrelid = 'portal.perfiles'::REGCLASS
    ) THEN
        RETURN;
    END IF;

    -- Falla nombrando el problema. Una FK que no se puede crear por datos sucios da
    -- un error del motor que no dice cuántas filas ni de dónde salieron.
    SELECT count(*) INTO huerfanos
      FROM portal.perfiles p
      LEFT JOIN identity.personas x ON x.id = p.persona_id
     WHERE x.id IS NULL;

    IF huerfanos > 0 THEN
        RAISE EXCEPTION
            'Hay % perfiles de portal cuyo persona_id no existe en identity.personas. '
            'Resolvelos antes de crear la clave foránea: la migración no los borra '
            'porque decidir qué hacer con un perfil huérfano no es de la migración.',
            huerfanos;
    END IF;

    ALTER TABLE portal.perfiles
        ADD CONSTRAINT perfiles_persona_fk
        FOREIGN KEY (persona_id) REFERENCES identity.personas(id);
END
$portal_fk_persona$;
