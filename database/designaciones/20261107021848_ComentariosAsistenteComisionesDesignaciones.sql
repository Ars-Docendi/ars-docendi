-- 20261107021848_ComentariosAsistenteComisionesDesignaciones (Designaciones).
-- No editar después de publicar: crear otra migración incremental.
--
-- Comentarios de dominio sobre los objetos de `comisiones` que el asistente lee.
--
-- ESTOS COMMENT ON NO SON DOCUMENTACIÓN, SON FUNCIONALIDAD: el prefijo del prompt se
-- arma leyendo los privilegios efectivos JUNTO CON estos comentarios, y de ahí sale
-- cómo el modelo traduce «comisión 02», «turno noche» o «los sábados» a columnas.
-- Como el prefijo no puede leer valores de estas tablas (la conexión que lo arma no
-- tiene actor y la RLS le devolvería cero filas), los hechos que el esquema no dice
-- van acá y no en una lista de valores.
--
-- Alcance: sólo lo CONCEDIDO por el manifiesto de privilegios. `sga_importaciones` y
-- las columnas denegadas no se comentan: describir lo que el asistente no puede leer
-- sólo sirve para que lo pida y choque con permission denied.
--
-- Hechos que el modelo no puede inferir y que cambian la respuesta correcta:
--   * el snapshot ACUMULA varios períodos lectivos, y no hay marca de «el actual»;
--   * `aula` y `responsabilidad` son texto libre sin catálogo;
--   * `dia` es 1 a 7 con lunes = 1 (sábado = 6, domingo = 7), según el mapeo del
--     importador de Guaraní (AgrupadorComisionesGuarani.Dia);
--   * turnos, por convención institucional y por `desde`: mañana antes de las 12:00,
--     tarde de 12:00 a 17:59, noche desde las 18:00;
--   * un vínculo nulo (materia_id, carrera_id, persona_id) significa «no existe en el
--     sistema», no «sin materia»;
--   * el docente de una comisión (Guaraní, por período) NO es una designación (el
--     nombramiento administrativo): «quién dicta X» sigue respondiéndose por
--     designaciones salvo que la pregunta nombre comisión, horario, aula, día, turno,
--     cuatrimestre o período.
--
-- Idempotente: COMMENT ON reemplaza el comentario anterior, no acumula.

-- ---------------------------------------------------------------- sga_comisiones

COMMENT ON TABLE comisiones.sga_comisiones IS
    'Comisiones de cursada copiadas de SIU-Guaraní: una fila por comisión de una materia en un período lectivo. Sinónimos del dominio: comisión, comisión de cursada, cursada, turno de una materia. El snapshot ACUMULA varios períodos lectivos (varios cuatrimestres y años a la vez) y no tiene marca del período actual: filtrar por anio_academico y periodo_lectivo_nombre, y si la pregunta no dice el período, tomar el anio_academico más reciente y agrupar la respuesta por periodo_lectivo_nombre. Los docentes, carreras y horarios de la comisión están en sga_comision_docentes, sga_comision_carreras y sga_comision_horarios, unidos por comision_guarani_id. NO confundir al docente de una comisión (quién la dicta según Guaraní en ese período) con una designación (el nombramiento administrativo, en designaciones.designaciones).';

COMMENT ON COLUMN comisiones.sga_comisiones.comision_guarani_id IS
    'Identificador de la comisión en Guaraní. Clave de la tabla y columna de unión con las tres tablas hijas. Es un texto opaco: no se muestra al usuario.';
COMMENT ON COLUMN comisiones.sga_comisiones.anio_academico IS
    'Año académico del período lectivo, por ejemplo 2026. Con varios períodos importados, el más reciente es el valor máximo de esta columna.';
COMMENT ON COLUMN comisiones.sga_comisiones.periodo_lectivo_nombre IS
    'Nombre del período lectivo tal como lo informa Guaraní, por ejemplo «1er cuatrimestre». Un mismo anio_academico puede tener más de un período: «este cuatrimestre» o «este período» sin más datos se responde por cada período del año más reciente, nombrándolos, sin elegir uno ni mezclarlos.';
COMMENT ON COLUMN comisiones.sga_comisiones.nombre IS
    'Nombre de la comisión dentro de la materia: es lo que el Departamento llama «comisión 01», «comisión 02». Se repite entre materias y entre períodos: identifica la comisión sólo junto con la materia y el período.';
COMMENT ON COLUMN comisiones.sga_comisiones.materia_codigo IS
    'Código de la materia en Guaraní, como texto. No es identity.materias.code ni está garantizado que coincida.';
COMMENT ON COLUMN comisiones.sga_comisiones.materia_nombre IS
    'Nombre de la materia tal como figura en Guaraní. Está siempre presente aunque materia_id sea nulo, así que es la columna segura para buscar por nombre de materia.';
COMMENT ON COLUMN comisiones.sga_comisiones.materia_id IS
    'Materia del sistema a la que corresponde la comisión (clave foránea a identity.materias). Nulo significa que la materia de Guaraní no existe en el sistema, no que la comisión no tenga materia: para cruzar con designaciones por materia hay que unir por esta columna y descartar los nulos.';
COMMENT ON COLUMN comisiones.sga_comisiones.modalidad IS
    'Modalidad de cursada en texto de Guaraní, por ejemplo presencial o virtual. Puede ser nula.';
COMMENT ON COLUMN comisiones.sga_comisiones.ubicacion IS
    'Sede o ubicación de la comisión en texto libre de Guaraní. Puede ser nula.';

-- -------------------------------------------------------- sga_comision_carreras

COMMENT ON TABLE comisiones.sga_comision_carreras IS
    'Carreras a las que se ofrece cada comisión según Guaraní: una fila por par comisión y carrera, porque una comisión puede servir a varias carreras. Se une a sga_comisiones por comision_guarani_id.';

COMMENT ON COLUMN comisiones.sga_comision_carreras.comision_guarani_id IS
    'Comisión a la que pertenece la fila (clave foránea a sga_comisiones).';
COMMENT ON COLUMN comisiones.sga_comision_carreras.carrera_codigo IS
    'Código de la carrera en Guaraní, como texto. No es identity.carreras.code.';
COMMENT ON COLUMN comisiones.sga_comision_carreras.carrera_nombre IS
    'Nombre de la carrera tal como figura en Guaraní. Siempre presente aunque carrera_id sea nulo: es la columna segura para buscar por nombre de carrera.';
COMMENT ON COLUMN comisiones.sga_comision_carreras.carrera_id IS
    'Carrera del sistema (clave foránea a identity.carreras). Nulo significa que la carrera de Guaraní no existe en el sistema.';

-- -------------------------------------------------------- sga_comision_docentes

COMMENT ON TABLE comisiones.sga_comision_docentes IS
    'Docentes de cada comisión según Guaraní, con su responsabilidad: una fila por docente y comisión. Sinónimos del dominio: docente de la comisión, equipo de la comisión. Responde «quién está en la comisión 02 de tal materia», «qué días y en qué aula da clase tal docente» y «cuántas comisiones tiene cada docente» (contar comisiones distintas por docente, dentro de un período). NO es una designación: una persona puede figurar en una comisión sin tener designación vigente en esa materia, y al revés. «Quién dicta» o «quiénes dictan» una MATERIA, sin nombrar comisión, horario, aula, día, turno, cuatrimestre ni período, NO se responde con esta tabla: se responde con las designaciones vigentes (designaciones.designaciones con vigente_hasta nulo).';

COMMENT ON COLUMN comisiones.sga_comision_docentes.comision_guarani_id IS
    'Comisión que dicta el docente (clave foránea a sga_comisiones).';
COMMENT ON COLUMN comisiones.sga_comision_docentes.orden IS
    'Posición del docente en la lista de la comisión según Guaraní, desde 1. Sólo ordena: no indica jerarquía.';
COMMENT ON COLUMN comisiones.sga_comision_docentes.nombre IS
    'Nombre del docente tal como lo informa Guaraní. Siempre presente, incluso cuando persona_id es nulo, así que es la columna que no pierde docentes: usarla para mostrar y, cuando no hay vínculo, para contar o buscar.';
COMMENT ON COLUMN comisiones.sga_comision_docentes.responsabilidad IS
    'Responsabilidad del docente en la comisión, en TEXTO LIBRE de Guaraní (por ejemplo Titular, Adjunto, Jefe de Trabajos Prácticos). No es un catálogo cerrado y puede traer valores inesperados: comparar con ILIKE y no con igualdad. Puede ser nula.';
COMMENT ON COLUMN comisiones.sga_comision_docentes.persona_id IS
    'Persona del sistema a la que corresponde el docente (clave foránea a identity.personas). Nulo significa que el docente de Guaraní no se pudo vincular a una persona del sistema: esas filas siguen siendo docentes de la comisión y se identifican por nombre. Para cruzar con designaciones hay que unir por esta columna y descartar los nulos.';

-- -------------------------------------------------------- sga_comision_horarios

COMMENT ON TABLE comisiones.sga_comision_horarios IS
    'Horarios de cursada de cada comisión: una fila por franja (día, hora de inicio y fin, aula); una comisión puede tener varias. Responde en qué días y horarios se dicta, en qué aula, y qué comisiones son de noche o de sábado. Se une a sga_comisiones por comision_guarani_id. No hay catálogo de aulas ni reservas: sólo se sabe qué aula tiene asignada cada franja, así que NO responde qué aulas están libres.';

COMMENT ON COLUMN comisiones.sga_comision_horarios.comision_guarani_id IS
    'Comisión a la que pertenece la franja (clave foránea a sga_comisiones).';
COMMENT ON COLUMN comisiones.sga_comision_horarios.dia IS
    'Día de la semana, de 1 a 7 con lunes = 1: martes = 2, miércoles = 3, jueves = 4, viernes = 5, sábado = 6, domingo = 7. «Los sábados» es dia = 6.';
COMMENT ON COLUMN comisiones.sga_comision_horarios.desde IS
    'Hora de inicio de la franja (hora local, sin fecha). Define el turno por convención institucional: mañana si desde es anterior a 12:00, tarde de 12:00 a 17:59, noche desde las 18:00. «De noche» es desde >= 18:00.';
COMMENT ON COLUMN comisiones.sga_comision_horarios.hasta IS
    'Hora de fin de la franja. Siempre posterior a desde. El turno no se decide con esta columna.';
COMMENT ON COLUMN comisiones.sga_comision_horarios.aula IS
    'Aula asignada a la franja, en TEXTO LIBRE de Guaraní: no hay catálogo, de modo que la misma aula puede estar escrita de varias formas; buscar con ILIKE. Puede ser nula. No sirve para saber qué aulas están libres.';
