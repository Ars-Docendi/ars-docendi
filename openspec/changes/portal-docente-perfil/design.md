## Context

El módulo Portal está scaffolded (`Modules.Portal` + `Modules.Portal.Contracts` en backend, `frontend/src/features/portal/` con `types.ts` vacío y una `IndexPage` que dice "Módulo en construcción") pero no tiene comportamiento. `docs/architecture/domains/portal.md` ya lo declara fuente canónica de la información del docente.

Dos restricciones del entorno condicionan el diseño:

1. **`/portal` es la landing del sistema.** `router.tsx` redirige `/` a `/portal` y `nav.ts` muestra "Mi Portal" en los seis perfiles de rol. Es la primera pantalla que ve un docente al ingresar por primera vez.
2. **El resto del sistema es transaccional.** El formulario de pedido de designación —la referencia visual existente— se completa una vez, se envía y entra en un circuito de aprobación. El Portal no funciona así, y copiar su patrón sería el error por defecto.

El alcance es la **cara del docente** como prototipo navegable con datos mockeados, siguiendo el patrón ya validado en `admin-docentes` y `pedidos-designacion`.

## Goals / Non-Goals

**Goals:**

- Pantalla "Mi Portal" con el perfil del docente autenticado en ocho secciones, con estados Loading / Empty / Error / Success.
- Un modelo de interacción coherente con lo que la pantalla es: un perfil que se vuelve a visitar, no un trámite que se envía.
- Que la pantalla se entienda sin texto explicativo, por su forma y sus etiquetas.
- Que el perfil vacío sea legible y corto, y que el avance sea visible sin instrumentos de progreso.
- Un solo patrón de sección reutilizado, en vez de ocho secciones a medida.
- Prototipo navegable sin decisiones de persistencia pendientes.

**Non-Goals:**

- Backend, persistencia real, endpoints HTTP o Contracts nuevos.
- Declaración y conciliación de horas, y cualquier lectura de datos de Designaciones.
- Vista de Secretaría sobre los docentes y búsqueda por habilidad.
- Onboarding paso a paso del primer ingreso (es otro objetivo).
- Workflow de aprobación o validación de títulos, certificaciones o CV: son informativos.
- Resolver si `/portal` sigue siendo la landing cuando exista un dashboard.

## Decisions

### D1 — Perfil vivo: lectura por defecto, edición por sección

Cada sección se muestra en modo lectura y se edita de forma independiente, con su propio guardado y su propia confirmación (`Toast`). **No hay un "Guardar" global al pie de la página.**

El motivo es que el Portal se visita muchas veces para tocar una sola cosa. Con un guardado global, el docente que entra a corregir su teléfono tiene que recorrer siete secciones para llegar al botón, y apretarlo se siente como enviar el perfil entero a algún lado —que es justamente lo que no pasa—.

**Alternativa descartada:** replicar el formulario de pedido (tarjeta única, secciones, guardado al pie, validación bloqueante al enviar). Es el patrón correcto para un trámite y el incorrecto para un perfil.

### D2 — Store mock local, sin backend

`frontend/src/features/portal/mock/mockStore.ts` es la única fuente de datos, igual que en `admin-docentes`. Las secciones editables mutan estado en memoria; el bloque Perfil lee un seed.

Consecuencia arquitectónica: **`Modules.Portal` queda hoja del DAG**. Al no manejar horas, el Portal no necesita leer nada de Designaciones, y el edge proyectado `Designaciones → Portal.Contracts` no se activa acá. No se agregan edges al grafo.

**Alternativa descartada:** cablear `IPortalQueries` y React Query contra API real — fuera de alcance y bloquearía el prototipo en decisiones de persistencia abiertas.

### D3 — Ownership por campo, una sola fuente por campo

El perfil es una entidad conceptual única, pero cada campo tiene **un** editor primario: el docente posee contacto, CV, experiencia, educación, certificaciones, proyectos, habilidades e intereses; Secretaría posee DNI, legajo y CUIL; Azure AD provee nombre, apellido y mail institucional.

En el mock esto se representa marcando el bloque Perfil como read-only y **no duplicando ningún campo** que ya viva en `features/docentes`.

**Nota de fricción cruzada:** `admin-docentes` hoy deja a Secretaría editar `telefono`. Cuando se retome ese change, ese lado debe pasar a read-only o a override explícito. Queda anotado como dependencia entre changes.

### D4 — Sin copy explicativo; la forma comunica

No hay textos de ayuda por sección, ni avisos del tipo "todavía no cargaste X", ni notas explicando por qué un campo no se edita.

- **Lo read-only se comunica por ausencia de afordancia**: si todas las secciones tienen su control de edición y Perfil no, se entiende sin una palabra.
- **Las etiquetas desambiguan solas**: "Mail institucional" en Perfil y "Mail" en Contacto alcanzan para saber cuál es cuál y por qué uno no se toca.

**Trade-off aceptado:** un docente con el CUIL mal cargado no encuentra en la pantalla a quién reclamarle. Se acepta a cambio de una interfaz limpia; si en la validación con el cliente resulta un problema real, se resuelve entonces.

### D5 — Sección vacía = una línea, no una tarjeta vacía

Una sección sin datos se renderiza como una fila con su nombre y un control de alta. Al cargarse el primer ítem, se expande a tarjeta con su contenido.

Dos consecuencias buscadas: el perfil vacío **entra en una sola pantalla** en vez de ser ocho tarjetas huecas, y **la página crece con el perfil**, de modo que el avance se ve sin barra de progreso ni porcentaje.

**Alternativa descartada:** un indicador de completitud. ¿Cuántas certificaciones son "completo"? Solo Contacto y CV tienen un final definible; un porcentaje sería precisión falsa.

**Alternativa descartada:** pestañas por sección. Reducen el scroll pero esconden exactamente lo que se necesita que se vea —los huecos—, y el problema del Departamento es que los docentes no cargan nada.

### D6 — El CV vacío es una dropzone

La sección CV sin archivo se presenta como zona de arrastre, no como una fila con "+". La forma explica la acción sin necesidad de una línea de texto, que es coherente con D4.

Es además la acción de mayor valor por esfuerzo del perfil: el docente ya tiene el PDF hecho, y con una sola acción el perfil deja de estar vacío. Las secciones estructuradas se completan después.

### D7 — El estado manda, no la visita

Todo el comportamiento depende del **estado de cada sección** (vacía o con datos), nunca de si es el primer ingreso del docente. No hay un modo "primera vez".

Esto mantiene la pantalla simple, evita estado de onboarding que habría que persistir, y hace que el Portal **no asuma ser la landing**: el día que exista un dashboard, se muda al menú sin cambios.

### D8 — Habilidades e intereses van separados

Dos listas de tags distintas, mismo widget y mismo vocabulario.

Ante una vacante son señales diferentes: quien tiene la habilidad puede tomarla ya; quien la tiene como interés la tomaría y se formaría. Son candidatos distintos, y una sola lista pierde la distinción para siempre.

**Alternativa descartada:** una lista con un nivel por tag (experto / interesado). Más rica, pero agrega fricción en cada alta; dos listas consiguen lo mismo sin preguntarle nada al docente.

### D9 — Un patrón de sección, reutilizado

Experiencia, Educación, Certificaciones y Proyectos son la misma cosa: una lista de ítems que se agregan, editan y borran. Se implementan sobre **un componente interno único** (encabezado con acción, filas, menú kebab ⋮ con Editar/Eliminar, estado vacío), no como cuatro secciones a medida.

- **Alta y edición en `Modal`**, siguiendo el precedente de `ModalNuevoDocente` / `ModalEditarDocente`. Son formularios chicos y discretos; no aplica la fatiga de modal que marca `design-principles.md`. Si un ítem crece, `Drawer` es la salida.
- **Borrado con confirmación** reutilizando el patrón de `ModalEliminarPeriodo` (qué se borra, aviso de que no se puede deshacer), sin justificativo: es información propia del docente.
- **Contacto es la excepción**: dos campos y es lo que más se edita, así que se edita inline dentro de la tarjeta.

### D10 — Adhesión estricta a la guía de estilos

- **Sin prototipo Pencil previo en este change** (decisión del equipo, 2026-09-02). El diseño se especifica en texto en el design spec y se implementa directo. La adhesión a la guía de estilos se mantiene por completo: lo que se saltea es la etapa de prototipado visual, no las restricciones de diseño.
- **Fuente única de componentes**: `@ars-docendi/ui` — `Breadcrumbs`, `PageHeader`, `DataList`, `Field`, `Input`, `Select`, `DatePicker`, `Textarea`, `FileUpload`, `Modal`, `InlineAlert`, `Toast`, `Button`. No se inventan componentes ni estilos.
- **Principios** de `docs/product/design-principles.md`: cohesión institucional, claridad sobre decoración, defaults accesibles, estados explícitos. Anti-patterns a evitar: lorem ipsum, stub interactions, iconos sin label en acciones críticas.
- **Tokens existentes** (`frontend/src/index.css` y el theme de la librería). Sin paletas ni escalas nuevas.
- **Design spec** en `docs/product/designs/portal-docente-design-spec.md` desde `_design-spec-template.md` (invariante #12).

### D11 — El widget de tags se compone a nivel feature

`@ars-docendi/ui` **no tiene** `Tag`, `Chip`, `Combobox`, `MultiSelect` ni autocomplete: el patrón de tags no se puede armar con lo que hay.

Se compone dentro de la feature siguiendo el precedente de `MateriasSelector` y `AsignacionesSelector` en `features/docentes` (`Select` del vocabulario + lista con "×"). No es inventar lenguaje visual, es componer el existente.

**Deuda registrada:** si más adelante Secretaría busca docentes por habilidad, va a necesitar el mismo widget y ahí conviene subirlo a `ui-lib`. Se anota en `docs/quality/tech-debt.md`.

### D12 — Adjuntos como metadata mock

El CV y los PDF de proyectos registran nombre y metadata en el store, sin storage real, igual que los adjuntos de `pedidos-designacion`. El hint deja claro que es mock; la persistencia real es una decisión de backend.

## Risks / Trade-offs

- **[Se le pide el CV dos veces: el PDF y lo mismo cargado a mano en Educación, Experiencia y Proyectos]** → Es la causa típica de que estos módulos queden vacíos. Se mitiga parcialmente con D6, que convierte el CV en la puerta de entrada en vez de un extra, pero la redundancia queda. Si en la validación con el cliente se confirma como freno, la salida es bajar cuánto se pide estructurado (OQ2).
- **[Sin copy explicativo, un dato institucional mal cargado deja al docente sin saber a quién reclamarle]** → Aceptado en D4. Es reversible con una línea de texto si aparece como problema real.
- **[El perfil vacío puede leerse como "el sistema no tiene nada mío"]** → Mitigado porque el bloque Perfil **nunca está vacío**: viene precargado de Azure AD y Secretaría, así que lo primero que ve el docente es su nombre y su legajo correctos.
- **[Solapamiento de `telefono` con `admin-docentes`]** → D3 fija el ownership y el campo no se duplica. Requiere que `admin-docentes`, hoy a mitad de implementación, ajuste su lado al retomarse.
- **[Fake UI, invariante #7]** → Todas las secciones deben operar sobre el store mock: agregar, editar y borrar reflejan estado. Sin botones inertes ni lorem ipsum.
- **[Los PDF de investigación pesan]** → No impacta el mock, pero es la primera necesidad real de storage del sistema y hay que tenerlo presente al diseñar el backend.

## Migration Plan

No aplica migración de datos ni deploy de backend: es un prototipo frontend con estado en memoria. Rollback = revertir el PR; no hay estado persistido ni migraciones que deshacer. La ruta `/portal` y la entrada de menú ya existen, así que no hay cambios en el shell.

## Open Questions

- **OQ1 — Vocabulario de habilidades e intereses: catálogo curado o texto libre.** El caso de uso (encontrar a quién contactar ante una vacante) pide algo consistente y buscable, lo que sugiere catálogo; pero cierra flexibilidad. El mock usa catálogo con opción de "sugerir nueva" para cubrir ambos mundos sin cerrar la decisión.
- **OQ2 — La doble carga del CV.** ¿El PDF es solo respaldo y lo estructurado es lo que vale, o se reduce cuánto se pide estructurado? A validar con el cliente viendo el prototipo.
- **OQ3 — Quién consume estos datos y cuándo** (concursos, categorización docente, informes a la Facultad). El uso define qué campos importan de verdad; hoy los campos son los razonables por defecto.
- **OQ4 — Qué ve en `/portal` alguien que no es docente**, por ejemplo un administrativo de Secretaría. La raíz redirige ahí para los seis perfiles de rol.
- **OQ5 — Hasta cuándo el Portal es la landing.** D7 deja la pantalla lista para mudarse al menú cuando exista un dashboard, pero la decisión no está tomada.
