-- ---------------------------------------------------------------------------
-- Comentarios de dominio sobre los objetos de `identity` que el asistente lee.
--
-- Estos COMMENT ON no son documentación. Son la capa que le permite al modelo
-- mapear lenguaje natural a tablas y columnas: el prefijo del prompt de sistema
-- se arma leyendo los privilegios efectivos de la conexión JUNTO CON estos
-- comentarios. Una columna sin comentario llega al modelo como un nombre pelado
-- y un tipo, y el modelo tiene que adivinar qué significa.
--
-- Por eso incluyen a propósito los SINÓNIMOS con que el Departamento nombra a
-- cada cosa —«docente», «profesor», «trámite», «cátedra»— que en el esquema no
-- aparecen. Sin ellos, «cuántos profesores tiene Algoritmos» no encuentra
-- `identity.personas`.
--
-- Alcance: solo lo que el manifiesto declara CONCEDIDO. Las tablas denegadas no
-- se comentan; describir algo que el asistente no puede leer solo sirve para que
-- lo pida y falle con permission denied en vez de abstenerse.
--
-- Ownership: viven acá, en el DDL de identity, y no en el del módulo asistente,
-- por el mismo criterio con que las policies RLS viven en el DDL de
-- designaciones — el dueño del bounded context escribe el DDL de sus objetos.
--
-- Idempotente: COMMENT ON reemplaza el comentario anterior, no acumula.
-- ---------------------------------------------------------------------------

-- ------------------------------------------------------------------ carreras

COMMENT ON TABLE identity.carreras IS
    'Carreras de grado del Departamento. Sinónimos del dominio: carrera. Las materias de una carrera NO cuelgan de esta tabla: salen de sus planes vigentes (identity.planes + identity.materias_plan). El ámbito de un Coordinador de Carrera se define sobre esta tabla.';

COMMENT ON COLUMN identity.carreras.id IS
    'Identificador de la carrera.';
COMMENT ON COLUMN identity.carreras.code IS
    'Código corto e irrepetible de la carrera, como se la nombra en la documentación interna.';
COMMENT ON COLUMN identity.carreras.name IS
    'Nombre completo de la carrera tal como se publica.';
COMMENT ON COLUMN identity.carreras.is_active IS
    'Si la carrera sigue vigente. Una carrera dada de baja conserva sus materias y su historia; filtrar por esta columna es lo que distingue "las carreras que hay" de "las carreras que hubo".';
COMMENT ON COLUMN identity.carreras.created_at IS
    'Momento de alta del registro. Es metadato del sistema, no una fecha del dominio: no expresa cuándo empezó a dictarse la carrera.';

-- ------------------------------------------------------------------ materias

COMMENT ON TABLE identity.materias IS
    'Catálogo único de materias del Departamento: UNA fila por materia, aunque se dicte en varias carreras. Sinónimos del dominio: materia, asignatura, cátedra, espacio curricular. Es la unidad sobre la que se designa a un docente y sobre la que se define el ámbito de un Jefe de Cátedra. NO tiene carrera: para saber en qué carreras se dicta, cruzar identity.materias_plan (activo) con identity.planes (vigente y activo); para la carrera de una designación o un pedido, usar su propia columna carrera_id.';

COMMENT ON COLUMN identity.materias.id IS
    'Identificador de la materia.';
COMMENT ON COLUMN identity.materias.code IS
    'Código de cinco dígitos de la materia, irrepetible en todo el Departamento.';
COMMENT ON COLUMN identity.materias.name IS
    'Nombre de la materia. ATENCIÓN: una materia compartida entre carreras es UNA sola fila, pero su nombre se repite en cada carrera donde se dicta. Toda pregunta que nombre una materia sin nombrar su carrera puede referirse a varias carreras; al contar por carrera, la misma materia suma en cada una.';
COMMENT ON COLUMN identity.materias.is_active IS
    'Si la materia sigue dictándose.';
COMMENT ON COLUMN identity.materias.created_at IS
    'Momento de alta del registro. Metadato del sistema, no fecha del dominio.';

-- ------------------------------------------------------------------ planes

COMMENT ON TABLE identity.planes IS
    'Planes de estudio de cada carrera. Catálogo informativo: dice qué materias se dictan en cada carrera. Sinónimos del dominio: plan de estudios, plan. Las materias de una carrera son las de sus planes con vigente Y activo verdaderos.';
COMMENT ON COLUMN identity.planes.id IS
    'Identificador del plan.';
COMMENT ON COLUMN identity.planes.carrera_id IS
    'Carrera a la que pertenece el plan.';
COMMENT ON COLUMN identity.planes.codigo IS
    'Código del plan, irrepetible dentro de su carrera.';
COMMENT ON COLUMN identity.planes.nombre IS
    'Nombre del plan tal como se publica.';
COMMENT ON COLUMN identity.planes.vigente IS
    'Si es el plan vigente de la carrera. Un plan activo pero no vigente es un plan anterior que todavía tiene alumnos: sus materias NO cuentan como materias actuales de la carrera.';
COMMENT ON COLUMN identity.planes.activo IS
    'Si el plan sigue cargado como activo. Un plan inactivo no aporta materias a la carrera.';
COMMENT ON COLUMN identity.planes.created_at IS
    'Momento de alta del registro. Metadato del sistema, no fecha del dominio.';

-- ------------------------------------------------------------------ materias_plan

COMMENT ON TABLE identity.materias_plan IS
    'Pertenencia de una materia a un plan: une identity.materias con identity.planes. Una materia compartida aparece en varios planes. Para "las materias de una carrera": JOIN identity.materias_plan mp ON mp.materia_id = m.id AND mp.activo JOIN identity.planes pl ON pl.id = mp.plan_id AND pl.vigente AND pl.activo, filtrando por pl.carrera_id.';
COMMENT ON COLUMN identity.materias_plan.id IS
    'Identificador de la pertenencia.';
COMMENT ON COLUMN identity.materias_plan.plan_id IS
    'Plan al que pertenece la materia.';
COMMENT ON COLUMN identity.materias_plan.materia_id IS
    'Materia que forma parte del plan.';
COMMENT ON COLUMN identity.materias_plan.activo IS
    'Si la materia sigue formando parte del plan. Una pertenencia inactiva no cuenta.';
COMMENT ON COLUMN identity.materias_plan.created_at IS
    'Momento de alta del registro. Metadato del sistema, no fecha del dominio.';

-- ------------------------------------------------------------------ personas

COMMENT ON TABLE identity.personas IS
    'Padrón de personas físicas del Departamento. Sinónimos del dominio: docente, profesor, agente. Es el registro de la PERSONA, distinto de su cuenta de acceso al sistema (identity.users) y distinto de su vínculo laboral (designaciones.designaciones). Una persona puede figurar acá sin tener cuenta y sin tener designación vigente.';

COMMENT ON COLUMN identity.personas.id IS
    'Identificador de la persona. Es la clave por la que la referencian los pedidos y las designaciones.';
COMMENT ON COLUMN identity.personas.legajo IS
    'Número de legajo institucional. Puede faltar en personas cargadas antes de que se les asignara uno.';
COMMENT ON COLUMN identity.personas.nombre IS
    'Nombre de pila.';
COMMENT ON COLUMN identity.personas.apellido IS
    'Apellido. ATENCIÓN: se repite entre personas distintas. Toda pregunta que identifique a alguien solo por apellido puede ser ambigua.';
COMMENT ON COLUMN identity.personas.created_at IS
    'Momento de alta en el padrón. Metadato del sistema: no es la fecha de ingreso a la institución.';
COMMENT ON COLUMN identity.personas.documento IS
    'Número de documento de identidad. Dato personal.';
COMMENT ON COLUMN identity.personas.cuil IS
    'Clave única de identificación laboral. Dato personal.';
COMMENT ON COLUMN identity.personas.fecha_nacimiento IS
    'Fecha de nacimiento. Dato personal.';
COMMENT ON COLUMN identity.personas.telefono IS
    'Teléfono de contacto. Dato personal.';

-- --------------------------------------------------------------------- users

COMMENT ON TABLE identity.users IS
    'Cuentas de acceso al sistema. Sinónimos del dominio: usuario, cuenta. NO es el padrón de docentes: una cuenta se vincula a lo sumo a una persona, y hay personas del padrón sin cuenta. Para preguntas sobre docentes la tabla correcta es identity.personas.';

COMMENT ON COLUMN identity.users.id IS
    'Identificador de la cuenta. Es el valor que identifica al actor de una consulta del asistente.';
COMMENT ON COLUMN identity.users.display_name IS
    'Nombre con que se muestra la cuenta en la interfaz.';
COMMENT ON COLUMN identity.users.is_active IS
    'Si la cuenta puede iniciar sesión. Una cuenta desactivada conserva su historial.';
COMMENT ON COLUMN identity.users.created_at IS
    'Momento de alta de la cuenta.';
COMMENT ON COLUMN identity.users.last_login_at IS
    'Último inicio de sesión registrado. Nulo si nunca inició sesión.';
COMMENT ON COLUMN identity.users.persona_id IS
    'Persona del padrón a la que corresponde esta cuenta. Nulo en cuentas puramente administrativas que no representan a un docente.';
COMMENT ON COLUMN identity.users.upn IS
    'Correo institucional con que la persona inicia sesión. Dato de contacto.';

-- --------------------------------------------------------------------- roles

COMMENT ON TABLE identity.roles IS
    'Catálogo de roles del sistema. Sinónimos del dominio: rol, perfil. Un rol define QUÉ puede hacer alguien (vía identity.rol_permisos) y SOBRE QUÉ ÁMBITO (columna scope). No confundir con designaciones.cargos, que es la jerarquía académica.';

COMMENT ON COLUMN identity.roles.id IS
    'Identificador del rol.';
COMMENT ON COLUMN identity.roles.code IS
    'Código del rol. Valores de sistema: docente, jefe_catedra, coordinador_carrera, secretaria, decanato, administrativo, sys_admin.';
COMMENT ON COLUMN identity.roles.name IS
    'Nombre del rol como se lo nombra en el Departamento.';
COMMENT ON COLUMN identity.roles.description IS
    'Descripción de para qué sirve el rol.';
COMMENT ON COLUMN identity.roles.scope IS
    'Ámbito sobre el que aplica el rol. Valores admitidos: global, carrera, materia. Determina si una asignación de este rol tiene que apuntar a una carrera, a una materia, o a nada.';
COMMENT ON COLUMN identity.roles.es_sistema IS
    'Si el rol viene con el sistema y no se puede eliminar ni renombrar. Los roles creados desde la administración tienen esta columna en falso.';
COMMENT ON COLUMN identity.roles.is_active IS
    'Si el rol se puede seguir asignando.';
COMMENT ON COLUMN identity.roles.created_at IS
    'Momento de alta del rol.';

-- ---------------------------------------------------------------- user_roles

COMMENT ON TABLE identity.user_roles IS
    'Asignaciones de rol a cuenta, con su ámbito. Sinónimos del dominio: membresía, asignación de rol. Una misma cuenta puede tener varios roles a la vez, y el mismo rol sobre ámbitos distintos. Es la tabla que responde "quién es coordinador de qué" y "quién es jefe de cátedra de qué materia".';

COMMENT ON COLUMN identity.user_roles.id IS
    'Identificador de la asignación.';
COMMENT ON COLUMN identity.user_roles.user_id IS
    'Cuenta a la que se le asignó el rol.';
COMMENT ON COLUMN identity.user_roles.role_id IS
    'Rol asignado.';
COMMENT ON COLUMN identity.user_roles.materia_id IS
    'Materia sobre la que aplica la asignación. Presente solo cuando el rol tiene ámbito de materia.';
COMMENT ON COLUMN identity.user_roles.carrera_id IS
    'Carrera sobre la que aplica la asignación. Presente cuando el rol tiene ámbito de carrera, y en la asignación de un docente junto con materia_id: el par materia + carrera que se eligió al asignarlo. Un Jefe de Cátedra tiene materia sin carrera.';
COMMENT ON COLUMN identity.user_roles.granted_at IS
    'Momento en que se otorgó el rol.';
COMMENT ON COLUMN identity.user_roles.created_at IS
    'Momento de alta del registro.';
COMMENT ON COLUMN identity.user_roles.deleted_at IS
    'Momento de la baja lógica. IMPORTANTE: una asignación con esta columna no nula está REVOCADA y no debe contarse. Toda consulta sobre quién tiene qué rol debe filtrar por deleted_at nulo.';

-- ------------------------------------------------------------------ permisos

COMMENT ON TABLE identity.permisos IS
    'Catálogo de permisos del sistema. Sinónimos del dominio: permiso, facultad. Un permiso es una acción concreta; los roles los agrupan.';

COMMENT ON COLUMN identity.permisos.id IS
    'Identificador del permiso.';
COMMENT ON COLUMN identity.permisos.code IS
    'Código del permiso, con la forma modulo.accion — por ejemplo designaciones.ver o aulas.gestionar.';
COMMENT ON COLUMN identity.permisos.nombre IS
    'Nombre del permiso en lenguaje del Departamento.';
COMMENT ON COLUMN identity.permisos.descripcion IS
    'Qué habilita el permiso.';
COMMENT ON COLUMN identity.permisos.created_at IS
    'Momento de alta del permiso.';

-- -------------------------------------------------------------- rol_permisos

COMMENT ON TABLE identity.rol_permisos IS
    'Qué permisos tiene cada rol. Es la tabla intermedia entre identity.roles e identity.permisos; se administra desde la interfaz sin necesidad de migración.';

COMMENT ON COLUMN identity.rol_permisos.rol_id IS
    'Rol al que se le concede el permiso.';
COMMENT ON COLUMN identity.rol_permisos.permiso_id IS
    'Permiso concedido.';
COMMENT ON COLUMN identity.rol_permisos.created_at IS
    'Momento en que se concedió.';
