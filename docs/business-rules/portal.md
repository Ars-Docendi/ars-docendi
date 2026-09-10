# Business rules: `portal`

## Contexto

- **Módulo / superficie:** `backend/src/Modules.Portal/`, `database/portal/` y el acceso de lectura de `Modules.Asistente` sobre el schema `portal`.
- **Owner / stakeholders:** Secretaría Académica del Departamento (decide quién consulta qué); el docente (es el titular del dato y quien lo carga).
- **Change/Spec OpenSpec relacionado:** `openspec/changes/asistente-lee-portal/` (por crear, ARS-100) y `openspec/specs/perfil-docente-portal/`.
- **Normativa de referencia:** Ley 25.326 de Protección de los Datos Personales (Argentina). **Las citas de artículo están pendientes de verificación contra el texto vigente** — ver la advertencia de abajo.

> **Sobre las citas.** Este documento nombra artículos de la Ley 25.326 por su contenido conocido, no por una lectura del texto oficial hecha desde el repositorio. Antes de tratarlo como respaldo normativo hay que **confirmar la redacción vigente** y consultar con el área legal de la universidad si el banco de datos está inscripto ante la AAIP y quién actualiza esa declaración cuando cambian los destinatarios. Registrar una cita que nadie verificó es peor que no tener ninguna: la primera se cree.

> **Alcance.** Estas reglas gobiernan **qué puede consultar el asistente conversacional sobre el perfil profesional de un docente**, y quién puede habilitarlo. No gobiernan la API REST del portal, cuya defensa es `ServicioPortal.PersonaActualAsync` más el filtro por dueño del repositorio.

## Reglas

### BR-`portal`-001 El acceso del asistente al portal exige finalidad declarada

- **Statement:** Ningún rol puede consultar por el asistente el perfil profesional de otra persona sin que Secretaría Académica haya declarado por escrito para qué tarea concreta lo necesita.
- **Rationale:** Los datos del portal se recogieron para que el docente mantenga su propia ficha. Consultarlos cruzados a través del asistente es una **finalidad distinta**: hoy están disponibles perfil por perfil y en la práctica nadie los recorre; con el asistente se responden en dos segundos. Lo que desaparece no es la protección formal sino la fricción, y la fricción era parte de la protección.
- **Provenance:** `from_regulation`
- **Fuente normativa:** Ley 25.326, art. 4 inc. 3 — los datos no pueden usarse para finalidades distintas o incompatibles con aquellas que motivaron su obtención. _(Cita pendiente de verificación.)_
- **Ejemplos:** Secretaría declara «buscar docentes con perfil adecuado ante una vacante o una acreditación CONEAU» → el permiso tiene sustento. Nadie declara nada y alguien concede el permiso «por las dudas» → no lo tiene.
- **Finalidad declarada (2026-09-09):** _«Consulta general del plantel docente: que las autoridades del Departamento —Secretaría, Decanato, Administración— y el Coordinador de Carrera conozcan la formación, la experiencia laboral, las certificaciones y las habilidades declaradas del plantel a su alcance, sin exigir un trámite específico de por medio.»_

  **Quién la declaró:** Franco Garcete, autor del Trabajo Final Integrador. **No es Secretaría Académica**, que es a quien el statement de esta regla le atribuye la declaración. Queda escrito así a propósito: si el sistema se audita, la diferencia entre «lo declaró el titular del banco de datos» y «lo declaró el desarrollador» es exactamente lo que un auditor va a mirar, y descubrirlo en ese momento es peor que tenerlo anotado desde ahora. **Ratificarla con Secretaría sigue pendiente.**

  **Consecuencia asumida:** es la finalidad más amplia de las que se evaluaron, y por eso **no acota el `GRANT`**: se concede la trayectoria completa —formación, experiencia, certificaciones y habilidades— sin vínculo con un trámite. Una finalidad más estrecha, como «armar el proyecto docente», habría permitido recortar columnas. Se eligió la amplia con conocimiento de eso.

- **Roles afectados:** todos los que pueden usar el asistente.
- **Implementación:** el permiso `portal.ver_trayectoria_ajena` nace concedido a nadie (`database/identity/015_identity_permiso_portal_trayectoria.sql`). Conceder es una decisión de Secretaría, no un despliegue.
- **Mapping a test:** `PermisoYPersonaPortalTests.El_permiso_existe_y_no_lo_tiene_ningun_rol`.

### BR-`portal`-002 Quién puede otorgar el permiso de trayectoria ajena

- **Statement:** `portal.ver_trayectoria_ajena` sólo se concede por decisión registrada de Secretaría Académica, nombrando el rol y la finalidad. No se concede a un usuario individual ni «para probar».
- **Rationale:** **Es la regla de control más importante del paquete**, porque la frontera real es administrativa y no técnica. La RLS impone el permiso, pero el permiso se concede desde `/roles` en treinta segundos y sin migración. Sin esta regla, la política efectiva es «cualquiera con `roles.gestionar_membresia`».
- **Provenance:** `from_regulation`
- **Fuente normativa:** Ley 25.326, art. 4 inc. 1 (pertinencia y no excesividad respecto de la finalidad). _(Cita pendiente de verificación.)_
- **Ejemplos:** Secretaría concede el permiso al rol `secretaria` con la finalidad de BR-001 registrada → correcto. Un administrador se lo concede a `jefe_catedra` sin registro → viola esta regla aunque el sistema lo permita.
- **Roles afectados:** quien administra la matriz de roles.
- **Quién puede otorgarlo (2026-09-09):** el rol `secretaria`, y sólo ese. Es lo que ya impone el código y no hizo falta cambiarlo: la pantalla exige `roles.administrar` o `roles.gestionar_membresia` (`frontend/src/features/roles/pages/IndexPage.tsx:35-37`), y `database/identity/008_identity_rol_permisos.sql` le da esos dos permisos únicamente a `secretaria` — ni Decanato, ni Coordinador, ni Administrativo.
- **Nota sobre la pantalla:** la regla decía `/membresia-roles`, que **dejó de existir**: esa feature se borró y hoy `router.tsx` la redirige a `/roles`.
- **Consecuencia asumida:** el sistema **no puede impedir** una concesión indebida; sólo puede dejarla registrada. Esta regla existe para que el registro sea la política y no una formalidad.

### BR-`portal`-007 El permiso se ejerce dentro del ámbito del rol

- **Statement:** `portal.ver_trayectoria_ajena` habilita a ver la trayectoria de otras personas **dentro del ámbito del actor**: el jefe de cátedra alcanza a quienes tienen designación vigente en sus materias, el coordinador a los de su carrera, y los roles de ámbito departamental —Secretaría, Administración, Decanato— a todo el padrón.
- **Rationale:** el permiso solo era una frontera de todo o nada, y con él un Coordinador veía el padrón completo. Es más de lo que «alcance de carrera» significa en el resto del sistema, y más de lo que Secretaría pidió. El ámbito ya vive en `identity.user_roles` y el motor sabe evaluarlo; reproducirlo con permisos distintos por rol lo movería a la matriz de permisos, donde nada impediría dárselo a alguien sin cátedra.
- **Provenance:** `from_regulation`
- **Fuente normativa:** Ley 25.326, art. 4 inc. 1 (pertinencia y no excesividad respecto de la finalidad). _(Cita pendiente de verificación.)_
- **Consecuencia asumida:** un docente **sin designación vigente** no lo alcanza ningún rol no global. Es la lectura literal de «sus profesores asignados» y tiene filo: entre períodos, un docente desaparece de la vista de su jefe justo cuando hay que renovarlo. Se eligió sobre las alternativas —última designación, o período de gracia— porque las dos hacen que el alcance nunca se achique.
- **Roles afectados:** todos los que pueden usar el asistente.
- **Implementación:** `identity.asistente_alcanza_a(persona)`, invocada por las seis policies de `database/portal/005_portal_rls_ambito.sql`.
- **Mapping a test:** `RlsPortalAsistenteTests`, en particular `El_vocabulario_de_habilidades_sigue_al_alcance_de_quien_las_declaro`.

### BR-`portal`-003 El contacto personal no es consultable por el asistente

- **Statement:** El teléfono y el mail que el docente carga en la sección Contacto de su portal no se exponen al asistente conversacional, para ningún rol y bajo ningún permiso.
- **Rationale:** El contacto **institucional** ya está disponible por `identity.personas.telefono` e `identity.users.upn`, así que exponer el personal no habilita ninguna pregunta que el institucional no cubra: es todo riesgo y ningún valor nuevo. Y hay un agravante — el design spec del portal decidió explícitamente no poner copy que explique quién ve esos datos, así que el docente los cargó sin que nadie le dijera adónde iban.
- **Provenance:** `from_regulation`
- **Fuente normativa:** Ley 25.326, art. 4 inc. 1 (minimización) y art. 6 (información al titular sobre los destinatarios). _(Citas pendientes de verificación.)_
- **Ejemplos:** «¿cuál es el celular de Juan Pérez?» → el asistente no puede responderlo, y no porque falle: la tabla no se le concede. «¿cuál es el mail institucional de Juan Pérez?» → sí, por identity y con el permiso de datos personales.
- **Roles afectados:** todos.
- **Implementación:** `portal.contactos` figura como `denegada-explicita` en `database/asistente/manifiesto-privilegios.json`, con el motivo escrito. La denegación es de **tabla entera** y no columna por columna, para que reabrirla exija editar esa entrada.
- **Mapping a test:** `RlsPortalAsistenteTests.El_asistente_no_llega_a_las_tablas_que_no_se_le_conceden` (verifica `42501`, no cero filas).

### BR-`portal`-004 El asistente no entrega el archivo del CV

- **Statement:** El asistente puede decir que un docente cargó su CV y de cuándo es, pero nunca devuelve la ubicación ni el contenido del archivo.
- **Rationale:** Entregar la URI convierte una consulta de metadata en una descarga, y el asistente no es un directorio de archivos. Es el mismo criterio que ya se aplicó a `designaciones.pedido_adjuntos.uri`.
- **Provenance:** `from_spec`
- **Fuente normativa:** No es normativa: es coherencia con el precedente del módulo Designaciones.
- **Ejemplos:** «¿quién cargó su CV este año?» → contestable si `portal.cvs` se concediera. «dame el CV de X» → no.
- **Roles afectados:** todos.
- **Estado:** hoy `portal.cvs` **no se concede en absoluto**, porque ninguna pregunta del catálogo la necesita. La regla queda escrita para el día que se conceda la metadata.

### BR-`portal`-005 Toda respuesta apoyada en el portal declara su cobertura

- **Statement:** Cuando la respuesta se apoya en datos del portal, el asistente declara sobre cuántas personas del padrón existe ese dato. Nunca afirma que nadie cumple una condición sin decir cuántos la cargaron.
- **Rationale:** El portal es autodeclarado y opcional, y el propio design spec dice que «el problema del Departamento es que los docentes no cargan nada». «Ningún docente sabe Python» y «nadie cargó sus habilidades» son la **misma consulta** con el mismo cero, y decir el primero cuando la verdad es el segundo es una afirmación falsa sobre personas reales.
- **Provenance:** `from_spec`
- **Fuente normativa:** No es normativa: deriva de la métrica primaria del proyecto, corrección con abstención.
- **Ejemplos:** «no encontré ninguno. De 120 docentes, 14 cargaron sus habilidades» → correcto. «ningún docente sabe Python» a secas → viola la regla.
- **Roles afectados:** todos.
- **Mapping a test:** `CoberturaDelPortalTests`, en particular `Con_cobertura_el_actor_que_alcanza_todo_ya_no_afirma_ausencia_a_secas`.

### BR-`portal`-006 Contar y enumerar no se pueden separar

- **Statement:** Quien puede preguntarle al asistente cuántos docentes cumplen una condición de perfil, puede también preguntar quiénes son. El sistema **no ofrece** un nivel intermedio de acceso estadístico anónimo.
- **Rationale:** `ValidadorDeSql` es una lista negra de funciones y palabras clave: no analiza la forma del `SELECT`. Las dos consultas atraviesan el mismo validador, el mismo permiso y la misma policy. Registrarlo como regla evita que alguien prometa lo contrario en una conversación con el cliente.
- **Provenance:** `inferred_from_code`
- **Fuente normativa:** No es normativa: es una limitación declarada del sistema.
- **Roles afectados:** quien negocia el alcance con Secretaría.
- **Consecuencia asumida:** si el Departamento quisiera estadística agregada sin identificar personas, **eso no es lo que este sistema construye** y haría falta otra superficie.
