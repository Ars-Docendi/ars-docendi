-- ---------------------------------------------------------------------------
-- Comentarios de dominio sobre los objetos de `designaciones` que el asistente
-- lee. Ver la nota extensa en database/identity/013_identity_comentarios_asistente.sql:
-- estos COMMENT ON son la capa que le permite al modelo mapear lenguaje natural
-- a tablas, no documentación para humanos.
--
-- Alcance: solo lo CONCEDIDO por el manifiesto. Quedan sin comentar
-- `designaciones.idempotencia_comandos` (denegada entera) y
-- `designaciones.pedidos.snapshot` y `designaciones.pedido_adjuntos.uri`
-- (columnas denegadas).
--
-- Idempotente: COMMENT ON reemplaza el comentario anterior, no acumula.
-- ---------------------------------------------------------------------------

-- -------------------------------------------------------------------- cargos

COMMENT ON TABLE designaciones.cargos IS
    'Catálogo de cargos docentes, de mayor a menor jerarquía. Sinónimos del dominio: cargo, categoría docente, jerarquía. Valores: Profesor Titular, Profesor Asociado, Profesor Adjunto, Jefe de Trabajos Prácticos, Ayudante de Primera, Ayudante de Segunda. NO confundir con identity.roles, que es el permiso dentro del sistema: una persona puede ser Profesor Titular sin tener ningún rol administrativo.';

COMMENT ON COLUMN designaciones.cargos.id IS
    'Identificador del cargo.';
COMMENT ON COLUMN designaciones.cargos.codigo IS
    'Código del cargo. Valores: titular, asociado, adjunto, jtp, ayudante1, ayudante2.';
COMMENT ON COLUMN designaciones.cargos.nombre IS
    'Nombre completo del cargo.';
COMMENT ON COLUMN designaciones.cargos.abreviatura IS
    'Forma abreviada del cargo, como aparece en listados y planillas — por ejemplo JTP o Ay. 1ra.';
COMMENT ON COLUMN designaciones.cargos.orden IS
    'Posición en la jerarquía académica: 1 es el cargo más alto (Profesor Titular) y 6 el más bajo (Ayudante de Segunda). Es la columna por la que se ordena un plantel por jerarquía.';
COMMENT ON COLUMN designaciones.cargos.activo IS
    'Si el cargo se puede seguir solicitando.';
COMMENT ON COLUMN designaciones.cargos.created_at IS
    'Momento de alta del registro.';

-- ------------------------------------------------------------------ periodos

COMMENT ON TABLE designaciones.periodos IS
    'Períodos de designación. Sinónimos del dominio: período, ciclo, cuatrimestre de designación. Cada período tiene una ventana de CARGA —cuándo se pueden presentar pedidos— y una ventana de IMPACTO —a qué lapso académico aplican las designaciones resultantes—. Las dos ventanas son distintas y no se solapan necesariamente.';

COMMENT ON COLUMN designaciones.periodos.id IS
    'Identificador del período.';
COMMENT ON COLUMN designaciones.periodos.nombre IS
    'Nombre del período tal como lo nombra el Departamento.';
COMMENT ON COLUMN designaciones.periodos.carga_desde IS
    'Primer día en que se pueden presentar pedidos para este período.';
COMMENT ON COLUMN designaciones.periodos.carga_hasta IS
    'Último día en que se pueden presentar pedidos para este período.';
COMMENT ON COLUMN designaciones.periodos.impacto_desde IS
    'Primer día del lapso académico al que aplican las designaciones de este período.';
COMMENT ON COLUMN designaciones.periodos.impacto_hasta IS
    'Último día del lapso académico al que aplican las designaciones de este período.';
COMMENT ON COLUMN designaciones.periodos.activo IS
    'Si es el período en curso. Hay a lo sumo UNO activo en todo el sistema. Es la forma correcta de resolver "el período actual" o "ahora": preguntar por esta bandera, nunca comparar fechas contra el reloj.';
COMMENT ON COLUMN designaciones.periodos.created_at IS
    'Momento de alta del registro.';

-- ------------------------------------------------------------------- pedidos

COMMENT ON TABLE designaciones.pedidos IS
    'Pedidos de designación docente. Sinónimos del dominio: pedido, trámite, solicitud, novedad, proyecto docente. Es la SOLICITUD en curso, no el vínculo laboral: un pedido aprobado produce una fila en designaciones.designaciones. Para preguntas sobre quién da clase hoy, la tabla es designaciones.designaciones; para preguntas sobre qué se está tramitando, es ésta.';

COMMENT ON COLUMN designaciones.pedidos.id IS
    'Identificador del pedido.';
COMMENT ON COLUMN designaciones.pedidos.numero IS
    'Número de trámite, irrepetible. Es como se lo identifica en la comunicación entre áreas.';
COMMENT ON COLUMN designaciones.pedidos.periodo_id IS
    'Período de designación al que pertenece el pedido.';
COMMENT ON COLUMN designaciones.pedidos.persona_id IS
    'Persona del padrón sobre la que trata el pedido: el docente que se da de alta, de baja o al que se le cambia el cargo.';
COMMENT ON COLUMN designaciones.pedidos.materia_id IS
    'Materia sobre la que trata el pedido. Es la columna por la que se acota el alcance de un Jefe de Cátedra o de un Coordinador de Carrera.';
COMMENT ON COLUMN designaciones.pedidos.novedad IS
    'Qué tipo de cambio se pide. Valores admitidos: "Sin novedad" (se confirma la situación actual), "Alta" (se incorpora un docente), "Baja" (se lo desvincula), "Cambio de cargo o dedicación".';
COMMENT ON COLUMN designaciones.pedidos.estado IS
    'Etapa del circuito de aprobación. Valores admitidos: borrador (todavía no se envió), en_revision_coordinador, en_revision_secretaria, en_revision_decanato, devuelto (volvió al origen con observaciones), en_lote (aprobado y agrupado para su acto administrativo), rechazado (terminal), cancelado (terminal). Un pedido "pendiente" es cualquiera en alguna de las tres etapas en_revision_*.';
COMMENT ON COLUMN designaciones.pedidos.prioritario IS
    'Si el pedido fue marcado para tratamiento prioritario.';
COMMENT ON COLUMN designaciones.pedidos.cargo_solicitado_id IS
    'Cargo que se solicita. Nulo en pedidos de baja, donde no se pide ningún cargo.';
COMMENT ON COLUMN designaciones.pedidos.dedicacion_solicitada IS
    'Dedicación que se solicita, de "Categoría 0" a "Categoría 6". Nulo cuando el pedido no la modifica.';
COMMENT ON COLUMN designaciones.pedidos.horas IS
    'Horas de dictado semanales que se solicitan.';
COMMENT ON COLUMN designaciones.pedidos.horas_investigacion IS
    'Horas semanales dedicadas a investigación que se solicitan.';
COMMENT ON COLUMN designaciones.pedidos.horas_externas IS
    'Horas semanales que la persona ya dedica a otra institución. Se declaran para evaluar la compatibilidad de cargos.';
COMMENT ON COLUMN designaciones.pedidos.justificacion IS
    'Texto libre con el fundamento del pedido, escrito por quien lo origina.';
COMMENT ON COLUMN designaciones.pedidos.tipo_baja IS
    'Motivo de la baja. Valores admitidos: Renuncia, Jubilación, Otro. Nulo cuando la novedad no es una baja.';
COMMENT ON COLUMN designaciones.pedidos.tipo_baja_detalle IS
    'Detalle del motivo cuando el tipo de baja es "Otro".';
COMMENT ON COLUMN designaciones.pedidos.etapa_retorno IS
    'Etapa a la que vuelve el pedido cuando se resuelve una devolución. Nulo si el pedido no fue devuelto.';
COMMENT ON COLUMN designaciones.pedidos.propietario_actual IS
    'Rol que tiene el pedido en su bandeja en este momento.';
COMMENT ON COLUMN designaciones.pedidos.created_at IS
    'Momento en que se creó el pedido. Es la fecha de inicio del trámite.';

-- ----------------------------------------------------------- pedido_adjuntos

COMMENT ON TABLE designaciones.pedido_adjuntos IS
    'Archivos adjuntos de un pedido. Sinónimos del dominio: adjunto, documentación respaldatoria. Registra QUÉ documentación se acompañó; el contenido del archivo no es consultable desde acá.';

COMMENT ON COLUMN designaciones.pedido_adjuntos.id IS
    'Identificador del adjunto.';
COMMENT ON COLUMN designaciones.pedido_adjuntos.pedido_id IS
    'Pedido al que acompaña.';
COMMENT ON COLUMN designaciones.pedido_adjuntos.tipo IS
    'Clase de documento. Valores admitidos: cv, dni_frente, dni_dorso, justificativo.';
COMMENT ON COLUMN designaciones.pedido_adjuntos.nombre IS
    'Nombre del archivo tal como se lo subió.';
COMMENT ON COLUMN designaciones.pedido_adjuntos.created_at IS
    'Momento en que se adjuntó.';

-- ---------------------------------------------------------- pedido_historial

COMMENT ON TABLE designaciones.pedido_historial IS
    'Bitácora del circuito de un pedido: una fila por cada acción que alguien tomó sobre él. Sinónimos del dominio: historial, trazabilidad, seguimiento. Es la tabla que responde "por dónde pasó este trámite" y "quién lo aprobó".';

COMMENT ON COLUMN designaciones.pedido_historial.id IS
    'Identificador del asiento.';
COMMENT ON COLUMN designaciones.pedido_historial.pedido_id IS
    'Pedido sobre el que se actuó.';
COMMENT ON COLUMN designaciones.pedido_historial.accion IS
    'Qué se hizo. Valores admitidos: crear, enviar, aceptar, rechazar, devolver, reenviar, editar, cancelar, priorizar, despriorizar.';
COMMENT ON COLUMN designaciones.pedido_historial.rol_id IS
    'Rol con el que actuó quien tomó la acción. La misma persona puede actuar con roles distintos en momentos distintos.';
COMMENT ON COLUMN designaciones.pedido_historial.actor_id IS
    'Cuenta que tomó la acción. Nulo cuando la acción la produjo el sistema y no una persona.';
COMMENT ON COLUMN designaciones.pedido_historial.etapa IS
    'Etapa en la que quedó el pedido después de la acción. Mismos valores que designaciones.pedidos.estado.';
COMMENT ON COLUMN designaciones.pedido_historial.comentario IS
    'Texto libre que dejó quien actuó: la observación de una devolución, el fundamento de un rechazo.';
COMMENT ON COLUMN designaciones.pedido_historial.created_at IS
    'Momento de la acción. Es la columna por la que se ordena el historial.';

-- ------------------------------------------------------------- designaciones

COMMENT ON TABLE designaciones.designaciones IS
    'Designaciones docentes vigentes e históricas: el vínculo entre una persona, una materia y un cargo, con su lapso de vigencia. Sinónimos del dominio: designación, nombramiento, cargo asignado, plantel. Es la tabla que responde quién dicta qué, con qué cargo y desde cuándo. Una persona puede tener varias designaciones simultáneas en materias distintas.';

COMMENT ON COLUMN designaciones.designaciones.id IS
    'Identificador de la designación.';
COMMENT ON COLUMN designaciones.designaciones.persona_id IS
    'Persona designada.';
COMMENT ON COLUMN designaciones.designaciones.materia_id IS
    'Materia en la que está designada.';
COMMENT ON COLUMN designaciones.designaciones.cargo_id IS
    'Cargo con el que está designada.';
COMMENT ON COLUMN designaciones.designaciones.dedicacion IS
    'Dedicación de la designación, de "Categoría 0" a "Categoría 6".';
COMMENT ON COLUMN designaciones.designaciones.horas IS
    'Horas semanales de la designación.';
COMMENT ON COLUMN designaciones.designaciones.vigente_desde IS
    'Primer día de vigencia de la designación.';
COMMENT ON COLUMN designaciones.designaciones.vigente_hasta IS
    'Último día de vigencia. NULO significa vigencia ABIERTA, o sea que la designación sigue vigente. Es la forma correcta de resolver "designaciones actuales": vigente_hasta IS NULL, nunca comparar contra el reloj.';
COMMENT ON COLUMN designaciones.designaciones.origen_pedido_id IS
    'Pedido que originó esta designación. Nulo en designaciones cargadas antes de que existiera el circuito de pedidos.';
COMMENT ON COLUMN designaciones.designaciones.created_at IS
    'Momento de alta del registro. Metadato del sistema: no es el inicio de la vigencia, que es vigente_desde.';
