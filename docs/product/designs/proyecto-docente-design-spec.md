---
status: draft # draft | review | approved
owner: ""
feature: "openspec/specs/pedidos-designacion/spec.md"
last_updated: 2026-09-09
---

# Design spec: Proyecto docente — pedidos y flujo de aprobación (SCRUM-7 + SCRUM-8)

## Resumen

Se diseña la experiencia del **Jefe de Cátedra** para cargar y gestionar los pedidos de designación de su cátedra dentro del período abierto: una lista "Mis pedidos" y un formulario de alta/edición con secciones que cambian según la novedad (Alta / Baja / Cambio de cargo o dedicación) — **SCRUM-7**. La continuidad se obtiene de las designaciones vigentes, sin crear pedidos artificiales. También se diseña el **circuito de aprobación** (Coordinador → Secretaría → Decanato, con Administración como revisor sin aprobación): una **tabla de revisión** y un detalle con cadena de aprobación e historial — **SCRUM-8**. La interfaz consume la API de Designaciones; el backend es autoridad de permisos y reglas.

## Roles que ven esta surface

- [x] Jefe de Cátedra (carga + corrección de devueltos)
- [x] Coordinador de Carrera (revisión y corrección de devueltos de su carrera)
- [x] Secretaría Académica (revisión y corrección de devueltos depto-wide)
- [x] Decanato (revisión depto-wide, etapa final)
- [x] Administrativos (revisión sin aprobación: rechaza/devuelve)
- [ ] Docente

## Flujo principal

1. El Jefe de Cátedra entra a **Mis pedidos** (`/designaciones/mis-pedidos`) desde el ítem de navegación. Ve los pedidos del período abierto y las designaciones vigentes como contexto de continuidad; la continuidad no crea trámites. Es una lista realista: normalmente pocos pedidos — el JC recién empieza a cargarlos de a uno (`mis-pedidos-simplificado`).
2. Crea uno nuevo con **Nuevo pedido** (`/designaciones/pedidos/nuevo`); click en cualquier fila (o el botón **"Ver"**) abre el **detalle** de ese pedido, y el botón **"Editar"** (visible solo si la API lo permite) abre el formulario sobre un borrador o pedido devuelto. Un pedido en **borrador** también puede **eliminarse** con confirmación; un `devuelto` no.
3. En el **formulario** elige primero la **novedad**; en Alta ingresa DNI, nombre y apellido de la persona nueva, y en Baja/Cambio selecciona un docente existente. Luego la materia se resuelve en contexto: Alta muestra las materias activas del Jefe de Cátedra; Baja/Cambio muestra sólo la intersección con las designaciones vigentes del docente. Una opción se selecciona automáticamente y varias se eligen explícitamente por materia.
4. **Guardar pedido** persiste datos válidos sin enviarlos. **Guardar y enviar** (o **Guardar y reenviar**, si el pedido estaba `devuelto`) guarda y, en el mismo paso, envía a revisión. El frontend adelanta las mismas validaciones que el backend.
5. Tras enviar, el pedido queda de solo lectura para el JC (salvo que sea devuelto).

## Layout / IA

- **Jerarquía de rutas**: no existe una pantalla índice en `/designaciones`; las superficies accesibles son sus rutas hijas y el breadcrumb padre se muestra sin enlace.
- **Mis pedidos** (`mis-pedidos-simplificado`): `Breadcrumbs` + `PageHeader` (acción "Nuevo pedido", deshabilitada sin período abierto) + tabla nativa del design system. Cada encabezado de datos — **N°**, **Docente**, **Legajo**, **Cátedra**, **Tipo**, **Enviado** y **Estado**— ofrece un filtro compacto; los textos buscan sin distinguir mayúsculas ni tildes, las opciones múltiples usan OR y las columnas distintas AND. Esos filtros se aplican antes del ordenamiento y de la paginación. Los encabezados de datos también alternan orden ascendente, descendente y sin orden manual; **Acciones** no ofrece filtro ni orden. Cada fila: N°, docente (sin prefijo "Prof."), **legajo** (o "—" si el docente todavía no tiene uno, p. ej. una Alta recién cargada), cátedra, **Tipo** (la novedad — antes "Novedad"), fecha de envío, `EstadoPedidoPill`. La fila entera es **clickeable** y navega al detalle del pedido. Al final de la fila: botón **"Ver"** (siempre visible, mismo destino que el click en la fila) y botón **"Editar"** (solo si la API incluye la acción `editar`) — ambos con el **mismo formato que las acciones de fila de Usuarios** (`Button variant="ghost" size="sm"`, sin ícono); y, únicamente cuando la API incluye `eliminar`, un control **"Eliminar"** (X roja) que abre `ModalEliminarPedido` antes de borrar. Ninguna de estas acciones dispara la navegación al detalle (`stopPropagation`). Sin menú kebab, sin acciones de enviar/cancelar en la lista — "Enviar"/"Reenviar" se mudaron al formulario (ver más abajo) y "Cancelar" (pasar a `cancelado`) no tiene reemplazo en esta iteración (Eliminar cubre el caso de un borrador no deseado, ver Decisiones de diseño).
- **Períodos de designación**: `PageHeader` + tabla nativa. **Nombre**, **Carga desde**, **Carga hasta**, **Impacto desde**, **Impacto hasta** y **Activo** ofrecen filtro por encabezado; Nombre y fechas buscan por texto normalizado y Activo permite seleccionar Activo/Inactivo, individualmente o ambos. Las mismas columnas alternan orden ascendente, descendente y sin orden manual; sin orden manual se conserva el orden predeterminado por Impacto desde descendente. **Acciones** queda sin filtro ni orden.
- **Formulario de pedido**: `Breadcrumbs` + encabezado propio (eyebrow `DESIGNACIONES · <NOVEDAD>` + título + subtítulo según novedad/edición) + **tarjeta** (máx. 860px) con secciones de eyebrow mono por novedad:
  - **Tipo de novedad** (radios horizontales) — siempre.
  - **Datos del docente**: en Alta son inputs nuevos separados (DNI, Nombre y Apellido); en Baja / Cambio es un `Select` de docente existente y, después, la materia contextual compatible. El panel de **datos actuales** es read-only (Antigüedad · Cargo actual · Dedicación actual). **En Cambio, el panel se convierte en un resumen de cambios**: cada campo que difiere de lo solicitado se muestra como transición `actual → solicitado` (viejo en gris tenue, flecha, nuevo en negrita/verde acento — mismo lenguaje visual para cargo, dedicación, materia con sus horas, y horas de investigación/externas; sin cambios se ve el valor plano).
  - **Materia contextual**: Alta usa las materias activas del ámbito del Jefe de Cátedra; Baja/Cambio intersectan ese catálogo con las materias de la persona seleccionada. Con cero opciones se muestra un error inline y no se puede guardar; con una se muestra como solo lectura; con varias aparece un único selector contextual.
  - **Designación solicitada** (Alta/Cambio): cargo solicitado (`Select` libre entre todo el catálogo) + dedicación solicitada (`Select` libre entre Categorías 1–6 activas) + la materia elegida en solo lectura con sus horas editables + horas de investigación / horas externas (otro depto., campos numéricos libres, sin cierre contra la dedicación).
  - **Materia del docente** (Baja): la materia elegida y las horas del pedido, íntegramente de solo lectura — es contexto de qué queda vacante.
  - **Justificación** (Alta/Baja/Cambio): en Baja, `Select` "Tipo de baja" (Renuncia/Jubilación/Otro; "Otro" exige detalle en texto libre) antes de "Motivo de la baja"; en el resto, "Motivo del pedido" (`Textarea`).
  - **Documentación** : obligatoria en Alta (3 dropzones CV/DNI frente/dorso) y Baja (justificativo); opcional en Cambio (respaldo).
  - Al editar un pedido **devuelto** se muestra un `InlineAlert` de devolución (motivo + etapa de retorno) sobre la tarjeta.
  - Botonera (alineada a la derecha, con separador): Cancelar (secundario, descarta la edición sin guardar) + **Guardar pedido** (secundario) + **Guardar y enviar** / **Guardar y reenviar** si el pedido está `devuelto` (primario). Las dos acciones de guardado validan adjuntos, justificación, tipo de baja y legajo antes de llamar a la API.
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
- **Docente según novedad**: Alta carga un docente nuevo (DNI, Nombre y Apellido) y no crea una cuenta automáticamente; Baja / Cambio seleccionan del catálogo servido por la API y luego una materia compatible por su UUID. La continuidad se consulta desde las designaciones vigentes.
- **Materia contextual, una por pedido**: la UI nunca traduce nombres para identificar una materia. La materia única se fija automáticamente, varias requieren selección y cero bloquea el guardado; el backend vuelve a validar ámbito y vigencia.
- **Validación inline bloqueante al guardar o enviar**: los datos inválidos no se mandan; el error aparece en el campo (`Field error`) o como `InlineAlert` (adjuntos). Las reglas mapean a BR-001..004 y BR-018, y el backend vuelve a validarlas como autoridad.
- **Acciones gated por estado** (invariante #7): Editar solo en borrador/devuelto-propietario; Enviar y Cancelar solo en borrador; **Eliminar solo en borrador** (`mis-pedidos-simplificado`) — a propósito más angosto que Editar: un `devuelto` ya tiene una revisión asociada en su historial, eliminarlo la borraría sin dejar rastro. Nada de botones muertos.
- **Adjuntos**: `FileUpload` registra solo el nombre del archivo; integrar almacenamiento del binario queda fuera del alcance actual.
- **Eliminar con confirmación, sin justificativo** (`mis-pedidos-simplificado`): `ModalEliminarPedido` reutiliza el patrón de `ModalEliminarPeriodo` (título, texto con el nombre del docente, aviso "no se puede deshacer", Cancelar/Eliminar) — no el de `ModalConfirmacionAccion` (pensado para acciones de revisión con justificativo y aviso de a quién se notifica, que no aplica: eliminar un borrador propio no notifica a nadie).
- **Legajo es opcional en el pedido, obligatorio en el catálogo de docentes existentes** (`mis-pedidos-simplificado`): un docente de **Alta** puede no tener legajo todavía (lo asigna el sistema/RRHH después de cargado) — la tabla muestra "—" en ese caso; un docente ya existente (Baja/Cambio) siempre lo tiene, viene del catálogo (`DOCENTES_EXISTENTES`). No se agregó un input de Legajo al form: el cliente pidió filtrarlo y verlo en la tabla, no capturarlo ahí.
- **Legajo obligatorio para guardar una Baja o un Cambio [BR-designaciones-018]** (`mis-pedidos-simplificado`): ambas novedades operan sobre un docente **ya existente** en el sistema, que por eso ya tiene legajo asignado — sin legajo, la validación bloquea el guardado con el mismo error de campo que falta DNI/nombre (`errores.docente`). Alta queda exceptuada (docente nuevo, todavía sin legajo).

## Anti-patterns a evitar (específicos de esta feature)

- Mostrar el botón "Enviar"/"Editar" en pedidos que no lo admiten por su estado (rompe invariante #7 y confunde el flujo).
- Dejar que el form envíe datos inválidos a la API (la validación cliente adelanta la respuesta, y el backend vuelve a validarla).
- Filtrar lógica de dominio (transiciones, guards) dentro de los componentes: las acciones permitidas vienen de la API y la validación anticipada vive en `pedidoValidacion.ts`; el backend conserva la autoridad.
- Simular que el binario se subió a un servidor: dejar claro que hoy se guarda sólo metadata.

## Circuito de aprobación (SCRUM-8)

### Flujo del revisor

1. Un revisor (Coordinador / Secretaría / Decanato / Administración) entra a **Revisión** (`/designaciones/revision`) desde el ítem de navegación (visible solo para esos roles).
2. Ve una **tabla única** con pestañas **Todos · En Cátedra · En Coordinación · En Secretaría · En Decanato · Finalizados** y los pedidos de **su ámbito** [BR-009]. El Coordinador ve sólo su carrera; Secretaría/Decanato/Administración ven todo el departamento. Los filtros se aplican antes de calcular los contadores.
3. Hace click en una **fila** → **detalle del pedido** (`/designaciones/pedidos/:id`), o vuelve a la pantalla de la que vino con el botón **Volver**, siempre visible en el detalle (`navigate(-1)` — el detalle también se llega desde "Mis pedidos" del JC, `mis-pedidos-simplificado`, no solo desde esta Tabla).
4. Si es el revisor de la etapa actual, actúa desde el panel de acciones del detalle: **Aprobar** (avanza la cadena), **Rechazar** (terminal), **Devolver** (retrocede un nivel), **Marcar prioritario** o **Quitar prioritario** (según corresponda, nunca ambos a la vez). El justificativo se confirma en el modal de cada acción.
5. Al **Aceptar**, el pedido avanza: Coordinador → Secretaría → Decanato → **En lote** (terminal-prototipo). Administración nunca acepta [BR-015].
6. Al **Devolver**, el pedido vuelve al actor anterior (Jefe de Cátedra / Coordinador / Secretaría) como `devuelto`; el propietario lo corrige y **reenvía**, retomando la etapa que lo devolvió [BR-014].

### Layout / IA — Tabla de revisión (única vista, tema E)

> El rediseño consolidado en `openspec/specs/tablero-revision-tabla/spec.md` eliminó el Tablero Kanban y el switcher de vistas: la Tabla es la **única** superficie de `/designaciones/revision`. Las exploraciones Kanban se conservan en el historial de Git.

- La tabla usa una sola superficie con pestañas: **Todos · En Cátedra · En Coordinación · En Secretaría · En Decanato · Finalizados**. Un pedido **Devuelto** vive en el área de `propietarioActual`, mientras su etapa de retorno se conserva para el reenvío. Secretaría Académica, Administrativo y Decanato ven todo el departamento; el Coordinador ve solo su carrera [BR-009].
- Orden dentro de cada sección de etapa: **prioritarios primero**, después **devueltos**, después el resto por fecha de última actualización **ascendente** (el que espera hace más tiempo, arriba). Orden dentro de Finalizados: **Aceptados antes que Rechazados**; dentro de cada bloque, por fecha **descendente** (el cierre más reciente arriba).
- La pestaña inicial corresponde al área del rol del actor; Administración abre en Todos. Los filtros de nombre, período, novedad, Estado y demás se aplican antes de calcular los contadores.
- Las pestañas son controles del design system, con una sola tabla debajo. El foco visible y el subrayado activo se conservan al pasar por hover.
- La fila de pestañas conserva subrayado activo y foco visible durante hover. En Finalizados, los roles Secretaría, Decanato y Administración ven Exportar junto con el período configurado; el control muestra progreso, errores y la ausencia de período activo.
- `Breadcrumbs` + `PageHeader` ("Tablero de revisión de pedidos", subtítulo "Pedidos en tu ámbito · {rol} · {ámbito}") + filtros generales en `FiltrosLista`: se conservan **Período**, **Prioridad**, **Sin movimiento** y, cuando corresponde, **Carrera**. **Docente**, **Legajo**, **Tipo**, **Inicio**, **Últ. actualización** y **Estado** pasan a los encabezados de la tabla; **Área** aparece como filtro sólo en **Todos**. Todos los criterios se aplican antes de calcular filas y contadores, sin controles duplicados.
- **Columnas de la tabla**: `Docente` (avatar de iniciales + nombre, sin prefijo) · `Legajo` (o "—") · `Tipo` (chip Alta / Baja / Cambio) · `Inicio` · `Últ. actualización` · `Estado` · `Área` (sólo en Todos) · `Acciones`. Estado se filtra desde el filtro opcional y el orden se puede cambiar desde los encabezados ordenables.
- **Gating por ámbito** [BR-009]: el Coordinador ve solo su carrera; Secretaría / Decanato / Administración, todo el departamento. `puedeRevisar(pedido, actor)` decide para qué pedidos el actor está **en turno** (resalte + botones de acción), no qué pedidos se ven: dentro de cada sección se ve el ámbito completo del actor y "Mis pendientes" filtra a tu turno.
- Mockup: frame `ebl4U` ("Designaciones · Revisión de pedidos (Tabla)") en `screens.pen` — todavía refleja la tabla plana original (sin secciones), no sincronizado con la agrupación por etapa; queda como deuda de mockup.
- **Implementación (frontend real)**: `TablaRevision` reusa el modelo de pestañas, filtros, orden y estados de `tableroRevisionModelo.ts`. El **motivo de un pedido rechazado** se muestra en el detalle (`ResumenPedido`). El botón Exportar se limita a Finalizados y a los roles departamentales habilitados.

### Layout / IA — Detalle del pedido (role-aware)

Replica el frame `hcCfk` ("Revisión de novedad") de `screens.pen`: header rico + stepper horizontal + dos columnas (sin tabs). Se vincula a datos reales del `PedidoDesignacion`; los campos del mockup sin fuente de datos (legajo, email, expediente, integridad, carácter) se **omiten**, no se inventan (invariante #7).

- `Breadcrumbs` + un botón **Volver** persistente. El propietario actual de un `devuelto` (Jefe, Coordinador o Secretaría), y el Jefe propietario de un `borrador`, ven **Editar** cuando la API incluye esa acción. Sólo el borrador ofrece además **Eliminar**. Sigue el `PageHeader`: eyebrow (`Designaciones · Pedido {ref}`), título `{novedad} — {materia}`, meta `Cátedra {catedra} · {carrera}`, y `EstadoPedidoBadge` a la derecha.
- **Stepper horizontal de la cadena** (`CadenaRevision`): **5 etapas** — Jefe de Cátedra → Coordinador → Secretaría → Decanato → En lote. Cada etapa: marca (check `done` / punto `current` / número `pending` / x `rejected`), rol (mono uppercase) y línea de detalle ("Envió/Reenvió · fecha", "En revisión · vos", "Pendiente", "Aprobó · fecha", "Rechazó · fecha", "Devuelto para corrección"); los conectores se pintan en accent cuando la etapa previa está cumplida. Se deriva del estado + historial con `derivarCadena(pedido, actor)`.
- **Columna izquierda** (`adoc-det-main`):
  - `ResumenPedido` — tarjeta "Datos del pedido": cabecera del docente (avatar, nombre, chip de cargo actual, `DNI · antigüedad`) + chip de novedad; grilla de datos (cátedra / carrera / materia, cargo y dedicación con transición `actual → solicitado`, horas de investigación con tag Portal); si el pedido está **`rechazado`**, el **motivo de rechazo destacado** en cita (borde izquierdo danger, tema E — se mudó acá desde la card del Kanban ya eliminado); el **justificativo del Jefe de Cátedra** en cita (borde izquierdo accent); y la documentación adjunta si existe.
  - **Historial del pedido**: título + nota "Auditoría · usuario · fecha (RNF-7)" + `AuditLog` (cada evento: actor, verbo, fecha, comentario).
- **Rail derecho** (`adoc-det-rail`, 380px):
  - `PanelAccionesRevision` (solo para el revisor de la etapa en su ámbito): **Aprobar y pasar a {siguiente}** (primary; oculto para Administración [BR-015]); un único `Textarea` de **Justificativo** compartido (entrada rápida); fila **Rechazar** (destructive) + **Devolver** (secondary); **Marcar prioritario** (ghost) cuando el pedido no es prioritario, o **Quitar prioritario** (ghost, sin justificativo obligatorio) cuando ya lo es — nunca ambos a la vez (tema E); y un _scope hint_ ("Revisás como {rol} de {ámbito}…"). Los botones **no mutan directo**: abren un **modal de confirmación** por acción (ver abajo). El dominio sigue siendo la autoridad (la mutation revalida los guards).
  - `DatosTramite` — meta-panel "Datos del trámite": Etapa, Ámbito, Prioritario, y la fecha de envío/creación.
- Quien no tenga acciones permitidas ve el detalle de solo lectura. El propietario actual de un `devuelto` puede editarlo y reenviarlo; el Jefe de Cátedra también puede editar y eliminar sus borradores.

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

En desarrollo, el usuario **"Demo (todos los roles)"** usa el `RoleMenu` del TopBar para cambiar de rol sin reautenticarse. La sesión local conserva sólo usuario y rol seleccionados; el backend vuelve a resolver permisos y ámbito en cada solicitud.

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
- **Reenvío del JC desde el formulario**: un pedido `devuelto` al Jefe de Cátedra se corrige con "Editar" desde Mis pedidos y se reenvía con **"Guardar y reenviar"** dentro del propio formulario (`mis-pedidos-simplificado`) — ya no es una acción separada de la tabla, cierra el lazo de corrección en el mismo lugar donde se edita.
- **Tabla con pestañas por área:** Todos, En Cátedra, En Coordinación, En Secretaría, En Decanato y Finalizados. El filtro Estado distingue estados sin alterar el área del pedido.
- **Una sola vista, sin switcher**: el Tablero Kanban se eliminó — la Tabla ya cubre toda la información que necesita el revisor (docente, asignatura, novedad, estado + avance, prioridad), y mantener dos vistas duplicaba superficie sin agregar información nueva. Ver `openspec/specs/tablero-revision-tabla/spec.md`.
- **Quitar prioritario sin justificativo (tema E)**: bajar la urgencia de un pedido es una acción de menor riesgo que subirla (no hay nada que justificar ante otros revisores); pedirlo agregaría fricción sin beneficio. Mismo guard de ámbito que Marcar prioritario — ningún actor fuera de su ámbito puede despriorizar. Queda **fuera de alcance** la jerarquía de cargos para restringir quién puede despriorizar a quién (tema C).
- **Confirmación de acciones vía modal**: las acciones de alto impacto (sobre todo Rechazar, terminal, y Devolver) requieren un paso de confirmación explícito que comunica el efecto y a quién se notifica, y mueve la validación del justificativo obligatorio al modal. El panel inline queda como disparador + entrada rápida del comentario; la confirmación es el punto único de envío (ver "Modal de confirmación de acciones de revisión").

## Patrón transversal — menú de acciones por fila (kebab ⋮)

En las tablas con acciones por fila del módulo Designaciones, las acciones se agrupan en un **menú kebab** (botón ⋮ `ellipsis-vertical` al final de la columna ACCIONES) que abre un **menú contextual** (popover), en vez de iconos sueltos inline. Unifica la columna, escala cuando hay más de una acción y deja la fila limpia.

- **Alcance actual**: _Períodos de designación_ (config · Secretaría) y _Administración de Usuarios_ (admin · Secretaría). _Mis pedidos_ dejó este patrón (`mis-pedidos-simplificado`): fila clickeable → detalle + botones "Ver"/"Editar" inline (mismo formato `Button variant="ghost" size="sm"` que las acciones de fila de Usuarios, sin menú) + X roja "Eliminar" solo en borradores.
- **Contenido = solo acciones reales y habilitadas** (invariante #7): el menú lista únicamente lo que el estado / rol permite — no se inventan acciones para "llenar" el menú (p. ej. no se agregó "Duplicar" si no es una capacidad real). En el mockup: Períodos → **Editar** + **Eliminar**; Usuarios → **Editar** + **Activar/Desactivar**.
- **Acción destructiva diferenciada**: la opción destructiva (Eliminar) se pinta con color `danger` (texto + icono `trash-2` en rojo); las demás en neutro con icono de apoyo (`square-pen` Editar, `ban` Desactivar).
- **Estados del control**: ⋮ en reposo por fila; al abrir, el disparador queda en estado activo (fondo + borde) y el popover flota sobre las filas siguientes (card blanca con borde, esquinas redondeadas y sombra).
- Mockups de referencia: columna ACCIONES con el menú abierto en la fila 1 de los frames `ICs06` (Períodos) y `A9tdD` (Administración de Usuarios) en `screens.pen`.

## Patrón transversal — filtros y ordenamiento por encabezado

Las tablas de **Usuarios** y **Docentes** ofrecen un control compacto de filtro en cada encabezado aplicable, representado por un SVG de embudo reconocible. El menú se abre desde un botón accesible y aparece inmediatamente debajo del control, fuera del contenedor con scroll horizontal; sólo se ubica arriba cuando no hay espacio suficiente. Se cierra con Escape o clic fuera y conserva un indicador cuando el filtro sigue activo. Los campos textuales buscan por coincidencia parcial sin distinguir mayúsculas ni tildes; las opciones múltiples combinan sus valores con OR y las columnas distintas con AND.

- **Usuarios**: Apellido y Nombre, Documento, Legajo y UPN/Email admiten búsqueda; Roles, Perfil docente y Estado admiten selección múltiple. Ordenan Apellido y Nombre, Documento, Legajo, UPN/Email y Estado.
- **Docentes**: Apellido y Nombre, Documento y Legajo admiten búsqueda; Rol, Ámbitos, Asignaciones, Cuenta y Estado admiten filtro. Ordenan Apellido y Nombre, Documento, Legajo, Cuenta y Estado. Rol, Ámbitos y Asignaciones no ordenan porque muestran colecciones o resúmenes.
- La columna Acciones no ofrece filtro ni orden. El orden alterna ascendente, descendente y sin orden manual; al quitarlo se conserva el orden predeterminado de la lista.
- _Mis pedidos_, _Revisión_ y _Períodos_ aplican el mismo patrón por encabezado: botón de embudo accesible, menú fuera del scroll, Escape/clic fuera, indicador de filtro activo y `aria-sort` en columnas ordenables. Los filtros generales que no representan columnas visibles permanecen en `FiltrosLista`; no se crean columnas ficticias ni se duplican controles.

## Referencias

- [`docs/product/design-principles.md`](../design-principles.md)
- Plan maestro histórico del prototipo mock: [`docs/product/designs/proyecto-docente-frontend-plan.md`](./proyecto-docente-frontend-plan.md)
- Spec funcional de pedidos: [`openspec/specs/pedidos-designacion/spec.md`](../../../openspec/specs/pedidos-designacion/spec.md)
- Spec funcional de aprobación: [`openspec/specs/aprobacion-pedidos-designacion/spec.md`](../../../openspec/specs/aprobacion-pedidos-designacion/spec.md)
- Tablero de revisión: [`openspec/specs/tablero-revision-tabla/spec.md`](../../../openspec/specs/tablero-revision-tabla/spec.md)
- Business rules: [`docs/business-rules/designaciones.md`](../../business-rules/designaciones.md)

## Open questions de diseño

- Nomenclatura definitiva del estado post-Decanato (`En lote` vs `Aprobado`) — afecta el badge a partir de SCRUM-8.
- ¿La precarga del período anterior debe traer también los adjuntos previos o solo los datos del docente? (a confirmar con el cliente).

### Catálogo de dedicaciones en administración de docentes

El change `ajustes-designaciones-y-datos-ejemplo` incorpora a cada fila de asignación un selector de Categoría 1 a 6, alimentado por el catálogo activo y enviado por UUID. La edición conserva la dedicación histórica hasta que se seleccione otra; no ofrece Categoría 0 como nueva selección.
