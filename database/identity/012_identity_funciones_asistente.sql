-- Resolución del actor del asistente conversacional.
--
-- Cuatro funciones que responden, sobre la base y en vivo: quién es el actor del
-- turno, si su alcance es global, qué materias ve y si tiene un permiso dado.
-- Las policies de RLS las usan como predicado.
--
-- POR QUÉ SECURITY DEFINER
-- Corren con los privilegios del dueño, no con los de quien llama. Así el
-- asistente puede preguntar «¿tengo este permiso?» sin que la respuesta dependa
-- de qué puede leer él mismo de identity, y sin que una policy futura sobre
-- identity cambie el resultado. Todas fijan `search_path = ''` y califican cada
-- nombre: sin eso, SECURITY DEFINER es un vector de escalada.
--
-- POR QUÉ NINGUNA LLEVA UN CÓDIGO DE ROL
-- La matriz rol -> permiso está comentada en su propia migración como PROVISIONAL
-- y PENDIENTE DE CONFIRMACIÓN CON EL CLIENTE, se edita desde /membresia-roles sin
-- migración, e identity.roles NO es un catálogo cerrado: Secretaría puede crear
-- roles propios. Una lista negra del tipo `code <> 'docente'` FALLA ABIERTA —
-- cualquier rol nuevo pasaría por default, tenga o no el permiso. Una lista
-- blanca falla cerrada, pero obliga a desplegar cada vez que el cliente crea un
-- rol. Por eso se pregunta por el permiso, que es el dato que el cliente
-- administra, y nunca por el rol.
--
-- STABLE y no VOLATILE: solo leen. Con VOLATILE, un predicado sin columnas deja
-- de ser pseudo-constante y el planner lo reevalúa FILA POR FILA en vez de
-- resolverlo una vez por consulta (One-Time Filter).

-- Actor del turno, leído del ajuste que fija la aplicación por transacción.
--
-- Devuelve NULL si el ajuste no está: sin actor no hay filas visibles, que es el
-- default correcto. En cambio ROMPE si el ajuste trae algo que no resuelve a un
-- usuario activo. El motivo es que la única fuente legítima de ese valor es
-- ICurrentUser.UserId; si llega el `oid` de Azure AD —que también es un UUID
-- válido— el actor no existe y el asistente devolvería CERO FILAS en silencio.
-- Para un sistema cuya métrica es corrección con abstención, «no encontré nada»
-- cuando en realidad no se sabe quién pregunta es una respuesta falsa.
CREATE OR REPLACE FUNCTION identity.asistente_actor()
RETURNS UUID
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path = ''
AS $asistente_actor$
DECLARE
    crudo TEXT := current_setting('app.asistente_user_id', TRUE);
    actor UUID;
BEGIN
    IF coalesce(crudo, '') = '' THEN
        RETURN NULL;
    END IF;

    BEGIN
        actor := crudo::UUID;
    EXCEPTION WHEN invalid_text_representation THEN
        RAISE EXCEPTION
            'app.asistente_user_id no es un UUID (%). Lo fija la aplicación con ICurrentUser.UserId.', crudo;
    END;

    IF NOT EXISTS (
        SELECT 1 FROM identity.users u WHERE u.id = actor AND u.is_active
    ) THEN
        RAISE EXCEPTION
            'app.asistente_user_id (%) no corresponde a un usuario activo. Verificá que sea identity.users.id y no el oid de Azure AD.', actor;
    END IF;

    RETURN actor;
END
$asistente_actor$;

-- ¿El actor tiene alguna asignación vigente de un rol de alcance global?
-- `scope` es una columna del catálogo de roles con CHECK propio, no un código de
-- rol: preguntar por ella no ata la función a ningún rol en particular.
CREATE OR REPLACE FUNCTION identity.asistente_es_global()
RETURNS BOOLEAN
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = ''
AS $asistente_es_global$
    SELECT EXISTS (
        SELECT 1
          FROM identity.user_roles ur
          JOIN identity.roles r ON r.id = ur.role_id
         WHERE ur.user_id = identity.asistente_actor()
           AND ur.deleted_at IS NULL
           AND r.is_active
           AND r.scope = 'global'
    );
$asistente_es_global$;

-- Materias que el actor puede ver.
--
-- Global ve todas. Con alcance de carrera ve las materias de esas carreras. Con
-- alcance de materia ve esas materias. Las asignaciones dadas de baja
-- (`deleted_at` no nulo) no cuentan: una revocación tiene que dejar de sumar
-- alcance en la consulta siguiente, sin reiniciar nada.
CREATE OR REPLACE FUNCTION identity.asistente_materias_visibles()
RETURNS SETOF UUID
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = ''
AS $asistente_materias_visibles$
    SELECT m.id
      FROM identity.materias m
     WHERE identity.asistente_es_global()

    UNION

    SELECT ur.materia_id
      FROM identity.user_roles ur
      JOIN identity.roles r ON r.id = ur.role_id
     WHERE ur.user_id = identity.asistente_actor()
       AND ur.deleted_at IS NULL
       AND r.is_active
       AND ur.materia_id IS NOT NULL

    UNION

    SELECT m.id
      FROM identity.user_roles ur
      JOIN identity.roles r ON r.id = ur.role_id
      JOIN identity.materias m ON m.carrera_id = ur.carrera_id
     WHERE ur.user_id = identity.asistente_actor()
       AND ur.deleted_at IS NULL
       AND r.is_active
       AND ur.materia_id IS NULL
       AND ur.carrera_id IS NOT NULL;
$asistente_materias_visibles$;

-- ¿El actor tiene un permiso, según la matriz vigente?
--
-- Recorre user_roles -> roles -> rol_permisos -> permisos en cada llamada. Que
-- se lea en vivo es lo que mantiene al asistente sincronizado: si Secretaría
-- cambia la matriz desde /membresia-roles, el asistente la sigue sin desplegar.
CREATE OR REPLACE FUNCTION identity.asistente_tiene_permiso(codigo TEXT)
RETURNS BOOLEAN
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = ''
AS $asistente_tiene_permiso$
    SELECT EXISTS (
        SELECT 1
          FROM identity.user_roles ur
          JOIN identity.roles r ON r.id = ur.role_id
          JOIN identity.rol_permisos rp ON rp.rol_id = r.id
          JOIN identity.permisos p ON p.id = rp.permiso_id
         WHERE ur.user_id = identity.asistente_actor()
           AND ur.deleted_at IS NULL
           AND r.is_active
           AND p.code = codigo
    );
$asistente_tiene_permiso$;

-- PUBLIC no ejecuta ninguna de las cuatro. Por default, una función nueva se crea
-- con EXECUTE para PUBLIC: sin este REVOKE, cualquier rol del cluster con acceso
-- a la base podría preguntarle a una función SECURITY DEFINER por los permisos de
-- cualquier actor. El GRANT a los dos roles del asistente va en la migración del
-- módulo, que es la que conoce sus nombres con sufijo de ambiente.
REVOKE EXECUTE ON FUNCTION identity.asistente_actor()              FROM PUBLIC;
REVOKE EXECUTE ON FUNCTION identity.asistente_es_global()          FROM PUBLIC;
REVOKE EXECUTE ON FUNCTION identity.asistente_materias_visibles()  FROM PUBLIC;
REVOKE EXECUTE ON FUNCTION identity.asistente_tiene_permiso(TEXT)  FROM PUBLIC;
