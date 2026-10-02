-- Permisos explícitos para las pantallas que antes tenían excepciones por rol.
CREATE OR REPLACE FUNCTION identity.proteger_roles_de_sistema()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        IF OLD.es_sistema THEN
            RAISE EXCEPTION 'el rol de sistema % no se puede eliminar', OLD.code;
        END IF;
        RETURN OLD;
    END IF;

    IF OLD.es_sistema THEN
        IF NEW.code IS DISTINCT FROM OLD.code THEN
            RAISE EXCEPTION 'el code del rol de sistema % es inmutable', OLD.code;
        END IF;
        IF NEW.scope IS DISTINCT FROM OLD.scope THEN
            RAISE EXCEPTION 'el scope del rol de sistema % es inmutable', OLD.code;
        END IF;
        IF NEW.name IS DISTINCT FROM OLD.name THEN
            RAISE EXCEPTION 'el nombre del rol de sistema % es inmutable', OLD.code;
        END IF;
        IF NEW.description IS DISTINCT FROM OLD.description THEN
            RAISE EXCEPTION 'la descripción del rol de sistema % es inmutable', OLD.code;
        END IF;
        IF NEW.is_active IS DISTINCT FROM OLD.is_active THEN
            RAISE EXCEPTION 'el estado del rol de sistema % es inmutable', OLD.code;
        END IF;
        IF NOT NEW.es_sistema THEN
            RAISE EXCEPTION 'no se puede quitar la marca es_sistema del rol %', OLD.code;
        END IF;
    ELSIF NEW.es_sistema THEN
        RAISE EXCEPTION 'no se puede promover el rol % a rol de sistema', OLD.code;
    END IF;
    RETURN NEW;
END;
$$;

INSERT INTO identity.permisos (id, code, nombre, descripcion) VALUES
    ('b2000000-0000-4000-8000-000000000021', 'designaciones.revisar',
     'Revisar designaciones',
     'Consultar y revisar pedidos según la etapa y el ámbito del actor.'),
    ('b2000000-0000-4000-8000-000000000022', 'docentes.ver',
     'Ver docentes',
     'Consultar docentes según el ámbito autorizado del actor.')
ON CONFLICT (code) DO UPDATE SET
    nombre = EXCLUDED.nombre,
    descripcion = EXCLUDED.descripcion;

INSERT INTO identity.rol_permisos (rol_id, permiso_id)
SELECT r.id, p.id
FROM identity.roles r
JOIN identity.permisos p ON p.code = ANY (
    CASE r.code
        WHEN 'coordinador_carrera' THEN ARRAY['designaciones.revisar']
        WHEN 'secretaria' THEN ARRAY['designaciones.revisar', 'docentes.ver']
        WHEN 'decanato' THEN ARRAY['designaciones.revisar']
        WHEN 'administrativo' THEN ARRAY['designaciones.revisar', 'docentes.ver']
        WHEN 'jefe_catedra' THEN ARRAY['docentes.ver']
        WHEN 'sys_admin' THEN ARRAY['designaciones.revisar', 'docentes.ver']
        ELSE ARRAY[]::TEXT[]
    END)
ON CONFLICT (rol_id, permiso_id) DO NOTHING;
