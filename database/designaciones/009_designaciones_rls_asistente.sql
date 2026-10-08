-- Row Level Security sobre las cuatro tablas del trámite, para el asistente.
--
-- QUÉ DECIDE ESTA MIGRACIÓN
-- RLS decide QUÉ FILAS ve una consulta, no si quien pregunta tiene derecho a la
-- tabla. En este sistema esas dos cosas NO coinciden: el rol `docente` tiene
-- ámbito de materia, pero sus únicos permisos son portal.ver y portal.editar. Una
-- policy que mirara solo el ámbito le abriría los pedidos, el historial y los
-- justificativos de rechazo de su materia — que la API REST le niega con 403. El
-- asistente no ampliaría un permiso: crearía acceso donde no hay ninguno.
--
-- Por eso cada predicado CONJUNTA dos condiciones:
--     permiso de dominio  AND  alcance del actor
-- Un [Authorize] en el endpoint no cubre el hueco: cuando la SQL ya está
-- corriendo, el [Authorize] es pasado.
--
-- POR QUÉ `ENABLE` Y NO `FORCE ROW LEVEL SECURITY`
-- Con ENABLE, el DUEÑO de la tabla queda exento de las policies. La aplicación
-- conecta como el rol dueño (app_<ambiente>), así que sigue viendo y escribiendo
-- todo, igual que antes de esta migración. FORCE somete también al dueño: como
-- estas policies son FOR SELECT y están escritas para el actor del asistente, la
-- aplicación dejaría de ver sus propias filas y de poder escribirlas. FORCE acá
-- no endurece nada, TIRA EL BACKEND ENTERO.
--
-- A QUIÉN APUNTAN LAS POLICIES
-- No llevan cláusula TO —o sea, valen para todo el que no sea el dueño—, y no
-- nombran a los roles del asistente. El motivo es de fronteras, no de seguridad:
-- este archivo pertenece al módulo Designaciones y se embebe en su assembly,
-- mientras que los nombres de rol llevan sufijo de ambiente y solo los conoce la
-- configuración del asistente. Nombrarlos acá obligaría a que Designaciones
-- leyera la configuración de otro módulo.
--
-- La restricción real la impone el predicado, que falla CERRADO: sin el ajuste
-- `app.asistente_user_id`, identity.asistente_actor() devuelve NULL, el permiso da
-- falso y no hay ninguna fila visible. Un rol que no sea el dueño y no fije el
-- actor no ve nada, se llame como se llame.

ALTER TABLE designaciones.pedidos           ENABLE ROW LEVEL SECURITY;
ALTER TABLE designaciones.designaciones     ENABLE ROW LEVEL SECURITY;
ALTER TABLE designaciones.pedido_historial  ENABLE ROW LEVEL SECURITY;
ALTER TABLE designaciones.pedido_adjuntos   ENABLE ROW LEVEL SECURITY;

-- El predicado de alcance es UNO SOLO para los tres casos: para un actor global,
-- asistente_materias_visibles() devuelve todas las materias, así que la
-- pertenencia es verdadera para toda fila. No hace falta ramificar por ámbito, y
-- no ramificar es lo que evita que un ámbito nuevo caiga en un `ELSE` permisivo.

DROP POLICY IF EXISTS asistente_ve_pedidos ON designaciones.pedidos;
CREATE POLICY asistente_ve_pedidos
    ON designaciones.pedidos
    FOR SELECT
    USING (
        identity.asistente_tiene_permiso('designaciones.ver')
        AND materia_id IN (SELECT identity.asistente_materias_visibles())
    );

DROP POLICY IF EXISTS asistente_ve_designaciones ON designaciones.designaciones;
CREATE POLICY asistente_ve_designaciones
    ON designaciones.designaciones
    FOR SELECT
    USING (
        identity.asistente_tiene_permiso('designaciones.ver')
        AND materia_id IN (SELECT identity.asistente_materias_visibles())
    );

-- Historial y adjuntos no tienen materia propia: cuelgan del pedido. El EXISTS
-- vuelve a pasar por designaciones.pedidos, que también tiene RLS, así que la
-- fila del pedido tiene que ser visible para el actor además de existir.

DROP POLICY IF EXISTS asistente_ve_pedido_historial ON designaciones.pedido_historial;
CREATE POLICY asistente_ve_pedido_historial
    ON designaciones.pedido_historial
    FOR SELECT
    USING (
        identity.asistente_tiene_permiso('designaciones.ver')
        AND EXISTS (
            SELECT 1
              FROM designaciones.pedidos p
             WHERE p.id = pedido_id
               AND p.materia_id IN (SELECT identity.asistente_materias_visibles())
        )
    );

DROP POLICY IF EXISTS asistente_ve_pedido_adjuntos ON designaciones.pedido_adjuntos;
CREATE POLICY asistente_ve_pedido_adjuntos
    ON designaciones.pedido_adjuntos
    FOR SELECT
    USING (
        identity.asistente_tiene_permiso('designaciones.ver')
        AND EXISTS (
            SELECT 1
              FROM designaciones.pedidos p
             WHERE p.id = pedido_id
               AND p.materia_id IN (SELECT identity.asistente_materias_visibles())
        )
    );
