## Why

La pantalla "Mis pedidos" acumuló fricción de UX y datos mock poco realistas: el nombre del docente
lleva un prefijo "Prof." redundante (la tabla ya dice "Docente"), el filtro es un buscador de texto
libre combinado en vez de campos separados (a diferencia de la pantalla de Usuarios, que ya resolvió
esto bien), la columna "Novedad" no usa el término que el cliente prefiere ("Tipo"), y el menú kebab
de 3 puntos mezcla acciones de navegación (ver detalle), edición y transiciones de estado (enviar,
cancelar) en un solo lugar poco descubrible. Además, el seed mock tiene 11 pedidos de ejemplo para la
cátedra del Jefe de Cátedra de prueba — una lista casi siempre vacía es el caso real (un JC recién
empieza a cargar pedidos de a uno), no una tabla llena cubriendo los 7 estados posibles.

## What Changes

- **Sin prefijo "Prof."**: la columna Docente de "Mis pedidos" muestra `{nombre}` en vez de
  `Prof. {nombre}`. Acotado a esta tabla — no se toca `ModalConfirmacionAccion.tsx` (fuera de esta
  pantalla).
- **Columna "Novedad" → "Tipo"**: mismo dato, nuevo header/label en toda la pantalla (tabla y filtro).
- **Filtro al estilo Usuarios**: reemplaza el buscador combinado + `Select` de estado fijo por dos
  campos de texto siempre visibles (**Docente**, **N°**) más un mecanismo "+ Añadir filtro" para
  agregar **Tipo** y **Estado** como filtros opcionales — mismo patrón visual y de interacción que
  `FiltrosUsuarios.tsx` (`features/usuarios/`), adaptado a los campos de un pedido.
- **Fila clickeable → detalle; botón "Editar" explícito**: se elimina el menú kebab de 3 puntos
  (`MenuAccionesPedido.tsx`, sin otros consumidores). Click en cualquier parte de la fila navega al
  detalle del pedido (`/designaciones/pedidos/:id`). Un botón **"Editar"** visible al final de la fila
  (no en un menú) aparece solo cuando el pedido es editable (borrador, o devuelto del que el JC es
  propietario — mismo guard `puedeEditarPedido` de siempre).
- **"Enviar a revisión" se muda al form de edición/creación**: ya no es una acción de la lista.
  `PedidoForm.tsx` suma un botón **"Guardar y enviar"** (crear/borrador) o **"Guardar y reenviar"**
  (editando un devuelto) junto a "Guardar pedido" — guarda los datos y, en el mismo paso, dispara la
  transición (`enviarPedido`/`reenviarPedido`).
- **"Cancelar" (pasar a `cancelado`) se elimina, sin reemplazo**: no forma parte de esta iteración. El
  botón "Cancelar" que ya tiene el form (descarta la edición y vuelve a la lista, sin guardar) no
  cambia de significado — nunca hizo la transición de dominio.
- ~~**Menos datos de ejemplo**: el seed de pedidos para la cátedra "Ingeniería de Software" (la del JC
  de prueba) baja de 11 a 4~~ — **revertido en la sexta ronda** (el cliente los necesitaba para
  probar): el seed vuelve a tener 11 pedidos de ejemplo para la cátedra + Mariano Tévez (otra carrera),
  ver más abajo. Se conserva el pedido de otra carrera (Mariano Tévez, "Ingeniería Industrial") — es el
  fixture que prueba el aislamiento por ámbito [BR-designaciones-009] en `pedidosApi.test.ts`, no se
  toca.

### Segunda ronda: acciones de fila al formato de Usuarios, Eliminar, y Volver

- **Botones "Ver"/"Editar" con el mismo formato que la pantalla Usuarios**: se reemplazan por
  `Button` (`variant="ghost"`, `size="sm"`, solo texto, sin ícono) — el mismo patrón visual que ya usa
  `features/usuarios/` para sus acciones de fila. "Ver" es nuevo: navega al detalle igual que el click
  en la fila (mismo destino, dos formas de llegar). "Editar" se reutiliza tal cual (mismo guard
  `puedeEditarPedido`), solo cambia de formato.
- **Nueva acción "Eliminar" para pedidos en `borrador`**: una X roja al final de la fila (ícono, sin
  texto, `aria-label` descriptivo) borra el pedido definitivamente, con confirmación por modal
  (`ModalEliminarPedido`, mismo patrón que `ModalEliminarPeriodo`). A diferencia de "Editar", **no**
  aplica a `devuelto` — un devuelto ya tiene una revisión asociada en su historial, no es un borrador
  "nunca enviado". La misma acción está disponible dentro del detalle del pedido
  (`/designaciones/pedidos/:id`) cuando el pedido es un borrador propio del Jefe de Cátedra, junto al
  botón Volver.
- **El botón Volver del detalle navega a la pantalla anterior real (`navigate(-1)`), no a una ruta
  fija**: corrige un supuesto de `rediseno-revision-solo-grilla` (D-3) que dejó de valer al agregar la
  fila clickeable de "Mis pedidos" hacia el mismo detalle — un JC que entra desde "Mis pedidos" y hace
  click en Volver debe volver a "Mis pedidos", no a la Tabla de revisión (una pantalla a la que ni
  siquiera tiene acceso). El requirement y las specs de ese cambio quedan amendados en su propio change
  (ver `rediseno-revision-solo-grilla/specs/aprobacion-pedidos-designacion/spec.md`); acá solo se
  documenta el impacto en el flujo de "Mis pedidos".
- **Filtro Docente más angosto**: el campo de texto "Docente" del filtro pasa de `flex-basis: 220px` a
  `150px`, igualando el ancho del campo equivalente en `FiltrosUsuarios.tsx` — el campo original era
  desproporcionadamente largo para el dato que contiene (un nombre).

### Tercera ronda: filtro genérico reutilizable + Legajo

- **El bloque de filtro se extrae a un componente genérico y reutilizable**: `shared/ui/FiltrosLista.tsx`
  reemplaza la implementación de filtro propia de "Mis pedidos" (campos fijos + "+ Añadir filtro"
  config-driven por una lista de campos, en vez de JSX/estado hardcodeado por pantalla). Queda listo
  para que otras pantallas lo adopten a futuro (el pedido explícito del cliente); no se migra
  `FiltrosUsuarios.tsx` a este componente en este change — sigue con su implementación propia, la
  migración queda como trabajo futuro fuera de este alcance.
- **Nuevo campo Legajo**: se agrega `legajo` al docente del pedido (`DocentePedido`/`DocenteExistente`),
  poblado en el catálogo mock y en el seed. La tabla de "Mis pedidos" suma una columna **LEGAJO** (junto
  a Docente), y el filtro suma **Legajo** como filtro opcional (mismo patrón que Tipo/Estado, detrás de
  "+ Añadir filtro" — igual que en `FiltrosUsuarios.tsx`, donde Legajo también es opcional). Una Alta
  (docente nuevo) puede no tener legajo todavía — el campo es opcional en `DocentePedido`, la tabla
  muestra "—".
- **Legajo obligatorio para Baja y Cambio [BR-designaciones-018]**: un pedido con novedad "Baja" o
  "Cambio de cargo o dedicación" MUST tener un docente con legajo asignado antes de poder guardarse
  (ambas novedades operan sobre un docente ya existente en el sistema). "Alta" queda exceptuada — el
  docente todavía no existe, se lo asigna el sistema/RRHH después. Nueva regla registrada en
  `docs/business-rules/designaciones.md`.

### Cuarta ronda: re-seed del legajo en navegadores con datos viejos + Editar en el detalle

- **Bump de versión del store mock**: la clave de `localStorage` pasa de `adoc.mock.pedidos.v2` a
  `.v3` para forzar un re-seed limpio — el seed ya tenía `legajo` correctamente poblado desde la
  segunda ronda, pero cualquier navegador que hubiera cargado la app antes de ese cambio seguía viendo
  su copia vieja persistida (sin legajo), porque el store solo siembra si `localStorage` está vacío.
  Mismo mecanismo ya usado para el bump v1→v2 (SCRUM-8).
- **Botón "Editar" en el detalle del pedido**: un gap real — el detalle (`/designaciones/pedidos/:id`)
  nunca tuvo forma de editar un borrador/devuelto propio del Jefe de Cátedra, solo "Mis pedidos" la
  tenía. Se agrega un botón "Editar" (junto a Volver y, si aplica, Eliminar) gateado por el mismo
  `puedeEditarPedido` — visible en `borrador` y en `devuelto` del propietario (a diferencia de
  Eliminar, que solo aplica a `borrador`). El requirement correspondiente vive en
  `rediseno-revision-solo-grilla` (capability `aprobacion-pedidos-designacion`, dueña de esa pantalla),
  amendado en su propio change.

### Sexta ronda: se restauran los 7 pedidos de ejemplo retirados

- **Reversión de la reducción de seed**: el cliente pidió expresamente recuperar los pedidos que se
  habían sacado ("los necesito") — la reducción a 4 (más Mariano Tévez) fue una decisión de esta misma
  sesión, no un pedido del cliente en sí; al no servirle para lo que necesitaba probar, se revierte.
  Se restauran los 7: **Diego Morales** (`Sin novedad`, borrador), **Sofía Romano** (`Baja`, borrador —
  único ejemplo de esta novedad en el seed), **Martín Acosta** (`Cambio`, borrador), **Lucía Fernández**
  (`Cambio`, `en_revision_coordinador`), **Florencia Cabrera** (`Cambio`, `en_revision_secretaria`),
  **Hernán Vidal** (`Cambio`, `en_revision_decanato`) y **Gabriel Núñez** (`Cambio`, `en_lote`) — el
  seed de la cátedra "Ingeniería de Software" vuelve a tener 11 entradas (+ Mariano Tévez de otra
  carrera), cubriendo los 7 estados posibles como el seed original pre-`mis-pedidos-simplificado`.
  Los que coinciden con una entrada de `DOCENTES_EXISTENTES` (Diego, Sofía, Lucía, Gabriel) reusan su
  DNI/legajo/cargo/dedicación/materia del catálogo, para que sea el mismo docente en ambos lados.
- **Bump de versión del store mock**: `adoc.mock.pedidos.v3` → `.v4`, mismo mecanismo que las rondas
  anteriores — sin el bump, un navegador que ya cargó la app con el seed reducido seguiría viéndolo.

## Capabilities

### New Capabilities

_(ninguna — "Eliminar un pedido en borrador" se agrega como requirement ADDED dentro de la capability
existente `pedidos-designacion`, no como capability nueva)_

### Modified Capabilities

- `pedidos-designacion`: el requirement "Lista 'Mis pedidos' del Jefe de Cátedra" se reescribe (nombre
  sin prefijo, columna Tipo, columna Legajo, filtro por Docente/N°/Legajo/Tipo/Estado, fila clickeable,
  botones Ver/Editar formato Usuarios, X roja de Eliminar en borradores, sin acciones de
  enviar/cancelar en la lista); se agrega un requirement nuevo, "Enviar y reenviar desde el form de
  pedido", que documenta el nuevo botón combinado; se agrega un requirement nuevo, "Eliminar un pedido
  en borrador", que documenta la X roja y su equivalente en el detalle. El comportamiento del botón
  Volver del detalle (`navigate(-1)`) se documenta en `rediseno-revision-solo-grilla` (capability
  `aprobacion-pedidos-designacion`), no acá — es la capability dueña de esa pantalla. El componente
  genérico `FiltrosLista` es una decisión de implementación (ver `design.md`), no introduce ni modifica
  ningún requirement por sí solo — el comportamiento observable del filtro de "Mis pedidos" es el mismo
  patrón ya especificado, solo suma Legajo. Se agrega también un requirement MODIFIED, "Adjuntos y
  justificación obligatorios por novedad" (título igual al de la spec base), extendido con la regla de
  legajo obligatorio para Baja/Cambio [BR-designaciones-018].

## Impact

- **Frontend** (`frontend/src/features/designaciones/`):
  - `components/TablaMisPedidos.tsx`: sin prefijo "Prof.", header "TIPO", fila clickeable
    (`onClick`/`role="button"` o similar accesible), botones "Ver" (nuevo) y "Editar" con formato
    `Button variant="ghost" size="sm"` (igual que Usuarios), X roja "Eliminar" condicional a
    `puedeEliminarPedido`, sin columna de kebab.
  - `components/MenuAccionesPedido.tsx` + su lugar en `TablaMisPedidos.tsx`: **eliminado** (sin otros
    consumidores).
  - `components/ModalEliminarPedido.tsx` (**nuevo**): modal de confirmación de Eliminar, mismo patrón
    que `ModalEliminarPeriodo.tsx`.
  - `components/lucide.tsx`: ícono `IconoX` (nuevo).
  - `api/maquinaEstados.ts`: predicado nuevo `puedeEliminarPedido(pedido, actor)` — `borrador` +
    actor Jefe de Cátedra únicamente (excluye `devuelto`, a diferencia de `puedeEditarPedido`).
  - `api/pedidosStore.ts`: función nueva `eliminar(id)` — saca el pedido del store (no es una
    transición de estado registrada en el historial).
  - `api/pedidosApi.ts`: función nueva `eliminarPedido(id, actor)` — valida `borrador` + rol JC vía
    `ErrorDominioPedido`, delega en `store.eliminar`.
  - `hooks/useAccionesPedido.ts`: hook nuevo `useEliminarPedido(actor)`.
  - `pages/MisPedidosPage.tsx`: nuevo bloque de filtro (Docente/N° fijos, Tipo/Estado opcionales, +
    Añadir filtro), sin el `Select` de estado ni el buscador combinado anteriores; ya no pasa
    `onEnviar`/`onCancelar` a la tabla; agrega `onEliminar` + estado del modal de confirmación.
  - `pages/DetallePedidoPage.tsx`: botón "Eliminar" (junto a Volver) visible cuando
    `puedeEliminarPedido(pedido, actor)`, con el mismo `ModalEliminarPedido`; al confirmar, navega a
    "Mis pedidos". El botón "Volver" pasa de un link fijo a `navigate(-1)` (ver nota de
    `rediseno-revision-solo-grilla` más arriba).
  - `components/PedidoForm.tsx` + `pages/PedidoFormPage.tsx`: botón "Guardar y enviar"/"Guardar y
    reenviar" (usa `useEnviarPedido`/`useReenviarPedido`, ya existentes); `PedidoForm.tsx` además
    propaga `legajo` al seleccionar un docente existente.
  - `api/pedidosSeed.ts`: `SEMILLAS` de "Ingeniería de Software" de 11 a 4 entradas; `SemillaPedido`
    suma `legajo?`.
  - `api/catalogos.ts`: `DOCENTES_EXISTENTES` suma `legajo` (obligatorio, un docente existente siempre
    lo tiene).
  - `types.ts`: `DocentePedido.legajo?` (opcional — una Alta puede no tenerlo todavía);
    `DocenteExistente.legajo` (obligatorio).
  - `components/TablaMisPedidos.tsx`: columna nueva **LEGAJO** (mono, junto a Docente; "—" si falta).
  - `components/filtrosMisPedidos.ts`: `FiltrosMisPedidosState` suma `legajo` (+ índice de string, para
    poder pasarse a `FiltrosLista`); `aplicarFiltrosMisPedidos` filtra por legajo (contiene, sin
    acentos/mayúsculas).
  - CSS: `pages/misPedidos.css` (grilla con la columna Legajo, fila clickeable, botones Ver/Editar
    formato Usuarios, X roja de Eliminar); el CSS del bloque de filtro se **mudó** a
    `shared/ui/FiltrosLista.css` (clases `.adoc-filtros-*`, ya no `.adoc-mp-filtro-*`).
  - `shared/ui/FiltrosLista.tsx` (**nuevo**) + `shared/ui/FiltrosLista.css` (**nuevo**): componente
    genérico y reutilizable del bloque de filtro (campos fijos + "+ Añadir filtro" config-driven),
    reemplaza la implementación propia que tenía `MisPedidosPage.tsx`.
  - `pedidoValidacion.ts`: nueva validación — Baja/Cambio exigen `docente.legajo` [BR-018].
  - `api/pedidosStore.ts`: clave de `localStorage` `adoc.mock.pedidos.v2` → `.v3` (fuerza re-seed en
    navegadores con datos viejos, sin `legajo`).
  - `pages/DetallePedidoPage.tsx`: botón "Editar" (`variant="secondary"`, ícono `IconoSquarePen`) junto
    a Volver, condicional a `puedeEditarPedido(pedido, actor)` — navega a
    `/designaciones/pedidos/:id/editar`.
  - `api/pedidosSeed.ts`: `SEMILLAS` de "Ingeniería de Software" vuelve de 4 a 11 entradas (restaura
    Diego Morales, Sofía Romano, Martín Acosta, Lucía Fernández, Florencia Cabrera, Hernán Vidal,
    Gabriel Núñez).
  - `api/pedidosStore.ts`: clave de `localStorage` `adoc.mock.pedidos.v3` → `.v4` (fuerza re-seed).
  - `pages/MisPedidosPage.test.tsx`: se quita la aserción de conteo total de botones "Editar"
    (`getAllByRole(...).length === 2`) — quedó obsoleta y brittle al crecer el seed; las aserciones
    por fila (borrador/devuelto sí, en revisión/rechazado no) ya cubrían la regla sin depender del
    tamaño del seed.
- **Business rules**: `docs/business-rules/designaciones.md` — nueva `BR-designaciones-018` (mismo
  formato que BR-001..004: `pedidoValidacion.ts` + `pedidoValidacion.test.ts`, cita normativa
  pendiente con el cliente); índice regenerado (`pnpm generate-indexes`).
- **Specs**: `rediseno-revision-solo-grilla/specs/aprobacion-pedidos-designacion/spec.md` (delta del
  botón Editar en el detalle, en su propio change — es la capability dueña de esa pantalla).
- **Sin cambios en el mockup** (`docs/product/designs/screens.pen`): esta iteración baja directo a
  código sin sincronizar el mockup — queda como deuda, igual que los changes anteriores de esta
  sesión.
- **Specs**: `openspec/specs/pedidos-designacion/spec.md` (delta, ver arriba);
  `rediseno-revision-solo-grilla/specs/aprobacion-pedidos-designacion/spec.md` (delta del botón
  Volver, en su propio change).
- **Sin impacto en backend**: sigue siendo store mock + `localStorage`.
- **Rollback**: cambio acotado al frontend, sin migraciones de datos; revertir el PR restaura el menú
  kebab, el filtro combinado, el seed de 11 pedidos, y (en `rediseno-revision-solo-grilla`) el botón
  Volver como link fijo.
