-- 20261107125238_ComentariosAsistenteSolicitudesAulas (Aulas).
-- No editar después de publicar: crear otra migración incremental.
--
-- Comentarios de dominio sobre aulas.solicitudes_reserva, la única tabla de `aulas` que
-- el asistente lee.
--
-- ESTOS COMMENT ON NO SON DOCUMENTACIÓN, SON FUNCIONALIDAD: el prefijo del prompt se
-- arma leyendo los privilegios efectivos JUNTO CON estos comentarios. El prefijo no puede
-- leer valores de la tabla (la conexión que lo arma no tiene actor y la RLS devolvería
-- cero filas), así que los hechos que el esquema no dice van acá.
--
-- Hechos que el modelo no puede inferir y que cambian la respuesta correcta:
--   * la tabla son SOLICITUDES de reserva de aula para una mesa de examen, no las aulas
--     de la cursada: «en qué aula se dicta X» se responde con comisiones.sga_comision_horarios;
--   * no hay catálogo de aulas: `aula_asignada` es texto libre, sólo existe si la
--     solicitud está aprobada, y por eso no se puede saber qué aula está libre ni su
--     capacidad;
--   * estado: pendiente, aprobada, rechazada o cancelada, y qué columna acompaña a cada uno;
--   * motivo_rechazo y motivo_cancelacion son texto libre del trámite.
--
-- Alcance: sólo lo CONCEDIDO por el manifiesto de privilegios.
--
-- Idempotente: COMMENT ON reemplaza el comentario anterior, no acumula.

COMMENT ON TABLE aulas.solicitudes_reserva IS
    'Solicitudes de reserva de un aula o laboratorio para una mesa de examen, que un docente o jefe de cátedra pide y un administrativo aprueba o rechaza. Sinónimos: reserva de aula, pedido de aula, solicitud de aula. Usar esta tabla sólo cuando la pregunta dice «aula» o «reserva» (reservas de aula, solicitudes de aula, pedidos de aula). «Solicitudes» o «pedidos» a secas, sin «aula» ni «reserva» —«qué solicitudes fueron rechazadas», «solicitudes urgentes», «solicitudes de mi carrera»—, son los pedidos de designación (designaciones.pedidos), NO esta tabla. NO es el aula en que se dicta una comisión: «en qué aula se dicta X» o «aula de la comisión» se responde con comisiones.sga_comision_horarios. No hay catálogo de aulas: no sirve para saber qué aulas están libres ni qué capacidad tienen. Quien aprueba solicitudes ve todas; quien sólo solicita ve las suyas.';

COMMENT ON COLUMN aulas.solicitudes_reserva.id IS
    'Identificador de la solicitud. Es un UUID opaco: no se muestra al usuario.';
COMMENT ON COLUMN aulas.solicitudes_reserva.docente_id IS
    'Persona que hizo la solicitud: el docente o jefe de cátedra solicitante (clave foránea a identity.personas). Para decir quién pidió, unir con identity.personas.';
COMMENT ON COLUMN aulas.solicitudes_reserva.dia IS
    'Fecha (DATE) para la que se pide el aula, por ejemplo la del examen. No es el día de la semana ni el día de cursada de una comisión.';
COMMENT ON COLUMN aulas.solicitudes_reserva.horario_desde IS
    'Hora de inicio de la reserva pedida.';
COMMENT ON COLUMN aulas.solicitudes_reserva.horario_hasta IS
    'Hora de fin de la reserva pedida; siempre posterior a horario_desde.';
COMMENT ON COLUMN aulas.solicitudes_reserva.cantidad_alumnos_aprox IS
    'Cantidad aproximada de alumnos que el solicitante espera. Es una estimación del pedido, no la capacidad de ningún aula.';
COMMENT ON COLUMN aulas.solicitudes_reserva.materia_id IS
    'Materia para la que se pide el aula (clave foránea a identity.materias).';
COMMENT ON COLUMN aulas.solicitudes_reserva.comision IS
    'Comisión a la que corresponde el pedido, en texto libre tal como la escribió el solicitante (por ejemplo «02»). No hay catálogo de comisiones y no se une con comisiones.sga_comisiones.';
COMMENT ON COLUMN aulas.solicitudes_reserva.estado IS
    'Estado de la solicitud de aula: pendiente (esperando respuesta), aprobada (tiene aula_asignada), rechazada (tiene motivo_rechazo) o cancelada. Valores exactos, en minúscula.';
COMMENT ON COLUMN aulas.solicitudes_reserva.aula_asignada IS
    'Aula que se asignó, en texto libre sin catálogo (por ejemplo «Aula 101» o «Laboratorio 3»). Sólo existe cuando estado es aprobada; en cualquier otro estado es nula, y una reserva aprobada que luego se cancela deja de tener aula.';
COMMENT ON COLUMN aulas.solicitudes_reserva.motivo_rechazo IS
    'Motivo que quien rechazó escribió, en texto libre. Sólo existe cuando estado es rechazada.';
COMMENT ON COLUMN aulas.solicitudes_reserva.created_at IS
    'Cuándo se creó la solicitud (con zona horaria). No es la fecha para la que se pide el aula: esa es dia.';
COMMENT ON COLUMN aulas.solicitudes_reserva.motivo_cancelacion IS
    'Motivo en texto libre que un administrativo ingresó al cancelar una reserva ya aprobada. Es nulo cuando el docente canceló su propia solicitud pendiente: nulo no significa que no esté cancelada, el estado lo dice.';
COMMENT ON COLUMN aulas.solicitudes_reserva.rol_solicitante IS
    'Rol con el que el solicitante hizo el pedido, por ejemplo docente o jefe_catedra. Nulo en las solicitudes anteriores a que se registrara.';
