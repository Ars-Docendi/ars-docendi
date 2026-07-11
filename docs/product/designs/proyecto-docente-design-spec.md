---
status: draft # draft | review | approved
owner: ""
feature: "openspec/changes/proyecto-docente-pedidos/specs/pedidos-designacion/spec.md"
last_updated: 2026-06-21
---

# Design spec: Proyecto docente — pedidos y flujo de aprobación (SCRUM-7 + SCRUM-8)

## Resumen

Se diseña la experiencia del **Jefe de Cátedra** para cargar y gestionar los pedidos de designación de su cátedra dentro del período abierto: una lista "Mis pedidos" y un formulario de alta/edición con secciones que cambian según la novedad (Sin novedad / Alta / Baja / Cambio de cargo o dedicación) — **SCRUM-7**. Y se diseña el **circuito de aprobación** (Coordinador → Secretaría → Decanato, con Administración como revisor sin aprobación): una **tabla de revisión** (única vista, sin Kanban desde el tema E del rediseño) y un detalle role-aware con cadena de aprobación e historial — **SCRUM-8**. Es un prototipo de alta fidelidad **frontend-only con datos mockeados**.

## Roles que ven esta surface

- [x] Jefe de Cátedra (carga + reenvío de devueltos)
- [x] Coordinador de Carrera (revisión de su carrera)
- [x] Secretaría Académica (revisión depto-wide)
- [x] Decanato (revisión depto-wide, etapa final)
- [x] Administrativos (revisión sin aprobación: rechaza/devuelve)
- [ ] Docente

## Flujo principal

1. El Jefe de Cátedra entra a **Mis pedidos** (`/designaciones/mis-pedidos`) desde el ítem de navegación. Ve los pedidos del período abierto, con la precarga de los docentes del período anterior como "Sin novedad".
2. Crea uno nuevo con **Nuevo pedido** (`/designaciones/pedidos/nuevo`) o **Editar** sobre un borrador / pedido devuelto.
3. En el **formulario** elige primero la **novedad**; según el tipo carga un docente nuevo (Alta) o selecciona un docente existente (Sin novedad / Baja / Cambio, con sus datos actuales en solo lectura), completa la designación solicitada / adjuntos / justificación que correspondan, y valida inline.
4. **Guardar pedido** persiste el borrador. Vuelve a Mis pedidos y el pedido aparece en estado `borrador`.
5. Desde Mis pedidos, **Enviar** pasa el borrador a `en_revision_coordinador` (inicio de la cadena de SCRUM-8). **Cancelar** (con confirmación) lo lleva a `cancelado`.
6. Tras enviar, el pedido queda de solo lectura para el JC (salvo que sea devuelto).

## Layout / IA

- **Mis pedidos**: `Breadcrumbs` + `PageHeader` (con acción "Nuevo pedido") + tabla. Cada fila: docente (nombre + DNI), materia(s) del pedido (resumen: una sola → su nombre; varias → "Primera +N"), novedad, `EstadoPedidoBadge` (+ badge "Prioritario"), y un **menú de acciones por fila (kebab ⋮)** con las acciones contextuales por estado (Editar / Enviar / Cancelar / Reenviar). Modal de confirmación para cancelar. Ver _Patrón transversal — menú de acciones por fila (kebab ⋮)_.
- **Formulario de pedido**: `Breadcrumbs` + encabezado propio (eyebrow `DESIGNACIONES · <NOVEDAD>` + título + subtítulo según novedad/edición) + **tarjeta** (máx. 860px) con secciones de eyebrow mono por novedad:
  - **Tipo de novedad** (radios horizontales) — siempre.
  - **Datos del docente**: en Alta son inputs nuevos (DNI + Apellido y Nombre); en Sin novedad / Baja / Cambio es un `Select` de docente existente + panel de **datos actuales** read-only (Antigüedad · Cargo actual · Dedicación actual, + Materia solo en Sin novedad — en Baja y Cambio la materia se lista aparte, ver abajo, para no duplicarla). **En Cambio, el panel se convierte en un resumen de cambios**: cada campo que difiere de lo solicitado se muestra como transición `actual → solicitado` (viejo en gris tenue, flecha, nuevo en negrita/verde acento — mismo lenguaje visual para cargo, dedicación, cada materia con sus horas, y horas de investigación/externas; sin cambios se ve el valor plano). Las materias se listan una debajo de la otra dentro del panel.
  - **Designación solicitada** (Alta/Cambio): cargo solicitado (`Select` libre entre todo el catálogo, sin restricción de jerarquía) + dedicación solicitada (**en Cambio, el `Select` solo ofrece dedicaciones jerárquicamente mejores que la actual** — la escala es descendente, Categoría 0 es la de mayor jerarquía y Categoría 6 la de menor; en Alta no hay restricción) + lista de materias y horas (agregar/quitar/seleccionar materia, editar horas; mínimo 1 fila obligatoria; en Cambio se precarga con las materias del docente) + horas de investigación / horas externas (otro depto., campos numéricos libres, sin cierre contra la dedicación).
  - **Materias del docente** (Baja): mismo listado de materias + horas que tiene el docente, pero íntegramente de solo lectura (sin `Select`, sin `Input`, sin agregar/quitar) — es contexto de qué queda vacante.
  - **Justificación** (Alta/Baja/Cambio): en Baja, `Select` "Tipo de baja" (Renuncia/Jubilación/Otro; "Otro" exige detalle en texto libre) antes de "Motivo de la baja"; en el resto, "Motivo del pedido" (`Textarea`).
  - **Documentación** : obligatoria en Alta (3 dropzones CV/DNI frente/dorso) y Baja (justificativo); opcional en Cambio (respaldo).
  - Al editar un pedido **devuelto** se muestra un `InlineAlert` de devolución (motivo + etapa de retorno) sobre la tarjeta.
  - Botonera (alineada a la derecha, con separador): Cancelar (secundario) + **Guardar pedido** (primario).
- Mockups de referencia en `docs/product/designs/screens.pen` (frames de pedido-form Alta / Baja / Editar·Cambio y Mis pedidos).

## Estados a diseñar

| Estado            | Descripción                                                                      | Cuándo se muestra                                  |
| ----------------- | -------------------------------------------------------------------------------- | -------------------------------------------------- |
| Loading           | "Cargando tus pedidos…" en la lista; "Cargando el pedido…" en edición            | Carga inicial / refetch de la query                |
| Empty             | `InlineAlert` informativo invitando a crear el primer pedido con "Nuevo pedido"  | El JC no tiene pedidos en el período abierto       |
| Error             | `InlineAlert` de error (carga de lista, carga de pedido, o fallo de una acción)  | Falla la query o la mutation                       |
| Success           | Tabla de pedidos / formulario operativo                                          | Estado normal con datos                            |
| Awaiting approval | El pedido enviado queda read-only para el JC; el badge muestra "En revisión · …" | Tras Enviar, hasta que el circuito (SCRUM-8) actúe |

## Decisiones de diseño

- **Pedido individual por docente** dentro de un contenedor "Mis pedidos" por período (decisión del grill §4).
- **Secciones condicionales por novedad**: el form solo muestra lo aplicable, evitando ruido (Alta/Cambio piden cargo+dedicación; Alta/Baja piden adjuntos; Cambio pide justificación).
- **Docente según novedad**: Alta carga un docente nuevo (inputs DNI + Apellido y Nombre); Sin novedad / Baja / Cambio seleccionan de un catálogo de docentes existentes y muestran su designación vigente (datos actuales) en solo lectura. Catálogos mock (docentes, materias, cargos, dedicaciones) en `api/catalogos.ts` — en el real provienen de la API Guaraní / módulo Portal.
- **Validación inline bloqueante**: el submit inválido no se envía; el error aparece en el campo (`Field error`) o como `InlineAlert` (adjuntos). Las reglas mapean a BR-001..004.
- **Acciones gated por estado** (invariante #7): Editar solo en borrador/devuelto-propietario; Enviar y Cancelar solo en borrador. Nada de botones muertos.
- **Adjuntos mock**: `FileUpload` registra solo el nombre del archivo (metadata); la persistencia real en File Storage es backend (`// TODO(backend)`).
- **Cancelar con confirmación**: por ser una acción terminal, pide confirmación en un `Modal`.

## Anti-patterns a evitar (específicos de esta feature)

- Mostrar el botón "Enviar"/"Editar" en pedidos que no lo admiten por su estado (rompe invariante #7 y confunde el flujo).
- Dejar que el form envíe datos inválidos al store (la validación debe bloquear antes del submit).
- Filtrar lógica de dominio (transiciones, guards) dentro de los componentes: vive en `maquinaEstados.ts` / `pedidoValidacion.ts`; los `// TODO(backend)` solo en `api/`.
- Simular adjuntos como si se subieran a un servidor: dejar claro (hint) que es metadata mock.

## Circuito de aprobación (SCRUM-8)

### Flujo del revisor

1. Un revisor (Coordinador / Secretaría / Decanato / Administración) entra a **Revisión** (`/designaciones/revision`) desde el ítem de navegación (visible solo para esos roles).
2. Ve una **tabla** con los pedidos de **su ámbito** [BR-009], ordenada por estado de avance (**En revisión** / Aceptados / Devueltos / Rechazados): el Coordinador solo los de su carrera; Secretaría/Decanato/Administración, todo el departamento.
3. Hace click en una **fila** → **detalle del pedido** (`/designaciones/pedidos/:id`), o vuelve a la tabla con el botón **Volver**, siempre visible en el detalle.
4. Si es el revisor de la etapa actual, actúa desde un **panel de acciones inline** en el rail derecho del detalle: **Aprobar** (avanza la cadena), **Rechazar** (terminal), **Devolver** (retrocede un nivel), **Marcar prioritario** o **Quitar prioritario** (según corresponda, nunca ambos a la vez) — con un único `Textarea` de justificativo compartido y la regla de comentario [BR-005] validada inline.
5. Al **Aceptar**, el pedido avanza: Coordinador → Secretaría → Decanato → **En lote** (terminal-prototipo). Administración nunca acepta [BR-015].
6. Al **Devolver**, el pedido vuelve al actor anterior (Jefe de Cátedra / Coordinador / Secretaría) como `devuelto`; el propietario lo corrige y **reenvía**, retomando la etapa que lo devolvió [BR-014].

### Layout / IA — Tabla de revisión (única vista, tema E)

> **Tema E del rediseño** (`openspec/changes/rediseno-revision-solo-grilla/`) eliminó el Tablero Kanban y el switcher de vistas: la Tabla es la **única** superficie de `/designaciones/revision`. Las exploraciones Kanban (`q6OrQB`, `kWSjh`, `Z0S9T`) se retiraron de `screens.pen`; se conservan en el historial de git si hace falta consultarlas.

- Modelo de agrupación: **opción D**. La tabla ordena por **estado de avance del pedido**, no por rol — separando dos ejes que antes se confundían: _en qué etapa está_ (columna `Estado`) y _si es mi turno_ (filtro "Mis pendientes").
- `Breadcrumbs` + `PageHeader` ("Tablero de revisión de pedidos", subtítulo "Pedidos en tu ámbito · {rol} · {ámbito}") + barra de filtros ("Mis pendientes" / "Tipo" / "Prioritario", sin switcher de vista) + **tabla plana ordenada por estado**: **En revisión → Aceptados → Devueltos → Rechazados** —; el estado de cada fila lo comunica la columna `Estado`, no un encabezado de grupo.
- **Columnas de la tabla**: `Docente` (avatar de iniciales + nombre) · `Asignatura` (filler) · `Novedad` (chip Alta / Baja / Cambio) · **`Estado`** (columna **combinada** estado + avance, en una línea: en revisión → mini-stepper accent + "En {etapa} · x/4"; aceptado → stepper completo verde + "Aceptado"; devuelto / rechazado → punto de color + etiqueta del estado) · `Prioritario` (columna **solo-ícono**: bandera roja si es prioritario, vacía en el resto).
- **Gating por ámbito** [BR-009]: el Coordinador ve solo su carrera; Secretaría / Decanato / Administración, todo el departamento. `puedeRevisar(pedido, actor)` decide para qué pedidos el actor está **en turno** (resalte + botones de acción), no qué pedidos se ven: la tabla muestra el avance completo de su ámbito y "Mis pendientes" filtra a tu turno.
- Mockup: frame `ebl4U` ("Designaciones · Revisión de pedidos (Tabla)") en `screens.pen`, tabla plana con columna `Estado` que fusiona estado + avance (mini-stepper) y columna Prioritario solo-ícono.
- **Implementación (frontend real)**: la vista Tabla (`TablaRevision` + `EstadoAvance`) reusa el modelo de agrupación (`construirColumnas`, `avancePedido`, …, en `tableroRevisionModelo.ts`). El default del filtro de turno se mantiene en **"Vista completa"**: se evaluó "Mis pendientes" pero deja la tabla casi vacía, porque los estados terminales no son "tu turno". El **motivo de un pedido rechazado** ya no se muestra en la tabla (no hay card); se mudó al detalle (`ResumenPedido`, ver más abajo). Changes OpenSpec: `tablero-revision-vista-tabla`, `rediseno-revision-solo-grilla`.

### Layout / IA — Detalle del pedido (role-aware)

Replica el frame `hcCfk` ("Revisión de novedad") de `screens.pen`: header rico + stepper horizontal + dos columnas (sin tabs). Se vincula a datos reales del `PedidoDesignacion`; los campos del mockup sin fuente de datos (legajo, email, expediente, integridad, carácter) se **omiten**, no se inventan (invariante #7).

- `Breadcrumbs` + un botón **Volver** persistente (junto a los breadcrumbs, siempre visible para cualquier rol — navega a `/designaciones/revision`, tema E) + `PageHeader`: eyebrow (`Designaciones · Pedido {ref}`), título `{novedad} — {materia}`, meta `Cátedra {catedra} · {carrera}`, y `EstadoPedidoBadge` (estado + Prioritario) a la derecha, con **peso visual reforzado** (tamaño/contraste, tema E) para que no se pierda en la fila del título — misma posición de siempre.
- **Stepper horizontal de la cadena** (`CadenaRevision`): **5 etapas** — Jefe de Cátedra → Coordinador → Secretaría → Decanato → En lote. Cada etapa: marca (check `done` / punto `current` / número `pending` / x `rejected`), rol (mono uppercase) y línea de detalle ("Envió/Reenvió · fecha", "En revisión · vos", "Pendiente", "Aprobó · fecha", "Rechazó · fecha", "Devuelto para corrección"); los conectores se pintan en accent cuando la etapa previa está cumplida. Se deriva del estado + historial con `derivarCadena(pedido, actor)`.
- **Columna izquierda** (`adoc-det-main`):
  - `ResumenPedido` — tarjeta "Datos del pedido": cabecera del docente (avatar, nombre, chip de cargo actual, `DNI · antigüedad`) + chip de novedad; grilla de datos (cátedra / carrera / materia, cargo y dedicación con transición `actual → solicitado`, horas de investigación con tag Portal); si el pedido está **`rechazado`**, el **motivo de rechazo destacado** en cita (borde izquierdo danger, tema E — se mudó acá desde la card del Kanban ya eliminado); el **justificativo del Jefe de Cátedra** en cita (borde izquierdo accent); y la documentación adjunta si existe.
  - **Historial del pedido**: título + nota "Auditoría · usuario · fecha (RNF-7)" + `AuditLog` (cada evento: actor, verbo, fecha, comentario).
- **Rail derecho** (`adoc-det-rail`, 380px):
  - `PanelAccionesRevision` (solo para el revisor de la etapa en su ámbito): **Aprobar y pasar a {siguiente}** (primary; oculto para Administración [BR-015]); un único `Textarea` de **Justificativo** compartido (entrada rápida); fila **Rechazar** (destructive) + **Devolver** (secondary); **Marcar prioritario** (ghost) cuando el pedido no es prioritario, o **Quitar prioritario** (ghost, sin justificativo obligatorio) cuando ya lo es — nunca ambos a la vez (tema E); y un _scope hint_ ("Revisás como {rol} de {ámbito}…"). Los botones **no mutan directo**: abren un **modal de confirmación** por acción (ver abajo). El dominio sigue siendo la autoridad (la mutation revalida los guards).
  - `DatosTramite` — meta-panel "Datos del trámite": Etapa, Ámbito, Prioritario, y la fecha de envío/creación.
- El JC y los demás roles ven el detalle **de solo lectura** (sin el panel de acciones) + el stepper, los datos y el historial, con el botón **Volver** igual de visible.

### Modal de confirmación de acciones de revisión

Cada acción del panel (Aceptar / Rechazar / Devolver / Priorizar / Despriorizar) abre un **modal de confirmación propio** antes de mutar (`ModalConfirmacionAccion`, reusa `Modal` de `@ars-docendi/ui`). Matchea 1:1 los frames `modalAprobar` / `modalRechazar` / `modalDevolver` / `modalPriorizar` de `screens.pen` (el de **Devolver** se diseñó en esta iteración; el de **Quitar prioridad** se agregó en el tema E, reusando el mismo patrón sin frame propio en el mockup).

- **Header**: ícono en círculo con tono por acción (accent para Aceptar y Quitar prioritario, danger para Rechazar, warning para Devolver/Priorizar) + título + subtítulo de etapa/efecto (p. ej. "Etapa Coordinador · pasa a Secretaría", "Termina el trámite · estado Rechazado", "Vuelve al Jefe de Cátedra · estado Devuelto", "Cualquier actor · sin justificativo" para Quitar prioridad). El subtítulo y el aviso de Aceptar/Devolver se derivan de la **etapa actual** del pedido.
- **Caja de aviso**: describe el efecto y a quién se notifica — **info accent** para Aceptar (a dónde avanza) y Quitar prioritario (deja de figurar como prioritario), **warning** para Rechazar (terminal, se genera pedido nuevo) y Devolver (vuelve a Borrador). Priorizar no lleva caja.
- **Comentario/justificativo editable**: el textarea del modal viene **pre-cargado** con lo tipeado en el panel inline y es editable; lo confirmado en el modal es lo que se envía (carry-over de un solo sentido: Cancelar descarta la edición del modal).
- **Validación dentro del modal**: el justificativo **obligatorio** en Rechazar/Devolver [BR-005] y Priorizar [BR-017] **bloquea el botón de confirmar** (deshabilitado) mientras esté vacío, con el indicador "· obligatorio". Aceptar y **Quitar prioritario** (tema E) permiten confirmar con comentario vacío. Reemplaza la validación inline anterior.
- **Footer**: **Cancelar** (cierra sin efecto) + **Confirmar** con label e ícono por acción ("Aprobar y enviar" ✓, "Rechazar novedad", "Devolver a Borrador", "Guardar prioridad", "Quitar prioridad").

### Gotcha de mapeo (lib en inglés ↔ dominio en español)

`@ars-docendi/ui` usa enums en inglés (`AuditVerb`: create/update/attach/approve/return/reject) — son símbolos de la lib (invariante #13, excepción de framework). La `accion` del historial va en español y se mapea español→`AuditVerb` (con etiqueta legible en español) al alimentar `AuditLog`. La cadena de aprobación, en cambio, se deriva a un tipo **propio en español** (`EtapaCadena` / `EstadoEtapaCadena`: cumplida / actual / pendiente / devuelta / rechazada) que consume el stepper in-app `CadenaRevision` (ya no se usa el `ApprovalTimeline` de la lib). Esos adapters (`detalleAdapters.ts` → `derivarCadena`) son funciones puras **de presentación**, no de dominio.

### Role-switching (recorrer la cadena en la demo)

El usuario **"Demo (todos los roles)"** usa el `RoleMenu` existente del TopBar para cambiar de rol sin re-loguear. El rol activo se persiste en la sesión mock y `useCurrentUser` lo observa de forma reactiva, así el `ActorContexto` que consume la capa `api/` cambia de forma coherente y el mismo pedido es visible/accionable en la etapa que corresponde a cada rol.

### Estados a diseñar — superficies de revisión

| Estado  | Tabla de revisión                                                                      | Detalle del pedido                                                                                                    |
| ------- | -------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------- |
| Loading | "Cargando los pedidos de tu ámbito…"                                                   | "Cargando el pedido…"                                                                                                 |
| Empty   | `InlineAlert` "No hay pedidos para revisar"                                            | (sin estado vacío propio; usa Error si el id no existe)                                                               |
| Error   | `InlineAlert` de error de carga                                                        | `InlineAlert` "No se encontró el pedido" / "fuera de tu ámbito"                                                       |
| Success | Tabla ordenada por estado (En revisión con `x/4` · Aceptados · Devueltos · Rechazados) | Header (badge reforzado) + botón Volver + stepper de cadena + 2 columnas (datos + historial / panel inline + trámite) |

### Decisiones de diseño — SCRUM-8

- **Sin drag**: el avance es una acción con regla (comentario obligatorio en rechazo/devolución), no un movimiento libre; un drag implicaría transiciones sin justificativo. Esta decisión sigue vigente aunque el Kanban se haya retirado (tema E) — la Tabla tampoco admite reordenar filas para cambiar el estado.
- **Autoridad en el dominio, affordance en la UI**: los botones se muestran con predicados derivados de la máquina de estados (`puedeRevisar` / `puedeAceptar`); la autoridad real la imponen los guards (etapa [BR-013] + ámbito [BR-009] + Administración-no-aprueba [BR-015]).
- **Reenvío del JC desde "Mis pedidos"**: un pedido `devuelto` al Jefe de Cátedra ofrece "Reenviar" en la tabla de Mis pedidos (además de Editar), cerrando el lazo de corrección.
- **Tabla por estado de avance + "tu turno" explícito (opción D)**: en vez de agrupar por rol (cuyo rótulo "etapa siguiente" mezclaba _estado real_ y _de quién es el turno_), la Tabla ordena por **estado de avance**, y cada fila en revisión declara su etapa con un indicador **`x/4`**. Separa el eje _estado_ (columna `Estado`) del eje _mi turno_ (filtro "Mis pendientes"). Suma un grupo **Rechazados** (terminal) para cerrar la trazabilidad del período.
- **Una sola vista, sin switcher (tema E)**: el Tablero Kanban se eliminó — la Tabla ya cubre toda la información que necesita el revisor (docente, asignatura, novedad, estado + avance, prioridad), y mantener dos vistas duplicaba superficie sin agregar información nueva. Ver `openspec/changes/rediseno-revision-solo-grilla/`.
- **Quitar prioritario sin justificativo (tema E)**: bajar la urgencia de un pedido es una acción de menor riesgo que subirla (no hay nada que justificar ante otros revisores); pedirlo agregaría fricción sin beneficio. Mismo guard de ámbito que Marcar prioritario — ningún actor fuera de su ámbito puede despriorizar. Queda **fuera de alcance** la jerarquía de cargos para restringir quién puede despriorizar a quién (tema C).
- **Confirmación de acciones vía modal**: las acciones de alto impacto (sobre todo Rechazar, terminal, y Devolver) requieren un paso de confirmación explícito que comunica el efecto y a quién se notifica, y mueve la validación del justificativo obligatorio al modal. El panel inline queda como disparador + entrada rápida del comentario; la confirmación es el punto único de envío (ver "Modal de confirmación de acciones de revisión").

## Patrón transversal — menú de acciones por fila (kebab ⋮)

En las tablas con acciones por fila del módulo Designaciones, las acciones se agrupan en un **menú kebab** (botón ⋮ `ellipsis-vertical` al final de la columna ACCIONES) que abre un **menú contextual** (popover), en vez de iconos sueltos inline. Unifica la columna, escala cuando hay más de una acción y deja la fila limpia.

- **Alcance actual**: _Mis pedidos_ (acciones por estado del Jefe de Cátedra), _Períodos de designación_ (config · Secretaría) y _Administración de Usuarios_ (admin · Secretaría).
- **Contenido = solo acciones reales y habilitadas** (invariante #7): el menú lista únicamente lo que el estado / rol permite — no se inventan acciones para "llenar" el menú (p. ej. no se agregó "Duplicar" si no es una capacidad real). En el mockup: Períodos → **Editar** + **Eliminar**; Usuarios → **Editar** + **Activar/Desactivar**.
- **Acción destructiva diferenciada**: la opción destructiva (Eliminar) se pinta con color `danger` (texto + icono `trash-2` en rojo); las demás en neutro con icono de apoyo (`square-pen` Editar, `ban` Desactivar).
- **Estados del control**: ⋮ en reposo por fila; al abrir, el disparador queda en estado activo (fondo + borde) y el popover flota sobre las filas siguientes (card blanca con borde, esquinas redondeadas y sombra).
- Mockups de referencia: columna ACCIONES con el menú abierto en la fila 1 de los frames `ICs06` (Períodos) y `A9tdD` (Administración de Usuarios) en `screens.pen`.

## Referencias

- [`docs/product/design-principles.md`](../design-principles.md)
- Plan maestro: [`docs/product/designs/proyecto-docente-frontend-plan.md`](./proyecto-docente-frontend-plan.md)
- Spec funcional (SCRUM-7): [`openspec/changes/proyecto-docente-pedidos/specs/pedidos-designacion/spec.md`](../../../openspec/changes/proyecto-docente-pedidos/specs/pedidos-designacion/spec.md)
- Spec funcional (SCRUM-8): [`openspec/changes/flujo-aprobacion-designaciones/specs/aprobacion-pedidos-designacion/spec.md`](../../../openspec/changes/flujo-aprobacion-designaciones/specs/aprobacion-pedidos-designacion/spec.md)
- Modal de confirmación de acciones: [`openspec/changes/modal-confirmacion-acciones-revision/`](../../../openspec/changes/modal-confirmacion-acciones-revision/)
- Business rules: [`docs/business-rules/designaciones.md`](../../business-rules/designaciones.md)

## Open questions de diseño

- Nomenclatura definitiva del estado post-Decanato (`En lote` vs `Aprobado`) — afecta el badge a partir de SCRUM-8.
- ¿La precarga del período anterior debe traer también los adjuntos previos o solo los datos del docente? (a confirmar con el cliente).
