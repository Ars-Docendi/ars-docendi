## Context

Cuatro ajustes puntuales sobre dos pantallas ya vigentes del módulo Designaciones, todos en
`frontend/src/features/designaciones/`: el form de pedido (`PedidoForm.tsx` + secciones) y la Tabla de
revisión (`TableroRevisionPage.tsx` + `TablaRevision.tsx`). Sin impacto en backend (mock +
`localStorage`) ni en otros módulos. El punto de mayor superficie es la eliminación completa de la
novedad "Sin novedad" (D-3 abajo): toca el tipo de dominio (`Novedad`), la máquina de estados no (no
tiene ramas propias de "Sin novedad"), la validación (tampoco), el seed, dos pantallas de filtro y
media docena de componentes de presentación. Los otros tres ítems son acotados a 1-3 archivos cada
uno.

`openspec/specs/pedidos-designacion/spec.md` y `openspec/specs/tablero-revision-tabla/spec.md` siguen
sin reflejar el estado real del código (los changes `rediseno-form-pedido-designaciones`,
`rediseno-revision-solo-grilla` y `revision-tabla-agrupada-y-filtros` todavía no se archivaron) —
mismo criterio que usó `revision-tabla-agrupada-y-filtros`: las specs delta de este change se escriben
contra el comportamiento real del código actual (leído directamente), no contra el texto desactualizado
de la spec base.

## Goals / Non-Goals

**Goals:**

- Agregar `esAgenteExterno` (checkbox) a la sección "Designación solicitada" del form de pedido, en
  Alta y Cambio, junto a "Horas externas".
- Agregar un filtro opcional **Carrera** a la Tabla de revisión, catálogo cerrado de 5 carreras, más
  una columna **Carrera** (nombre abreviado) en las 4 secciones de la tabla (mismo rótulo en filtro y
  columna, ver D-5).
- Sacar "Sin novedad" de todo el sistema: radio del form, filtros (Mis pedidos y Revisión), chip,
  mensajes de `ModalConfirmacionAccion` y precarga automática del seed.
- Marcar de forma llamativa, con **fondo de fila completo**, los pedidos prioritarios (rojo) y
  devueltos (amarillo) de la Tabla de revisión — ver D-7.
- Permitir que Alta y Cambio se guarden sin materias (solo cargo y dedicación) — ver D-8.
- Ver/Editar/Eliminar fijos (nunca ocultos) en "Mis pedidos", deshabilitados cuando no aplican — ver
  D-9.
- Registrar qué departamento se hace cargo de un docente marcado como agente externo — ver D-10.
- Sumar un ícono de devuelto (junto al de prioridad, en casillero fijo) a la columna Prioritario de la
  Tabla de revisión — ver D-11.

**Non-Goals:**

- No se agrega un "valor actual" de `esAgenteExterno` con transición `actual → solicitado` en el panel
  de datos actuales (a diferencia de horas externas, que sí compara contra el catálogo) — es un dato
  nuevo sin histórico previo; se documenta como decisión D-2 abajo.
- No se reemplaza el mecanismo de reconfirmación que hoy cubre "Sin novedad" (docente que sigue igual
  período a período) — queda fuera de este change, ver nota abierta en `proposal.md`.
- No se toca el nombre real de "Ingeniería en Informática" en el resto del sistema (seeds, tests,
  `contextoActor.ts`) — el filtro nuevo usa el nombre que ya existe.
- Sin cambios de backend, sin cambios de esquema de base de datos (todo mock).

## Decisions

### D-1: Novedad por defecto de un pedido nuevo, sin "Sin novedad"

Hoy `datosIniciales()` (`PedidoForm.tsx:48`) defaultea `novedad: pedido?.novedad ?? "Sin novedad"`
para un pedido nuevo (sin `pedidoInicial`). Al sacar esa opción, el default pasa a **`"Alta"`** (primer
elemento del array `NOVEDADES` reducido a `["Alta", "Baja", "Cambio de cargo o dedicación"]`) — mismo
patrón que "el radio arranca en la primera opción", sin dejar el radiogroup sin selección al abrir
`/designaciones/pedidos/nuevo`. Alternativa descartada: dejar `novedad` sin default (`undefined`) y
forzar al Jefe de Cátedra a elegir — más fricción sin beneficio claro, y complica el tipo
(`Novedad | undefined` se filtraría a componentes que hoy asumen `Novedad` no-nulo).

### D-2: `esAgenteExterno` sin "valor actual" ni transición

`horasExternas`/`horasInvestigacion` tienen contraparte "actual" en `DocenteExistente`
(`horasExternasActuales`) para que el panel de Cambio muestre `actual → solicitado`. `esAgenteExterno`
es un dato nuevo: no hay catálogo histórico de qué docentes ya eran agentes externos. Se modela **solo**
como parte de lo solicitado (`DatosEditablesPedido.esAgenteExterno`, default `false`), sin campo
`*Actuales` en `DocenteExistente` ni fila de transición en `DatosActualesPanel`. Al seleccionar un
docente existente para un Cambio, el checkbox arranca en `false` (no se infiere de ningún dato previo) —
el Jefe de Cátedra lo marca si corresponde a esta solicitud puntual. Si más adelante el cliente pide
comparar contra un valor vigente, es una extensión natural de este mismo campo (agregar
`esAgenteExternoActual` a `DocenteExistente`), fuera de alcance ahora.

> **Cuarta ronda**: el cliente pidió que, al marcar el checkbox, se pueda registrar **qué departamento
> se hace cargo** del docente — ver D-10.

### D-3: Alcance completo de la eliminación de "Sin novedad"

Confirmado con el cliente (no es ambigüedad de esta pasada): se elimina el valor `"Sin novedad"` del
union type `Novedad` en `types.ts`, no solo su radio. Efecto en cascada, todo en el mismo change:

- `PedidoForm.tsx`: `NOVEDADES` pierde la opción; rama `esSinNovedad` (controla si se ocultan
  Justificación/Adjuntos) se elimina — con solo 3 novedades restantes, esas secciones siempre son
  aplicables (Alta/Cambio piden justificación o adjuntos, Baja también), así que el condicional
  `!esSinNovedad` que las envolvía se retira en vez de dejarse como `!false` muerto.
- `SeccionDocentePedido.tsx` / `DatosActualesPanel.tsx`: la prop `mostrarMateria` (default `true`, hoy
  seteada explícitamente solo como `novedad === "Sin novedad"`) pierde su único caller con valor
  distinto del default después de este cambio — como Baja y Cambio ya pasan `mostrarMateria={false}`
  impícitamente vía la ausencia de ese caso, **se elimina la prop entera** de ambos componentes junto
  con la columna "Materia" de la franja superior que controlaba — código muerto, no una prop que
  "podría usarse después".
- `MisPedidosPage.tsx` / `TableroRevisionPage.tsx`: los arrays de opciones del filtro Tipo pierden la
  entrada "Sin novedad".
- `NovedadChip.tsx`, `ModalConfirmacionAccion.tsx`: pierden la entrada del mapa por novedad
  (`Record<Novedad, ...>` deja de aceptar la clave, el compilador de TS ya fuerza a sacarla).
- `pedidosSeed.ts`: las 2 semillas de precarga "Sin novedad" (Laura Giménez, Diego Morales) —
  **implementado distinto de lo planeado originalmente aquí**: en vez de eliminarse, se **recastean**
  a "Cambio de cargo o dedicación" (mismo DNI/legajo/nombre/estado `borrador`, con
  `cargoSolicitado`/`dedicacionSolicitada`/`justificacion` agregados para que el Cambio sea coherente).
  Motivo del cambio de plan: `MisPedidosPage.test.tsx` usa a "Laura Giménez" como _el_ ejemplo
  canónico de borrador en ~15 aserciones (fila editable, X de eliminar, filtros, paginación) sin
  depender de su novedad — eliminarla exigía reescribir todas esas aserciones para apuntar a otro
  docente, mucho más riesgo/costo que recastear sus 2 únicos campos de novedad. Ninguna de esas
  aserciones depende del texto "Sin novedad", así que el recast no rompe ningún test existente. El
  resto de los pedidos de ejemplo (Alta/Baja/Cambio en varios estados) no se toca.
- `pedidoValidacion.ts`, `maquinaEstados.ts`: no tienen ramas propias de `"Sin novedad"` (el chequeo
  siempre fue `novedad === "Alta" | "Baja" | "Cambio..."`, nunca `=== "Sin novedad"` explícito) — el
  narrowing de TypeScript los deja compilando sin cambios de lógica, solo el tipo se achica.

Alternativa descartada (la que se había planteado primero, antes de confirmar con el cliente): sacar
"Sin novedad" únicamente del radiogroup de creación, dejando el valor vigente en el tipo para pedidos ya
precargados. Se descartó porque el cliente pidió explícitamente el alcance completo.

### D-4: Devuelto como badge lleno, reusando tokens de `StatusBadge`

~~`EstadoAvance.tsx` ya arma el contenido correcto para `devuelto`...~~ — **superado en la segunda
ronda por D-7**: el cliente pidió marcar la fila entera, no solo la celda Estado. `.adoc-estado-avance.alerta`
vuelve a ser texto plano de color (sin pill), y el fondo lleno se mueve a `.adoc-tabla-row`. Se deja
esta entrada para que quede registro de la iteración; el diseño vigente es D-7.

### D-7: Fila entera de color para prioritario/devuelto (segunda ronda), prioritario gana el empate

El cliente pidió, en la revisión de la primera ronda, marcar **la fila completa** — no solo la celda
Estado — con fondo **rojo** para prioritario y **amarillo** para devuelto; pidió lo mismo para
prioritario aunque no estaba en el alcance original (hasta ahora solo tenía el ícono de bandera en su
columna, sin fondo). Un pedido puede ser las dos cosas a la vez (`prioritario` es un flag independiente
del `estado`, y `devuelto` no es terminal — el guard de la máquina de estados solo bloquea
`priorizar`/`despriorizar` en terminales). Confirmado con el cliente: si es ambas cosas, **gana rojo**
(prioritario) — es la señal más urgente para el revisor; el detalle de la devolución no se pierde
porque sigue en la celda Estado (stepper + "Devuelto por…").

Implementación: `claseFondoFila(pedido)` en `TablaRevision.tsx` devuelve
`" adoc-tabla-row--prioritario"`, `" adoc-tabla-row--devuelto"` o `""`, apendeada a la clase base de
`FilaTablaRevision`. CSS: `--danger-100`/`--warning-100` de fondo (mismos tokens de matiz que ya usaba
el badge de D-4, ahora en la fila en vez de la celda); en `:hover` se mantiene el mismo fondo con
`filter: brightness(0.96)` en vez de resetear a `--color-bg-canvas` (el hover por defecto de
`.adoc-tabla-row`) — perder el color al pasar el mouse sería confuso. Como la fila ya lleva el color,
`.adoc-estado-avance.alerta` (celda Estado de un devuelto) vuelve a ser solo texto de color — un pill
amarillo dentro de una fila amarilla es ruido, no señal adicional. El ícono de bandera de la columna
Prioritario se mantiene sin cambios (útil al escanear esa columna puntual, no es redundante con el
fondo). Alternativa descartada: franja lateral (`border-left`) en vez de fondo completo — el cliente
pidió explícitamente "el registro entero".

### D-5: Filtro Carrera como opcional, catálogo cerrado hardcodeado

> Nombrado "Filtro Propuesta" en la primera ronda — **renombrado a "Carrera"** en la segunda, a pedido
> del cliente, para que coincida con el título de la columna (D-6) y no queden dos rótulos distintos
> para el mismo dato. La clave interna también pasó de `filtros.propuesta` a `filtros.carrera` en el
> mismo cambio.

Mismo patrón que Legajo/Prioridad en `TableroRevisionPage.tsx` (`CampoFiltroOpcional`, tipo `select`,
vía "+ Añadir filtro") — no fijo como Nombre/Tipo, porque el Coordinador (que ve una sola carrera) no
lo necesita casi nunca; es principalmente para Secretaría/Decanato/Administración. El catálogo de 5
carreras se hardcodea como constante en `filtrosTablero.ts` (mismo lugar que hoy vive la lógica de
filtros de esta pantalla) — no se crea un `catalogos.ts` de carreras nuevo ni se deriva de los pedidos
existentes (evita que una carrera sin pedidos todavía quede invisible en el selector). Comparación por
igualdad exacta (`pedido.carrera === valor`), no "contiene" — a diferencia de Nombre/Legajo, es un
`Select` de opciones cerradas, no texto libre.

### D-6: Columna Carrera — mapeo cerrado a abreviatura, mismo catálogo que el filtro

La columna nueva **Carrera** (una por sección, entre **Legajo** y **Asignatura** — mismo lugar donde se
insertó Legajo en `revision-tabla-agrupada-y-filtros`) muestra el nombre **abreviado**, no
`pedido.carrera` tal cual: un `Record<string, string>` explícito (mismo patrón que `ETIQUETA_ADJUNTO`
en `pedidoValidacion.ts` o los mapas de `NovedadChip.tsx` — no manipulación de texto tipo "sacar el
prefijo Ingeniería") con las 5 entradas que pasó el cliente: "Ingeniería en Informática" → "Informática",
"Ingeniería Industrial" → "Industrial", "Ingeniería Civil" → "Civil", "Ingeniería Mecánica" →
"Mecánica", "Ingeniería Electrónica" → "Electrónica". Vive en el mismo archivo que el catálogo de
carreras del filtro Propuesta (`filtrosTablero.ts`, D-5) — una sola fuente para ambas features, evita
que el filtro y la columna se desincronicen si se agrega una carrera nueva. Se muestra en las 4
secciones (no condicionado por rol): aunque el Coordinador solo ve pedidos de su propia carrera y el
valor es siempre el mismo en su caso, mantener la columna sin condicionar por rol es más simple y
consistente que ocultarla selectivamente — mismo criterio ya usado para el resto de las columnas de
esta tabla, que tampoco varían por rol. Alternativa descartada: format-string (`carrera.replace("Ingeniería ", "").replace("en ", "")`) — funciona para estos 5 nombres pero es frágil ante nombres que no seguían el patrón "Ingeniería <de/en> X" (ninguno de los 5 lo rompe hoy, pero un mapeo explícito no depende de que seguir rompiéndolo en el futuro).

### D-8: Alta y Cambio permiten 0 materias (regla de negocio nueva, extendida a Cambio en la tercera ronda)

El cliente aclaró una regla de negocio que no estaba en el pedido original: un docente se puede dar de
alta solo con cargo y dedicación, sin ninguna materia todavía (se le asignan después, fuera de este
flujo) — **segunda ronda**, solo Alta. En la **tercera ronda** pidió lo mismo para **Cambio**: también
tiene que poder quedar sin materias. Como las dos únicas novedades que editan este listado
(`SeccionDesignacionSolicitada`, la única no-`soloLectura`) ya cubrían el mismo caso, la regla terminó
siendo **una sola para ambas**, no dos reglas distintas — se simplificó en el mismo cambio:

- `pedidoValidacion.ts`: la exigencia de `asignaciones.length > 0` queda **solo** para Baja
  (`datos.novedad === "Baja" && datos.asignaciones.length === 0`) — Baja sigue exigiendo mínimo 1
  porque su listado refleja lo que el docente ya tiene asignado (no debería llegar vacío nunca, a
  diferencia de Alta/Cambio que reflejan lo que se está pidiendo).
- `SeccionMateriasHoras.tsx`: la prop `permiteVacio` **se eliminó** — con Alta y Cambio permitiendo
  vaciar por igual (y Baja siempre `soloLectura`, nunca llega a este botón), no quedaba ningún caller
  que necesitara el comportamiento viejo (mínimo 1). El botón "Quitar" se muestra siempre que
  `!soloLectura`, sin condición de longitud.
- `SeccionDesignacionSolicitada.tsx`: la prop `esAlta` (que solo se usaba para setear `permiteVacio`)
  **se eliminó** junto con ella — quedaba sin uso.
- `PedidoForm.tsx#quitarMateria`: el guard `if (...) return prev` **se eliminó** — la función nunca se
  invoca para Baja (su `SeccionMateriasHoras` no recibe `onQuitar`), así que no necesita distinguir
  novedad.

El form sigue arrancando con 1 fila vacía por defecto en ambas novedades (sin cambios en
`datosIniciales`) — el Jefe de Cátedra la borra si no quiere cargar materias todavía, no hace falta
arrancar en 0 filas.

### D-9: Ver/Editar/Eliminar fijos en "Mis pedidos", deshabilitados en vez de ocultos (segunda ronda)

`TablaMisPedidos.tsx` ocultaba "Editar" y la X de "Eliminar" con `{condición && <Button>}` cuando
`puedeEditarPedido`/`puedeEliminarPedido` daban `false` — la fila de acciones cambiaba de ancho según
el estado del pedido. El cliente pidió que los 3 botones sean fijos (misma posición siempre) y que el
que no aplica se vea deshabilitado. Cambio: los `{condición && <Button>}` pasan a
`<Button disabled={!condición}>` (para "Editar", que ya usaba el componente `Button` de
`@ars-docendi/ui`, que reenvía `disabled` de forma nativa) y `<button disabled={!condición}>` (para la
X de eliminar, que es un `<button>` propio con clase `adoc-mp-eliminar`). CSS nuevo en
`misPedidos.css`: `.adoc-mp-acc :disabled { opacity: 0.4; cursor: not-allowed; }` — selector genérico
que cubre ambos botones sin duplicar la regla, más un `:disabled:hover` que anula el hover rojo de la X
(no tiene sentido resaltarla si no hace nada al clickear). "Ver" no cambia: siempre estuvo fijo y
habilitado.

### D-10: Departamento a cargo del agente externo — condicional, catálogo cerrado, sin "valor actual" (cuarta ronda)

El cliente pidió que, al marcar "Docente es agente externo", se habilite un selector para registrar
qué departamento (o Secretaría Académica) se hace cargo de él, con 7 opciones cerradas que dio
textualmente. Decisiones:

- **Tipo nuevo, no `string` libre**: `DepartamentoAgenteExterno` en `types.ts`, unión de 7 literales
  (mismo patrón que `TipoBaja`) — un catálogo cerrado se modela como union type, no como `string`
  suelto, para que el compilador detecte cualquier valor inválido en el resto del código.
- **Catálogo en `api/catalogos.ts`** (`DEPARTAMENTOS_AGENTE_EXTERNO`), no en `filtrosTablero.ts` —
  a diferencia de `CARRERAS` (D-5), este catálogo alimenta un campo del **form de pedido**, no un
  filtro de la Tabla de revisión; vive donde ya viven `CARGOS`/`DEDICACIONES`/`TIPOS_BAJA`, mismo
  criterio de ubicación por consumidor.
- **Renderizado condicional, no `disabled`**: el `Select` "Departamento a cargo" solo se muestra
  cuando `esAgenteExterno` es `true` (mismo patrón que "Otro" → campo "Detalle" en Tipo de baja) —
  no un `Select` siempre visible pero deshabilitado. Consistente con el resto del form: ningún otro
  campo condicional de esta pantalla usa `disabled`, todos aparecen/desaparecen.
- **Se limpia al desmarcar**: `PedidoForm.tsx` gana `cambiarEsAgenteExterno`, que resetea
  `departamentoAgenteExterno` a `undefined` cuando el checkbox pasa a `false` — evita guardar un
  departamento "huérfano" sin su checkbox correspondiente si el Jefe de Cátedra lo tilda y destilda.
- **Obligatorio solo si `esAgenteExterno` es `true`**: `pedidoValidacion.ts` agrega
  `if (datos.esAgenteExterno && !datos.departamentoAgenteExterno) errores.departamentoAgenteExterno = ...`
  — un `if` más, sin acoplarse a la novedad (el checkbox ya está gateado a Alta/Cambio por la sección
  que lo contiene).
- **Sin "valor actual"**: mismo criterio que D-2 — es un dato nuevo sobre lo solicitado, no hay
  histórico de qué departamento tenía a cargo a un docente antes, así que no hay transición
  `actual → solicitado` en el panel de Cambio.

### D-11: Flechita de devuelto centrada junto a la bandera de prioridad (quinta ronda, corregido en la sexta)

El cliente pidió sumar, a la bandera de prioridad que ya tenía la columna Prioritario de la Tabla de
revisión, un ícono para devuelto. Primer intento (quinta ronda) — **descartado en la sexta**: dos
casilleros de ancho fijo (`.adoc-tabla-prio-slot`), uno por ícono, siempre presentes aunque vacíos. El
cliente lo corrigió: no quería casilleros fijos — quería que **un solo ícono quede centrado en la
columna** (misma posición visual sea cual sea el ícono), y que **con los dos** se abra un espacio en
el medio quedando uno a cada lado (bandera a la izquierda, flechita a la derecha). Con casilleros
fijos, un solo ícono quedaba pegado a un costado (con el casillero vacío del otro lado corriéndolo del
centro) — exactamente lo que el cliente notó como "mal posicionado".

Implementación final (sexta ronda): sin casilleros — los dos íconos son hijos condicionales directos
de `.adoc-tabla-prio` (`{pedido.prioritario && <PrioridadFlagIcono />}` seguido de
`{pedido.estado === "devuelto" && <DevueltoFlechaIcono />}`), y la celda ya tenía
`justify-content: center` (heredado de antes de este change) más `gap: 6px` (nuevo). Flexbox centra
lo que haya: **un** ícono queda centrado solo; **dos** íconos se centran como par, separados por el
`gap` — que es, visualmente, el "espacio en el medio" que pidió el cliente — bandera primero
(izquierda) por ir primero en el JSX, flechita segunda (derecha). Mucho más simple que la solución
descartada, y es exactamente el comportamiento por defecto de `justify-content: center` con múltiples
hijos — no hacía falta ninguna lógica de posicionamiento propia.

- **Ícono reusado, no uno nuevo**: `DevueltoFlechaIcono` (en `NovedadChip.tsx`, junto a
  `PrioridadFlagIcono`) usa el mismo path `corner-up-left` que ya representa "Devuelto" en
  `EstadoPedidoPill.tsx` (Mis Pedidos) — mismo lenguaje visual en toda la feature para el mismo
  estado, en vez de inventar un ícono distinto para la Tabla de revisión.
- **Color ámbar (`--warning-500`)**: coherente con el amarillo que ya usa la fila devuelta (D-7) y el
  texto de la celda Estado (`.adoc-estado-avance.alerta`) — mismo tono en los tres lugares donde
  "devuelto" se representa en esta pantalla.
- **Columna Prioritario pasa de 44px a 60px**: el ancho fijo de la última columna del grid
  (`grid-template-columns`) tenía que crecer para que quepan dos íconos de 13px + gap sin apretarse
  contra Estado ni desbordar.
- **Independiente del color de fila (D-7)**: los dos íconos se muestran según sus propias condiciones
  (`pedido.prioritario`, `pedido.estado === "devuelto"`) — ninguno de los dos "gana" sobre el otro acá,
  a diferencia del fondo de fila (donde prioritario sí gana). Un pedido prioritario y devuelto muestra
  **ambos** íconos a la vez, aunque su fila sea roja (no amarilla) por D-7 — son señales
  independientes, no hay contradicción.

## Risks / Trade-offs

- ~~**[Riesgo] Eliminar "Sin novedad" es un cambio de comportamiento de negocio, no solo de UI**~~ —
  **cerrado**: el cliente confirmó que "Sin novedad" no se vuelve a usar nunca más — la eliminación
  sin reemplazo es el resultado buscado, no un riesgo pendiente.
- **[Riesgo] Tests que siembran `novedad: "Sin novedad"`** (`PedidoForm.test.tsx`,
  `pedidoValidacion.test.ts`, `maquinaEstados.test.ts`, `detalleAdapters.test.ts`,
  `EstadoAvance.test.tsx`, `ModalConfirmacionAccion.test.tsx`, `TablaRevision.test.tsx`,
  `tableroRevisionModelo.test.ts`, `pedidosApi.test.ts`) dejan de compilar en cuanto se achica el union
  type → mitigado por diseño: el compilador de TS marca cada uno, no hay forma de "olvidarse" de
  ninguno; se resuelven reescribiendo esos fixtures a otra novedad (Alta/Baja/Cambio) en el mismo PR.
- ~~**[Riesgo] Nombre de carrera "Ingeniería Informática" (cliente) vs. "Ingeniería en Informática"
  (código)**~~ — **cerrado**: el cliente confirmó que el nombre es a modo informativo, no hace falta
  unificarlo — se mantiene el nombre existente en código (ver D-5).
- **[Trade-off] `mostrarMateria` se elimina en vez de dejarse `false` permanente** → más líneas
  tocadas en este change (dos componentes en vez de uno), pero evita dejar una prop y una rama de JSX
  que ya no se puede activar nunca — preferido sobre dejar código muerto "por si vuelve".

## Migration Plan

- Cambio de una sola pasada, sin fases: todo el impacto vive en `frontend/`, sin backend ni datos
  persistentes reales (mock + `localStorage`, se resetea con los seeds nuevos).
- Orden sugerido en `tasks.md`: (1) tipo `Novedad` + limpieza de sus consumidores (compilador guía),
  (2) form de pedido (radio reducido + checkbox agente externo), (3) filtro Carrera + columna Carrera
  en Revisión, (4) fondo de fila rojo/amarillo (D-7), (5) materias opcionales en Alta (D-8), (6)
  botones fijos en Mis Pedidos (D-9), (7) specs delta + docs de negocio si aplica, (8) tests.
- Rollback: revertir el PR restaura el radio de 4 opciones, la precarga "Sin novedad", el form sin el
  checkbox, la Tabla de revisión sin filtro Carrera y sin el fondo de fila rojo/amarillo, Alta exigiendo
  mínimo 1 materia, y los botones de Mis Pedidos condicionales — sin necesidad de migración de datos
  (el `localStorage` del navegador se re-hidrata desde el seed en el próximo
  `openspec/../pedidosApi` reset).

## Open Questions

Ninguna pendiente — las 4 quedaron resueltas tras la revisión del cliente:

- ~~¿El nombre "Ingeniería en Informática" debe unificarse a "Ingeniería Informática"...?~~ —
  **resuelto**: es informativo, no hace falta unificarlo.
- ~~¿Hace falta algún reemplazo para la reconfirmación de docentes sin cambios que hoy cubría "Sin
  novedad"?~~ — **resuelto**: no, "Sin novedad" no se vuelve a usar nunca más.
- ~~Catálogo de carreras: el cliente mencionó 7 pero pasó 5 nombres...~~ — **resuelto**: 5 está bien
  por ahora, la lista completa va a depender del backend más adelante.
- ~~La columna nueva se llama "Carrera" y el filtro que apunta al mismo dato se llama "Propuesta"~~ —
  **resuelto en la segunda ronda**: ambos se llaman "Carrera".
