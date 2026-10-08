-- ---------------------------------------------------------------------------
-- Comentarios de dominio sobre los objetos de `portal` que el asistente lee.
--
-- ESTOS COMMENT ON NO SON DOCUMENTACIÓN, SON FUNCIONALIDAD. El prefijo del prompt
-- de sistema se arma leyendo los privilegios efectivos de la conexión JUNTO CON
-- estos comentarios: alrededor del 62 % de ese prefijo es texto de comentario, y de
-- ahí sale la calidad de la traducción. Una columna sin comentario le llega al
-- modelo como un nombre pelado y un tipo, y tiene que adivinar qué significa.
--
-- Alcance: SOLO las seis tablas que el catálogo de preguntas necesita. `contactos`,
-- `cvs`, `proyectos` y `proyecto_documentos` no se comentan porque no se conceden,
-- y describirle al modelo algo que no puede leer sirve únicamente para que lo pida
-- y choque con permission denied en vez de abstenerse.
--
-- Los sinónimos van a propósito: el Departamento dice «título», «posgrado»,
-- «antecedentes», «CV», y ninguna de esas palabras aparece en el esquema. Sin
-- ellos, «quiénes tienen posgrado» no encuentra `portal.educaciones`.
--
-- LO QUE ESTOS COMENTARIOS TIENEN QUE DECIR Y LOS DE IDENTITY NO. Portal es
-- AUTODECLARADO: lo carga el docente sobre sí mismo, nadie lo valida, y está casi
-- vacío. El modelo tiene que saber las tres cosas, porque cambian qué respuesta es
-- correcta. Un conteo sobre portal NO es un conteo sobre el Departamento: es un
-- conteo sobre quienes cargaron el dato.
--
-- Ownership: viven acá, en el DDL de portal, por el mismo criterio con que las
-- policies del asistente sobre designaciones viven en el DDL de designaciones — el
-- dueño del bounded context escribe el DDL de sus objetos.
--
-- Idempotente: COMMENT ON reemplaza el comentario anterior, no acumula.
-- ---------------------------------------------------------------------------

-- ------------------------------------------------------------------ perfiles

COMMENT ON TABLE portal.perfiles IS
    'Perfil profesional que cada docente carga sobre sí mismo en el Portal Docente. Sinónimos del dominio: perfil, ficha, antecedentes, currículum. ES EL PUENTE entre el portal y el padrón: para llegar al nombre de una persona hay que unir portal.perfiles.persona_id con identity.personas.id. ATENCIÓN AL RESPONDER: el perfil es AUTODECLARADO y opcional, y hoy la mayoría del padrón no cargó nada. Un conteo sobre estas tablas cuenta a quienes cargaron el dato, NO al Departamento: nunca afirmes que nadie tiene una habilidad o un título si lo que pasa es que nadie lo declaró.';

COMMENT ON COLUMN portal.perfiles.id IS
    'Identificador del perfil. Las tablas de trayectoria cuelgan de acá por perfil_id.';
COMMENT ON COLUMN portal.perfiles.persona_id IS
    'Persona del padrón a la que pertenece el perfil. Referencia identity.personas.id, y es el ÚNICO camino de portal hacia un nombre y un legajo. Es único: una persona tiene a lo sumo un perfil.';
COMMENT ON COLUMN portal.perfiles.created_at IS
    'Momento en que se creó el perfil. Es metadato del sistema: no expresa cuándo el docente terminó de cargarlo, ni sirve para saber si está completo.';

-- --------------------------------------------------------------- educaciones

COMMENT ON TABLE portal.educaciones IS
    'Formación académica que el docente declara. Sinónimos del dominio: título, formación, estudios, posgrado, carrera cursada. Una fila por título; un docente puede tener varias y también ninguna. NO es el cargo docente ni la materia que dicta —eso está en designaciones—: acá dice qué estudió, no qué enseña.';

COMMENT ON COLUMN portal.educaciones.id IS
    'Identificador del registro de formación.';
COMMENT ON COLUMN portal.educaciones.perfil_id IS
    'Perfil al que pertenece la formación. Para llegar al docente hay que pasar por portal.perfiles y de ahí a identity.personas.';
COMMENT ON COLUMN portal.educaciones.nivel IS
    'Nivel del título, en cuatro valores: Grado, Especialización, Maestría, Doctorado. «Posgrado» en el lenguaje del Departamento abarca los tres últimos, no es un valor de esta columna. El nivel lo elige el docente de una lista cerrada de la interfaz.';
COMMENT ON COLUMN portal.educaciones.carrera IS
    'Nombre de la carrera o del título, escrito libremente por el docente. ATENCIÓN: NO se corresponde con identity.carreras, que son las carreras que el Departamento dicta. Acá dice dónde estudió él, no qué se dicta acá, y la redacción varía entre personas.';
COMMENT ON COLUMN portal.educaciones.institucion IS
    'Universidad o institución donde cursó, escrita libremente. La misma institución puede aparecer escrita de varias maneras.';
COMMENT ON COLUMN portal.educaciones.desde IS
    'Fecha de inicio de la cursada, declarada por el docente.';
COMMENT ON COLUMN portal.educaciones.hasta IS
    'Fecha de finalización. NULO significa EN CURSO: un título con hasta nulo todavía no se obtuvo, y contarlo como obtenido es afirmar algo falso sobre una persona.';
COMMENT ON COLUMN portal.educaciones.created_at IS
    'Momento de carga del registro. Metadato del sistema, no una fecha del dominio.';

-- ----------------------------------------------------------- certificaciones

COMMENT ON TABLE portal.certificaciones IS
    'Certificaciones profesionales que el docente declara. Sinónimos del dominio: certificación, credencial, acreditación. Se distingue de portal.educaciones en que no es un título académico: son credenciales que suelen vencer, como las de proveedores de tecnología.';

COMMENT ON COLUMN portal.certificaciones.id IS
    'Identificador de la certificación.';
COMMENT ON COLUMN portal.certificaciones.perfil_id IS
    'Perfil al que pertenece la certificación.';
COMMENT ON COLUMN portal.certificaciones.nombre IS
    'Nombre de la certificación tal como la escribió el docente. La misma certificación puede figurar con nombres distintos entre personas.';
COMMENT ON COLUMN portal.certificaciones.emisor IS
    'Organismo o empresa que la emitió, escrito libremente.';
COMMENT ON COLUMN portal.certificaciones.fecha IS
    'Fecha de emisión.';
COMMENT ON COLUMN portal.certificaciones.vencimiento IS
    'Fecha de vencimiento. NULO significa QUE NO VENCE, no que ya venció: para preguntas sobre certificaciones vigentes o por vencer, una fila con vencimiento nulo está vigente para siempre y no debe contarse como vencida.';
COMMENT ON COLUMN portal.certificaciones.created_at IS
    'Momento de carga del registro. Metadato del sistema.';

-- -------------------------------------------------------------- experiencias

COMMENT ON TABLE portal.experiencias IS
    'Experiencia laboral que el docente declara. Sinónimos del dominio: experiencia, antecedentes laborales, trayectoria profesional. Incluye trabajos fuera de la universidad, que es lo que la distingue de las designaciones: acá está lo que hizo en la industria o en otras instituciones.';

COMMENT ON COLUMN portal.experiencias.id IS
    'Identificador de la experiencia.';
COMMENT ON COLUMN portal.experiencias.perfil_id IS
    'Perfil al que pertenece la experiencia.';
COMMENT ON COLUMN portal.experiencias.puesto IS
    'Puesto o rol que ocupó, escrito libremente. NO es un cargo docente: los cargos del Departamento están en designaciones.cargos y tienen un catálogo cerrado.';
COMMENT ON COLUMN portal.experiencias.organizacion IS
    'Empresa u organización donde trabajó, escrita libremente.';
COMMENT ON COLUMN portal.experiencias.desde IS
    'Fecha de inicio en ese puesto.';
COMMENT ON COLUMN portal.experiencias.hasta IS
    'Fecha de fin. NULO significa QUE SIGUE EN EL PUESTO: es el trabajo actual, no un dato faltante.';
COMMENT ON COLUMN portal.experiencias.created_at IS
    'Momento de carga del registro. Metadato del sistema.';

-- --------------------------------------------------------------- habilidades

COMMENT ON TABLE portal.habilidades IS
    'Vocabulario compartido de habilidades e intereses. Sinónimos del dominio: habilidad, competencia, skill, tema, área de interés. NO ES UN CATÁLOGO CURADO: cada término lo tipea un docente en su propio portal, así que el mismo concepto puede figurar escrito de varias formas y hay términos que declaró una sola persona. Esta tabla es sólo el término; quién lo declaró está en portal.docente_habilidades.';

COMMENT ON COLUMN portal.habilidades.id IS
    'Identificador del término.';
COMMENT ON COLUMN portal.habilidades.termino IS
    'El término tal como lo escribió quien lo declaró primero, con mayúsculas y acentos.';
COMMENT ON COLUMN portal.habilidades.termino_norm IS
    'El mismo término normalizado A MAYÚSCULAS, que es por donde hay que comparar y agrupar: buscar por termino falla ante una diferencia de mayúsculas. Para filtrar por un término hay que escribirlo en mayúsculas —termino_norm = ''KUBERNETES''— o normalizar el literal con upper(); comparar contra un literal en minúsculas no devuelve nada.';
COMMENT ON COLUMN portal.habilidades.created_at IS
    'Momento en que alguien declaró el término por primera vez. Metadato del sistema.';

-- SIN COMENTAR, PORQUE NO SE CONCEDEN. No es un olvido y conviene que quede escrito
-- acá, donde se ve la ausencia:
--
--   · `usos` es un contador AGREGADO sobre todo el padrón. Una policy por fila no
--     puede acotarlo —la fila es una sola y su número ya vio todo—, así que un actor
--     sin permiso leería cuánta gente declaró un término aunque no pueda ver a
--     ninguna. Es el único canal de esta tabla que la RLS no cierra.
--   · `sugerido` y `canonica_id` son maquinaria de curaduría del vocabulario, no
--     dominio: no responden ninguna pregunta del catálogo.
--   · `portal.experiencias.descripcion` es texto libre donde el docente escribe lo
--     que quiera sobre sí mismo. Clasificarla como sensible NO alcanza: el
--     enmascarador identifica la columna por (OID, attnum) y una expresión —un
--     `upper(descripcion)`, por ejemplo— reporta OID 0, que se trata como pública.
--     O no se concede, o viaja verbatim al proveedor del modelo. No se concede.

-- -------------------------------------------------------- docente_habilidades

COMMENT ON TABLE portal.docente_habilidades IS
    'Qué habilidades e intereses declaró cada docente. Es la tabla puente entre portal.perfiles y portal.habilidades, y la que responde «quién sabe tal cosa» o «a quién le interesa tal tema». Un docente puede declarar el mismo término como habilidad y como interés a la vez.';

COMMENT ON COLUMN portal.docente_habilidades.perfil_id IS
    'Perfil que declaró el término. Para llegar al nombre hay que pasar por portal.perfiles y de ahí a identity.personas.';
COMMENT ON COLUMN portal.docente_habilidades.habilidad_id IS
    'Término declarado, en portal.habilidades.';
COMMENT ON COLUMN portal.docente_habilidades.tipo IS
    'Distingue dos cosas que NO significan lo mismo: «habilidad» es algo que el docente declara saber, «interes» es algo que le gustaría hacer o dictar aunque no lo domine. Una pregunta sobre quién sabe algo filtra por habilidad; una sobre quién querría dictar algo filtra por interes. Confundirlos atribuye a alguien una competencia que no declaró.';
COMMENT ON COLUMN portal.docente_habilidades.created_at IS
    'Momento en que se declaró. Metadato del sistema.';
