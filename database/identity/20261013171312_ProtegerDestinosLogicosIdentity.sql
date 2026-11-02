-- Referencias intermodulares deliberadamente lógicas. No hay bypass runtime de DELETE.
CREATE FUNCTION identity.proteger_borrado_destino_logico() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION USING ERRCODE = '23503', CONSTRAINT = 'identity_destino_borrado_protegido',
        MESSAGE = 'Borrado físico de identidad protegido; usar desactivación. Mantenimiento exige procedimiento aprobado.';
END $$;
CREATE TRIGGER trg_users_borrado_logico BEFORE DELETE ON identity.users
FOR EACH ROW EXECUTE FUNCTION identity.proteger_borrado_destino_logico();
CREATE TRIGGER trg_personas_borrado_logico BEFORE DELETE ON identity.personas
FOR EACH ROW EXECUTE FUNCTION identity.proteger_borrado_destino_logico();

-- El consumidor invoca dentro de SU transacción antes de insertar/cambiar la referencia.
-- FOR SHARE bloquea también updates no clave (actividad/vínculo), no solo DELETE.
CREATE FUNCTION identity.validar_destinos_logicos(usuario_ids uuid[], persona_ids uuid[]) RETURNS void
LANGUAGE plpgsql AS $$
DECLARE
    fila record;
    encontrados integer := 0;
    esperados integer;
BEGIN
    SELECT count(DISTINCT id) INTO esperados FROM unnest(coalesce(persona_ids, ARRAY[]::uuid[])) id;
    FOR fila IN SELECT id FROM identity.personas WHERE id = ANY(persona_ids) ORDER BY id FOR SHARE LOOP
        encontrados := encontrados + 1;
    END LOOP;
    IF encontrados <> esperados OR array_position(persona_ids, NULL) IS NOT NULL THEN
        RAISE EXCEPTION USING ERRCODE = '23503', CONSTRAINT = 'identity_destino_no_elegible',
            MESSAGE = 'La persona de destino no existe.';
    END IF;
    encontrados := 0;
    SELECT count(DISTINCT id) INTO esperados FROM unnest(coalesce(usuario_ids, ARRAY[]::uuid[])) id;
    FOR fila IN SELECT id, is_active FROM identity.users WHERE id = ANY(usuario_ids) ORDER BY id FOR SHARE LOOP
        IF NOT fila.is_active THEN
            RAISE EXCEPTION USING ERRCODE = '23503', CONSTRAINT = 'identity_destino_no_elegible',
                MESSAGE = 'El usuario de destino no está activo.';
        END IF;
        encontrados := encontrados + 1;
    END LOOP;
    IF encontrados <> esperados OR array_position(usuario_ids, NULL) IS NOT NULL THEN
        RAISE EXCEPTION USING ERRCODE = '23503', CONSTRAINT = 'identity_destino_no_elegible',
            MESSAGE = 'El usuario de destino no existe.';
    END IF;
END $$;
