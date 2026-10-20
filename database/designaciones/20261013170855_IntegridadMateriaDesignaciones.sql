-- FK simple canónica; no exige pertenencia a plan/carrera informativos.
-- Bloquea escritores antes del preflight para que no pueda entrar un huérfano
-- entre la comprobación y la activación de la garantía.
LOCK TABLE designaciones.designaciones IN SHARE ROW EXCLUSIVE MODE;
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM designaciones.designaciones d
        LEFT JOIN identity.materias m ON m.id = d.materia_id
        WHERE m.id IS NULL
    ) THEN
        RAISE EXCEPTION 'Designaciones contiene materias huérfanas; requiere diagnóstico y saneamiento aprobado'
            USING ERRCODE = '23503';
    END IF;
END $$;
ALTER TABLE designaciones.designaciones
    ADD CONSTRAINT designaciones_materia_fk
    FOREIGN KEY (materia_id) REFERENCES identity.materias(id) ON DELETE RESTRICT;
