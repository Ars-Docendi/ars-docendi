-- La quinta función de resolución del actor: qué PERSONA es.
--
-- Las cuatro de 012 responden quién es el actor, si es global, qué materias ve y
-- si tiene un permiso. Ninguna responde a qué persona del padrón corresponde, y
-- eso es lo que hace falta para el predicado «mi propio perfil»: el portal está
-- archivado por persona, no por usuario.
--
-- POR QUÉ SON DOS IDENTIFICADORES Y NO UNO
-- `identity.users` es la cuenta —tiene UPN, azure_oid, último login— y
-- `identity.personas` es el legajo. Un usuario puede no tener persona asociada
-- (`persona_id` es nullable) y una persona puede no tener cuenta: el padrón
-- sintético tiene ocho de diecisiete sin usuario, para cubrir el alta docente de
-- alguien que todavía no se logueó.
--
-- DEVUELVE NULL CUANDO NO HAY PERSONA, y no rompe. Es distinto de
-- `asistente_actor()`, que sí rompe ante un identificador que no resuelve: allá un
-- valor presente y sin usuario significa que la aplicación mandó el dato
-- equivocado; acá un usuario sin persona es un estado LEGÍTIMO del sistema. Un NULL
-- hace falsa la comparación de la policy y el actor no ve ningún perfil propio, que
-- es exactamente lo correcto: no tiene ninguno.
--
-- Misma disciplina que las otras cuatro: STABLE porque solo lee, SECURITY DEFINER
-- para que la respuesta no dependa de qué puede leer el asistente de identity, y
-- `search_path = ''` con cada nombre calificado — sin eso, SECURITY DEFINER es un
-- vector de escalada.

CREATE OR REPLACE FUNCTION identity.asistente_persona()
RETURNS UUID
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = ''
AS $asistente_persona$
    SELECT u.persona_id
      FROM identity.users u
     WHERE u.id = identity.asistente_actor();
$asistente_persona$;

-- Se revoca de PUBLIC igual que las otras cuatro. El GRANT EXECUTE a los dos roles
-- de lectura del asistente vive en `database/asistente/001_asistente_grants.sql`,
-- que es la única migración que conoce sus nombres —llevan sufijo de ambiente—.
--
-- Las tres sentencias son una unidad: sin el GRANT, la policy que la usa no
-- devuelve cero filas sino que TIRA «permission denied for function», que es un
-- modo de falla distinto y mucho más ruidoso.
REVOKE EXECUTE ON FUNCTION identity.asistente_persona() FROM PUBLIC;
