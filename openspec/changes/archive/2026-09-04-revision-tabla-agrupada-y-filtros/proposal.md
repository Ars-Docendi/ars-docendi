## Why

La Tabla de revisión (`/designaciones/revision`, única vista desde el tema E) hoy lista todos los
pedidos del ámbito en una sola lista plana ordenada por estado — con muchos pedidos, el revisor tiene
que escanear toda la lista para encontrar los de un estado puntual, y no hay forma de buscar un
docente puntual salvo scrolleando. El cliente pidió: mostrar el nombre del docente sin el prefijo
"Prof." (redundante, la columna ya es "Docente"), agrupar la tabla en una sección por estado que se
pueda desplegar/colapsar, y poder filtrar por nombre o legajo del docente — como ya existe en la
pantalla de Usuarios.

## What Changes

> **Estado (segunda ronda):** este change se retomó parcialmente — el cliente pidió específicamente
> "agregar el mismo filtro para la pantalla Revisión que el que está en Mis Pedidos". Se implementó
> **solo el filtro** (este bullet, reescrito) reusando `shared/ui/FiltrosLista.tsx` (construido
> mientras tanto en `mis-pedidos-simplificado`). El **legajo en el modelo** ya había quedado resuelto
> por ese mismo change (D-10), así que tampoco hubo que tocarlo acá. La **agrupación por estado** y
> **sacar "Prof."** siguen en el alcance del change pero **pausados** ("dejemos de lado la tabla
> agrupada por ahora", instrucción explícita anterior del cliente) — quedan como bullets sin marcar
> abajo, sin implementar.

- ~~**Nombre del docente sin prefijo**~~: implementado en la cuarta ronda, ver más abajo.
- ~~**Agrupación desplegable por estado**~~: pausado, no implementado en esta pasada.
- **Filtro con el mismo patrón que Mis Pedidos** — **implementado**: `<FiltrosLista>` (componente
  genérico config-driven, `shared/ui/FiltrosLista.tsx`) en vez de los tres `Select` sueltos que hoy
  tiene la barra de filtros. **Nombre** del docente queda como campo fijo (paridad con "Docente" de
  Mis Pedidos); **Legajo**, **Tipo** y **Prioridad** pasan a ser opcionales vía "+ Añadir filtro"
  (paridad con Legajo/Tipo/Estado de Mis Pedidos) — Tipo y Prioridad eran `Select` siempre visibles
  antes, ahora se agregan/quitan igual que Legajo. **Vista** ("Mis pendientes"/"Vista completa") queda
  aparte, en las acciones del header — no es un filtro de dato, es un switcher de alcance. Comparación
  "contiene, sin distinguir mayúsculas/acentos" para Nombre y Legajo, igual que en Usuarios/Mis
  Pedidos.
- ~~**Nuevo campo `legajo` en el modelo del docente**~~: **ya resuelto** — `mis-pedidos-simplificado`
  (D-10) agregó `legajo` a `DocentePedido`/`DocenteExistente`, catálogo y semillas antes de que este
  change se retomara. No se duplicó el trabajo.

> **Estado (tercera ronda):** el cliente pidió tres columnas más para la Tabla de revisión (la
> **plana** actual — la agrupación en secciones sigue pausada, ver arriba), en paridad con Mis
> Pedidos: **Legajo** del docente (número de 4 dígitos), renombrar **Novedad** a **Tipo** (mismo
> header que Mis Pedidos; se mantiene el chip visual `NovedadChip`, solo cambia el título de
> columna) y agregar **Fecha Última Actualización** (fecha del último evento del historial del
> pedido, cualquier acción — no solo "enviar"). Las tres, **implementadas** en esta pasada sobre
> `TablaRevision.tsx`.

- **Columna Legajo** — **implementada**: nueva columna entre Docente y Asignatura, muestra
  `pedido.docente.legajo` (formato "—" si el docente de un Alta todavía no tiene legajo asignado,
  igual que en Mis Pedidos).
- **Header Tipo** — **implementado**: la columna que mostraba el chip de Novedad bajo el header
  "Novedad" ahora se titula "Tipo" (paridad textual con Mis Pedidos); el chip `NovedadChip` no
  cambia.
- **Columna Fecha Última Actualización** — **implementada**: nueva columna entre Tipo y Estado,
  fecha del evento más reciente de `pedido.historial` (`.at(-1)`, cualquier `AccionHistorial`),
  formateada `dd/mm/aaaa` vía `formatearFecha` (ya existente en `detalleAdapters.ts`).

> **Estado (cuarta ronda):** el cliente pidió retomar, específicamente, sacar el prefijo "Prof." de
> la columna Docente de la Tabla de revisión — **implementado**, desacoplado de la reestructura en
> secciones (que sigue pausada, sin instrucción del cliente para retomarla todavía). `TablaMisPedidos`
> y `ModalConfirmacionAccion` conservan "Prof. {nombre}" — el pedido fue específico de esta tabla.

- **Nombre del docente sin prefijo** — **implementado**: la columna Docente de `TablaRevision.tsx`
  muestra `{pedido.docente.nombre}` sin el `"Prof. "` hardcodeado que tenía antes.

> **Estado (quinta ronda):** el cliente retomó la agrupación, pero con un criterio distinto al
> planeado originalmente (D-1/D-2 de `design.md`, ver nota de superación ahí). En vez de agrupar por
> **estado de avance** (En revisión / Aceptados / Devueltos / Rechazados), ahora se agrupa por
> **etapa del circuito**: **En Coordinación** / **En Secretaría** / **En Decanato** / **Finalizados**
> (Aceptados + Rechazados juntos). Motivo explícito del cliente: Secretaría Académica, Administrativo
> y Decanato ven **todo el departamento** (a diferencia del Coordinador, que ve solo su carrera —
> BR-009), y necesitan triangular grandes volúmenes de pedidos por **dónde están trabados** en la
> cadena, no por si ya se cerraron. **Implementado**, reemplaza por completo la agrupación planeada
> antes (nunca se había llegado a construir).

- **Reagrupación por etapa del circuito** — **implementada**: `construirColumnas` (renombrado en
  espíritu, mismo nombre de función) ahora arma 4 secciones — `en-coordinacion`, `en-secretaria`,
  `en-decanato`, `finalizados` — en vez de las 4 anteriores. Un pedido **Devuelto** no tiene sección
  propia: vive en la sección de la etapa a la que volvió (`pedido.etapaRetorno`) — es ahí donde queda
  trabado hasta que se corrija y reenvíe. Dentro de cada sección de etapa, el orden es: **prioritarios
  primero**, después **devueltos**, después el resto por **fecha de última actualización ascendente**
  (el que espera hace más tiempo, arriba). Dentro de **Finalizados**: **Aceptados antes que
  Rechazados**; dentro de cada bloque, por fecha descendente (el cierre más reciente arriba).
- **Fila "Devuelto por {revisor}"** — **implementada**: la celda Estado de un pedido devuelto ya no
  dice solo "Devuelto" — dice **"Devuelto por {nombre de quien lo devolvió}"** (no el propietario
  actual que debe corregirlo, que a veces es el Jefe de Cátedra y no encaja en ninguna de las 3
  secciones de etapa) — da contexto de quién encontró el problema, sin necesitar abrir el detalle.
  Nuevo helper `quienDevolvio(pedido)` en `tableroRevisionModelo.ts` (último evento `"devolver"` del
  historial, campo `porNombre`).
- **Secciones desplegables** — **implementada**: nuevo sub-componente `SeccionEstadoTabla` dentro de
  `TablaRevision.tsx` — header `<button>` (`aria-expanded`, `aria-controls`, chevron que rota) +
  título + contador de pedidos; body condicional a `expandido`. Mismo patrón que `GrupoColapsable` de
  `Sidebar.tsx` — sin componente Accordion nuevo en `@ars-docendi/ui`. Una sección sin pedidos igual
  se muestra, con contador en 0 y el texto "Sin pedidos" (reusa el estado vacío existente).

> **Estado (sexta ronda):** el cliente ajustó el detalle de UX de las secciones recién entregadas
> (quinta ronda) — todo **implementado** sobre lo mismo, sin cambiar de nuevo el criterio de
> agrupación (sigue siendo por etapa del circuito, D-6):

- **Expansión por default según el rol del actor** — **implementada** (reemplaza "las 4 arrancan
  expandidas" de la quinta ronda): arranca expandida **solo** la sección del rol del actor —
  Coordinador → "En Coordinación", Secretaría → "En Secretaría", Decanato → "En Decanato" —; las
  otras 3 arrancan colapsadas. Administración no tiene sección propia ("da igual" cuál, instrucción
  explícita del cliente) → las 4 arrancan colapsadas para ese rol. Nuevo helper
  `seccionInicialDelActor(actor)` en `tableroRevisionModelo.ts`.
- **Head de columnas por sección, no compartido** — **implementado**: cada sección expandida
  renderiza su propio head (Docente/Legajo/Asignatura/Tipo/Fecha/Estado), ya no hay un head único
  arriba de las 4 — ahora que las secciones están separadas visualmente (ver bullet siguiente), un
  head compartido arriba de todas quedaba desconectado de las secciones colapsadas.
- **Separación visual entre secciones** — **implementada**: cada sección pasa a ser su propia card
  (borde + radius + fondo), separadas entre sí por `gap` (`var(--space-4)`, 16px) en vez de vivir
  pegadas dentro de un único contenedor con `border-top` entre ellas — pedido explícito del cliente
  ("no las dejes pegadas").
- **Color del título de sección distinto al de los headers de columna** — **implementado**: el
  título de cada sección ("En Coordinación", etc.) pasa a `--accent-700` (antes `--color-text-primary`,
  muy parecido al negro de las columnas) en las 4 secciones por igual (antes solo las 3 de etapa
  tenían acento; "Finalizados" quedaba en el mismo tono que las columnas) — pedido explícito del
  cliente ("se vuelve confuso").
- **Aceptado sin stepper** — **implementado**: la celda Estado de un pedido Aceptado (`en_lote`) pasa
  de mostrar el stepper completo de 4 barras + "Aceptado" a mostrar un punto verde + "Aceptado" —
  mismo lenguaje visual que Devuelto/Rechazado (dot de color + etiqueta), ya que es un estado
  terminal sin avance que mostrar.
- **Filtro Tipo fijo, al lado de Nombre** — **implementado**: `shared/ui/FiltrosLista.tsx` gana
  soporte para campos **fijos tipo select** (antes los fijos eran siempre `Input` de texto); en
  Revisión, **Tipo** pasa de opcional ("+ Añadir filtro") a fijo, junto a **Nombre**, al inicio de la
  barra — rompe la paridad de patrón con Mis Pedidos que se había buscado en la segunda ronda
  (instrucción explícita y posterior del cliente, específica de esta pantalla). Legajo y Prioridad
  siguen opcionales.
- **Más casos de Alta/Baja en el seed** — **implementado**: `pedidosSeed.ts` suma 4 Altas + 4 Bajas
  nuevas, repartidas en varias etapas del circuito (en revisión en cada una de las 3 etapas, devuelto,
  en_lote, rechazado) — antes había 1 sola Alta (rechazada) y 1 sola Baja (en borrador, ni siquiera
  visible en esta tabla). Para probar filtros, columnas y la agrupación con más variedad de datos.

> **Estado (séptima ronda):** ajuste puntual sobre el contador de cada sección — **implementado**.

- **Contador con etiqueta "Total:"** — **implementado**: el badge de cada header de sección pasa de
  mostrar solo el número (`2`) a mostrar `Total: 2` — más explícito sobre qué representa el número.
- **Color del contador distinto al fondo y a los headers de columna** — **implementado**: el badge
  pasaba casi inadvertido (fondo `--neutral-200` muy parecido al fondo del header de sección
  `--color-bg-canvas`, texto `--color-text-secondary` parecido al gris de los headers de columna
  `.adoc-tabla-h`). Pasa a `background: var(--accent-100)` + `color: var(--accent-700)` — mismo par
  de tokens que ya usa el avatar de iniciales (`.adoc-pedido-avatar`), consistente con el acento que
  ya tiene el título de la sección.

> **Estado (octava ronda):** el cliente marcó que el **card entero** de una sección colapsada se
> perdía contra el fondo de la página (screenshot: los 4 headers "En Coordinación/Secretaría/
> Decanato/Finalizados" se veían literalmente del mismo color que el fondo) — **implementado**.

- **Fondo del header de sección distinto al de la página** — **implementado**: la causa raíz era que
  `.adoc-seccion-header` usaba `background: var(--color-bg-canvas)` — el **mismo token** que usa el
  fondo de toda la página (`app/shell/shell.css`, el contenedor de contenido principal). Con una
  sección colapsada (solo el header visible, sin body), la card entera quedaba del mismo color que la
  página detrás, con el borde como única señal (insuficiente, según el cliente).

> **Estado (novena ronda):** pedido de seguimiento inmediato — "poneles un color verde claro" —
> **implementado**, en vez del blanco de la corrección anterior.

- ~~**Verde claro para las 4 cards**~~ — **superado en la décima ronda** (ver abajo): `.adoc-seccion-tabla`
  y `.adoc-seccion-header` pasaron primero a `background: var(--color-status-success-bg)` (=
  `--success-100`, el token semántico de "éxito"). El head de columnas (`.adoc-tabla-head`, dentro
  del body) sigue en `--color-bg-canvas` y las filas en `--color-bg-raised` (blanco) — ahora sí se
  distinguen correctamente, porque quedan anidados dentro de una card de color, no pegados al fondo
  gris de la página. Esto sigue vigente; lo que cambió en la décima ronda es **qué verde** se usa.
- ~~**Contador ajustado para no perderse contra el nuevo verde**~~ — **superado en la décima ronda**:
  pasó primero a un pill blanco (`--color-bg-raised`) con texto `--color-status-success-fg` (verde de
  "éxito"), con contraste garantizado contra el fondo de color.

- ~~**`--accent-200` como fondo, un escalón más oscuro**~~ — **superado en la undécima ronda**: se
  optó por `--accent-200` (86% de luminosidad, mismo verde-azulado que el título) en vez de
  `--success-100`, reutilizando un token existente sin `color-mix()`. El cliente lo rechazó
  ("está horrible") en la ronda siguiente.

> **Estado (undécima ronda):** el cliente rechazó el fill verde ("está horrible") y pidió una
> recomendación de color que quede bien con el fondo existente — **implementado**, cambio de
> **enfoque**, no solo de tono.

- **Diagnóstico**: el fondo de página (`--color-bg-canvas` = `--neutral-200`) es un cálido casi-neutro
  (`oklch(95% 0.006 75)` — matiz 75°, croma casi cero). El verde de marca (`--accent-*`) vive en
  matiz ~165°, a 90° de distancia en la rueda de color, con croma bastante más alto (0.06-0.12) — un
  fill sólido en esa familia **siempre** iba a leerse "frío" y desentonar contra ese cálido, sin
  importar qué tan claro u oscuro fuera el escalón elegido. Ninguna de las dos rondas anteriores
  (verde claro, verde un poco más oscuro) iba a funcionar por esta razón de fondo.
- **Card blanca + franja de color en el borde izquierdo (en vez de fill sólido)** — **superado en la
  duodécima ronda** (ver abajo): `.adoc-seccion-tabla` pasó primero a `background: var(--color-bg-raised)`
  (`#ffffff` puro, blanco sin matiz) más `border-left: 4px solid var(--accent-500)` y una sombra sutil
  (`box-shadow: 0 1px 3px rgba(0,0,0,0.06)`). El cliente lo rechazó también ("el blanco ese es
  horrible y no combina con el fondo beige") — un blanco totalmente sin matiz, al lado de un fondo
  cálido, también choca (aunque de forma distinta al verde: por ausencia de matiz en vez de por
  matiz opuesto). La franja de color y el contador en pill sí quedaron bien, se mantienen.

> **Estado (duodécima ronda):** el cliente rechazó también el blanco puro — **implementado**, mismo
> patrón (card + franja), pero con el tono de fondo correcto.

- **`--neutral-100` en vez de `--color-bg-raised`** — **implementado**: `--color-bg-raised` está
  definido como `#ffffff` literal (sin matiz), mientras que el fondo de página
  (`--color-bg-canvas` = `--neutral-200`) es un cálido con matiz 75°. `--neutral-100` (98% de
  luminosidad) es la propia escala de neutros de la app — comparte el matiz 75° del fondo, apenas
  más claro (95%→98%) — combina en vez de competir. `.adoc-seccion-tabla` y `.adoc-seccion-header`
  pasan a `background: var(--neutral-100)`; la franja (`--accent-500`), el borde y la sombra no
  cambian; el contador sigue en pill de color (`--accent-100`/`--accent-700`).

> **Estado (decimotercera ronda):** con la card ya resuelta, el cliente marcó que el head de
> columnas (Docente/Legajo/Asignatura/Tipo/Fecha/Estado) dentro de cada sección expandida no se
> distinguía ni del fondo de página ni del header/título del desplegable — **implementado**.

- ~~**Fondo del head de columnas: `--neutral-300`**~~ — **rechazado en la decimocuarta ronda**
  ("horrible"): `.adoc-tabla-head` pasó de `--color-bg-canvas` (= `--neutral-200`, el mismo tono que
  el fondo de página) a `--neutral-300` (91%, un escalón más oscuro que la card). El cliente no
  quería un tono propio para el head de columnas — quería que combine con el título, no que se
  diferencie más.

> **Estado (decimocuarta ronda):** el cliente pidió explícitamente que el head de columnas tenga
> **el mismo color** que el título del desplegable — **implementado**, revierte la decimotercera.

- **Fondo del head de columnas = fondo del título del desplegable** — **implementado**:
  `.adoc-tabla-head` pasa a `background: var(--neutral-100)` — el mismo token que
  `.adoc-seccion-header`. Header + head de columnas quedan como una sola superficie visual
  continua; el `border-bottom` entre ambos (`--color-border-strong`) sigue marcando la separación
  de contenido sin necesitar un cambio de color.

> **Estado (decimoquinta ronda):** con el mismo color, el título del desplegable y el head de
> columnas quedaban fundidos en un solo bloque sin nada entre medio — el cliente pidió agregar algo
> que los diferencie — **implementado**.

- **Línea divisoria entre el título y el head de columnas** — **implementado**: nuevo
  `border-top: 1px solid var(--color-border-default)` en `.adoc-tabla-head` — antes no había ningún
  elemento entre el botón del título (`.adoc-seccion-header`) y el head de columnas, así que al
  compartir el mismo fondo (`--neutral-100`, decimocuarta ronda) se leían como un solo bloque. La
  línea marca la transición sin volver a tocar el color de fondo.

> **Estado (decimosexta ronda):** el cliente marcó un problema funcional, no visual: mostrar
> "Devuelto por {nombre}" en la celda Estado **eliminó** la referencia a la etapa/estado real del
> pedido (el dato que un revisor usa, junto con la sección en la que vive la fila, para entender el
> filtro "Mis pendientes") — pidió que la etapa vuelva a estar, al costado de "Devuelto por"
> — **implementado**.

- ~~**Etapa al costado de "Devuelto por", como texto plano**~~ — **superado en la decimoséptima
  ronda**: primer intento, `"{etiquetaEtapaRetorno(pedido) ?? "Devuelto"} · Devuelto por
{quienDevolvio(pedido) ?? "—"}"` (sin stepper, todo texto). El cliente aclaró que quería mantener
  el stepper de 4 barras + el formato exacto que ya usaban los estados en revisión, no solo el
  texto de la etapa.
- **Ellipsis para textos largos en la celda Estado** — **implementado, sigue vigente**: con más
  texto en la celda, `.adoc-estado-avance-txt` suma `overflow: hidden` + `text-overflow: ellipsis`
  para no desbordar ni solaparse con la columna Prioritario — antes solo tenía `white-space: nowrap`.

> **Estado (decimoséptima ronda):** el cliente aclaró el pedido de la decimosexta: no alcanzaba con
> el texto de la etapa — quería el **mismo stepper de 4 barras** que ya usan los estados en revisión
> (el "formato" completo, no solo la etiqueta), calculado sobre la etapa a la que vuelve el
> devuelto, y "Devuelto por {nombre}" **más el rol/tipo de quien lo devolvió**, agregados al costado
> de ese stepper — no reemplazándolo por un simple punto de color — **implementado**.

- **Mismo stepper que un estado en revisión, calculado sobre `etapaRetorno`** — **implementado**:
  nuevo helper `avanceEtapaRetorno(pedido)` en `tableroRevisionModelo.ts` — mismo shape que
  `avancePedido` (`{ etiqueta, paso, total }`), pero indexado por `pedido.etapaRetorno` en vez de
  `pedido.estado`, reusando `PASO_DE_ETAPA`/`ETIQUETA_ETAPA`. `EstadoAvance.tsx` reusa el mismo
  componente `<Stepper>` que ya usan los estados activos para pintar un devuelto: mini-stepper
  (parcial, 1-3 de 4 barras — un `etapaRetorno` nunca es `en_lote`) + "En {etapa} · {paso}/{total}".
- **"Devuelto por {nombre} ({rol})" al costado del stepper** — **implementado**: nuevo helper
  `rolDeQuienDevolvio(pedido)` (mismo patrón que `quienDevolvio`, el `porRol` del último evento
  `"devolver"`). La celda Estado de un devuelto queda: **"En Coordinación · 1/4 · Devuelto por M.
  Díaz (Coordinador)"** — el stepper + etapa que ya existía para estados en revisión, sin tocar ese
  formato, con el detalle de la devolución agregado al final de la misma línea. Si el pedido no
  tiene `etapaRetorno` (no debería pasar, invariante de dominio) cae a un dot en vez de stepper, con
  el mismo "Devuelto por {nombre} ({rol})" — el único caso sin stepper, pero el detalle de quién
  devolvió no se pierde.

## Capabilities

### New Capabilities

_(ninguna)_

### Modified Capabilities

- `tablero-revision-tabla`: el requirement ADDED "Filtro de pedidos por nombre o legajo del docente"
  está **implementado**, con un ajuste de la sexta ronda: **Tipo** deja de ser opcional y pasa a ser
  un campo fijo junto a Nombre (reescrito para describirlo así, en vez de opcional vía "+ Añadir
  filtro"). El requirement MODIFIED "Vista Tabla del tablero de revisión" está **completamente
  implementado**: columnas **Legajo** y **Fecha última actualización** + renombre **Novedad → Tipo**
  - nombre del docente **sin prefijo "Prof."** + **agrupación en 4 secciones desplegables por etapa
    del circuito** (En Coordinación / En Secretaría / En Decanato / Finalizados, no por estado de
    avance como se había planeado originalmente — ver D-1/D-2 superadas en `design.md`), con expansión
    por default según el rol del actor (sexta ronda, ver D-8) y Aceptado con dot en vez de stepper
    (D-9). Ya no queda texto pendiente en este requirement.
- `pedidos-designacion`: el requirement "Creación de pedido de designación" (legajo en los datos del
  docente) ya está satisfecho por el código real — lo entregó `mis-pedidos-simplificado`, no este
  change. El texto del delta no cambia (sigue siendo correcto), solo se aclara la procedencia.

## Impact

- **Frontend** (`frontend/src/features/designaciones/`):
  - ~~`types.ts`, `api/catalogos.ts`, `api/pedidosSeed.ts`, `PedidoForm.tsx#seleccionarDocente`~~ — ya
    entregado por `mis-pedidos-simplificado`, sin cambios acá.
  - `components/filtrosTablero.ts`: `FiltrosTablero` suma `nombre`/`legajo` (+ índice de string);
    `aplicarFiltros` los aplica (texto libre, normalizado).
  - `pages/TableroRevisionPage.tsx`: bloque `<FiltrosLista>` (Nombre fijo, Legajo/Tipo/Prioridad
    opcionales) en vez de los tres `Select` sueltos; Vista queda separado en las acciones del header.
  - `components/TablaRevision.tsx`: columna Legajo (entre Docente y Asignatura), header "Novedad" →
    "Tipo", columna Fecha Última Actualización (entre Tipo y Estado), nombre del docente sin el
    prefijo "Prof."; reestructurado en 4 secciones desplegables vía nuevo sub-componente
    `SeccionEstadoTabla` (header `aria-expanded` + chevron + título + contador; body con su propio
    head de columnas + filas, condicional a `expandido`, inicializado según
    `seccionInicialDelActor(actor)`) — **implementado**.
  - `components/tableroRevisionModelo.ts`: `construirColumnas` reescrito (ya no toma `actor`; el
    orden ya no depende de "tu turno") para agrupar por etapa del circuito en vez de estado de
    avance; nuevos helpers `fechaUltimaActualizacion` (reusa `formatearFecha` de
    `detalleAdapters.ts`), `quienDevolvio` (último evento `"devolver"` del historial),
    `seccionInicialDelActor` (sección que arranca expandida según el rol) y
    `etiquetaEtapaRetorno` (etiqueta de la etapa a la que vuelve un devuelto, reusando
    `ETIQUETA_ETAPA`) — **implementado**.
  - `components/EstadoAvance.tsx`: la celda Estado de un devuelto muestra "{etapa} · Devuelto por
    {revisor}" (la etapa de `etapaRetorno` al costado de quién lo devolvió, decimosexta ronda) en
    vez de solo "Devuelto por {revisor}"; la de un Aceptado muestra un dot verde + "Aceptado" en vez
    del stepper completo de 4 barras — **implementado**.
  - `components/revision.css`: grid de la fila pasa de 5 a 7 columnas; cada sección es su propia
    card (borde/radius/fondo) separada por `gap`; título de sección en `--accent-700` (las 4, no solo
    las de etapa); dot verde para Aceptado (`.adoc-estado-avance.exito .adoc-estado-dot`) —
    **implementado**.
  - `shared/ui/FiltrosLista.tsx` / `.css`: `CampoFiltroFijo` gana la variante `{ tipo: "select",
opciones }` (antes solo texto); `pages/TableroRevisionPage.tsx` mueve **Tipo** de opcional a
    fijo, junto a Nombre — **implementado**.
  - `api/pedidosSeed.ts`: 4 semillas de Alta + 4 de Baja nuevas, en varias etapas del circuito —
    **implementado**.
- **Sin cambios en el mockup** (`docs/product/designs/screens.pen`): esta iteración baja directo a
  código sin sincronizar `ebl4U` — queda como deuda de mockup a resolver más adelante.
- **Specs**: `openspec/specs/tablero-revision-tabla/spec.md` y
  `openspec/specs/pedidos-designacion/spec.md` (deltas, ver arriba). Nota: ambos specs base todavía
  reflejan el estado pre-`rediseno-form-pedido-designaciones`/pre-`rediseno-revision-solo-grilla` (esos
  dos changes siguen sin archivar) — los deltas de este change se escriben asumiendo el estado
  ya vigente en código (Tabla única, sin Kanban; datos del docente con horas/materias múltiples), no
  el texto literal todavía en `openspec/specs/`.
- **Sin impacto en backend**: sigue siendo store mock + `localStorage`.
- **Rollback**: cambio acotado al frontend, sin migraciones de datos; revertir el PR restaura la Tabla
  plana original (sin filtros, sin legajo/tipo/fecha, sin secciones, con el prefijo "Prof.").
