## Why

Cuatro ajustes puntuales, pedidos directamente por el cliente, sobre dos pantallas ya existentes del
módulo Designaciones (`pedidos-designacion` y `tablero-revision-tabla`): al form de pedido le falta
capturar si el docente es agente externo (dato que hoy no existe en el modelo), a la Tabla de revisión
le falta un filtro por carrera para que Secretaría/Decanato/Administración —que ven **todo el
departamento**, no solo una carrera [BR-designaciones-009]— puedan acotar el volumen de pedidos, el
radio "Sin novedad" del form permite crear manualmente algo que en la práctica solo debería surgir de
la precarga automática del período, y un pedido Devuelto se pierde entre el resto de las filas de la
Tabla de revisión (mismo peso visual que cualquier otro estado, sin nada que llame la atención sobre
él). Los cuatro son ediciones acotadas sobre pantallas y specs vigentes, no una feature nueva.

## What Changes

- **Botón "Docente es agente externo" junto a Horas externas** (`pedidos-designacion`): nuevo checkbox
  `esAgenteExterno` en la sección "Designación solicitada" del form de pedido, al lado del campo "Horas
  externas (otro depto.)" — visible en "Alta" y "Cambio de cargo o dedicación" (las dos novedades que ya
  muestran esa sección), persistido en el pedido y reflejado en el resumen de detalle
  (`ResumenPedido.tsx`), igual que "Horas externas".
- **Filtro de propuesta (carrera) en Revisión** (`tablero-revision-tabla`): nuevo filtro opcional
  **Propuesta** en `FiltrosLista` de `/designaciones/revision` — `Select` de carrera, mismo patrón que
  Legajo/Prioridad (vía "+ Añadir filtro"). El campo `carrera` ya existe en `PedidoDesignacion`, pero
  hoy no es filtrable ni se muestra en ninguna columna. Opciones del selector: catálogo cerrado de 5
  carreras que pasó el cliente ("por ahora") — Ingeniería en Informática¹, Ingeniería Industrial,
  Ingeniería Civil, Ingeniería Mecánica e Ingeniería Electrónica.
  - ¹ El cliente escribió "Ingeniería Informática"; se usa el nombre exacto que ya existe en
    `pedido.carrera` en seeds/tests (`"Ingeniería en Informática"`) para que el filtro matchee los
    pedidos reales — a confirmar en la revisión de esta propuesta si el nombre debe cambiar en todo el
    sistema (alcance mucho mayor, no incluido acá).
- **Columna "Carrera" en la Tabla de revisión** (`tablero-revision-tabla`): nueva columna que muestra el
  **nombre abreviado** de la carrera del pedido (mismo campo `carrera` que usa el filtro Propuesta),
  entre **Legajo** y **Asignatura**. Mapeo cerrado nombre completo → abreviatura, provisto por el
  cliente: "Ingeniería en Informática" → **Informática**, "Ingeniería Industrial" → **Industrial**,
  "Ingeniería Civil" → **Civil**, "Ingeniería Mecánica" → **Mecánica**, "Ingeniería Electrónica" →
  **Electrónica**. Se agrega en las 4 secciones de la Tabla (mismo head de columnas en cada una).
- **BREAKING: eliminar el concepto "Sin novedad"** (`pedidos-designacion`, `tablero-revision-tabla`):
  por decisión explícita del cliente (confirmado tras aclarar el alcance), no es solo sacar la opción
  del radio — es sacar la novedad "Sin novedad" de **todo** el sistema: el radio "Tipo de novedad" del
  form queda con 3 opciones (Alta / Baja / Cambio de cargo o dedicación); el filtro Tipo de "Mis
  pedidos" y de "Revisión" pierden la opción "Sin novedad"; `NovedadChip` y el mapeo de
  `ModalConfirmacionAccion` pierden esa entrada; la **precarga automática de docentes del período
  anterior como "Sin novedad"** (seed `pedidosSeed.ts`, y el requirement de spec que la describe) se
  elimina — un docente que continúa sin cambios **deja de tener, en esta pasada, un mecanismo para
  reconfirmarse** en el sistema. Esto último es un cambio de comportamiento de negocio, no solo de UI;
  queda documentado como nota abierta más abajo para que el cliente lo confirme al revisar.
- **Devolución más llamativa en la Tabla de revisión** (`tablero-revision-tabla`): la celda Estado de un
  pedido `devuelto` hoy solo cambia el color del texto (`--color-status-warning-fg`), mismo peso visual
  que cualquier otra fila — pasa a incorporar el mismo lenguaje de badge lleno que ya usa
  `StatusBadge kind="devuelto"` en "Mis pedidos" (fondo `--warning-100` / texto `--warning-700` / borde
  `--warning-500` + ícono), sin perder el detalle ya construido (stepper de la etapa de retorno +
  "Devuelto por {nombre} ({rol})") — se re-usa el lenguaje visual existente del design system en vez de
  inventar uno nuevo.

## Capabilities

### New Capabilities

_(ninguna)_

### Modified Capabilities

- `pedidos-designacion`:
  - MODIFIED "Horas de investigación y horas externas del pedido" → suma el campo `esAgenteExterno`
    junto a horas externas, en Alta y Cambio; **cuarta ronda**: suma el `Select` "Departamento a
    cargo" (`departamentoAgenteExterno`, catálogo cerrado de 7 opciones), obligatorio cuando
    `esAgenteExterno` es verdadero.
  - MODIFIED "Secciones condicionales por novedad" → el radio "Tipo de novedad" pasa de 4 a 3 opciones
    (se saca "Sin novedad").
  - MODIFIED "Materias y horas del pedido" → se retira el escenario/comportamiento específico de "Sin
    novedad" (materia vigente de solo lectura sin sección de solicitud); **segunda y tercera ronda**:
    ni Alta ni Cambio exigen ya un mínimo de 1 materia (regla de negocio nueva, extendida a Cambio en
    la tercera ronda) — Baja sigue exigiéndolo.
  - MODIFIED "Resumen de cambios en el panel de datos actuales (Cambio)" → se retira la mención a "Sin
    novedad" como caso sin transiciones (ya no existe esa novedad).
  - MODIFIED "Lista 'Mis pedidos' del Jefe de Cátedra" → se retira el escenario "Precarga de docentes
    del período anterior" (la precarga automática como "Sin novedad" desaparece) y "Sin novedad" sale
    del filtro opcional Tipo; **segunda ronda**: los botones Ver/Editar/Eliminar pasan de
    condicionales a fijos-pero-deshabilitados.
- `tablero-revision-tabla`:
  - ADDED "Filtro de carrera en la Tabla de revisión" (nombrado "Filtro de propuesta" en la primera
    ronda, renombrado en la segunda tras el pedido de unificar rótulos con la columna).
  - MODIFIED "Vista Tabla del tablero de revisión" → suma la columna **Carrera** (nombre abreviado,
    entre Legajo y Asignatura) en las 4 secciones; el filtro fijo "Tipo" pierde la opción "Sin
    novedad"; **segunda ronda**: los pedidos prioritarios y devueltos marcan la **fila entera** (rojo/
    amarillo, prioritario gana si son ambos) en vez de solo la celda Estado; **quinta ronda**: la
    columna Prioritario suma un ícono de flechita ámbar para devuelto, en un casillero fijo propio,
    junto al de la bandera de prioridad.

## Impact

- **Frontend** (`frontend/src/features/designaciones/`):
  - `types.ts`: `Novedad` pierde `"Sin novedad"`; `DatosEditablesPedido`/`PedidoDesignacion` suman
    `esAgenteExterno: boolean`; **cuarta ronda**: nuevo tipo `DepartamentoAgenteExterno` (unión cerrada
    de 7 literales) y campo opcional `departamentoAgenteExterno` en ambas interfaces.
  - `components/PedidoForm.tsx`: `NOVEDADES` pasa a `["Alta", "Baja", "Cambio de cargo o dedicación"]`;
    el default de un pedido nuevo deja de ser `"Sin novedad"` (pasa a requerir selección explícita o a
    `"Alta"` — se define en `design.md`); rama `esSinNovedad` se elimina.
  - `components/SeccionDesignacionSolicitada.tsx`: nuevo `Checkbox` "Docente es agente externo" junto al
    `Field` de "Horas externas (otro depto.)"; nueva prop `esAgenteExterno`/`onEsAgenteExterno`;
    **cuarta ronda**: `Select` "Departamento a cargo" condicional (solo si `esAgenteExterno`), catálogo
    `DEPARTAMENTOS_AGENTE_EXTERNO` (nuevo, en `api/catalogos.ts`).
  - `pedidoValidacion.ts`: **cuarta ronda** — `esAgenteExterno` sin `departamentoAgenteExterno` marca
    error; `PedidoForm.tsx#cambiarEsAgenteExterno` limpia el departamento al desmarcar el checkbox.
  - `components/DatosActualesPanel.tsx`: la prop `mostrarMateria` (solo usada por "Sin novedad") queda
    sin caller — se limpia si el análisis de impacto en `design.md` confirma que no tiene otro uso.
  - `components/ResumenPedido.tsx`, `components/NovedadChip.tsx`,
    `components/ModalConfirmacionAccion.tsx`: pierden la entrada `"Sin novedad"` de sus mapas por
    novedad; `ResumenPedido` suma la fila "Agente externo" junto a "Horas externas".
  - `pages/MisPedidosPage.tsx`, `pages/TableroRevisionPage.tsx`: `NOVEDADES`/opciones del filtro Tipo
    pierden `"Sin novedad"`.
  - `pages/TableroRevisionPage.tsx`, `components/filtrosTablero.ts`: nuevo filtro opcional
    **Carrera** (`filtros.carrera`, renombrado de `propuesta` en la segunda ronda), catálogo cerrado
    de 5 opciones.
  - `components/TablaRevision.tsx`, `components/tableroRevisionModelo.ts`: nueva columna **Carrera**
    (entre Legajo y Asignatura) en las 4 secciones, con nombre abreviado vía un mapeo cerrado
    nombre completo → abreviatura (mismo archivo/patrón que otros catálogos de la feature).
  - `components/revision.css`: grid de columnas pasa de 7 a 8; **segunda ronda**: fondo de fila
    completo rojo/amarillo (`.adoc-tabla-row--prioritario`/`--devuelto`) para prioritario/devuelto —
    reemplaza el badge lleno de la celda Estado de la primera ronda; **quinta ronda**: columna
    Prioritario pasa de `44px` a `60px`, con dos casilleros fijos (`.adoc-tabla-prio-slot`).
  - `components/EstadoAvance.tsx`: sin cambios de contenido; la clase `alerta` vuelve a teñir solo el
    texto (el fondo lleno se movió a la fila, ver arriba).
  - `components/NovedadChip.tsx`, `components/TablaRevision.tsx`: **quinta ronda** — nuevo
    `DevueltoFlechaIcono` (ícono `corner-up-left`, mismo que usa `EstadoPedidoPill` para "Devuelto" en
    Mis Pedidos), junto a `PrioridadFlagIcono` en la columna Prioritario.
  - `api/pedidosSeed.ts`: las 2 semillas de precarga "Sin novedad" se recastean a "Cambio de cargo o
    dedicación" (no se eliminan — ver D-3 en `design.md` para el motivo del cambio de plan);
    `api/catalogos.ts` pierde su comentario sobre "Sin novedad".
  - `pedidoValidacion.ts`, `components/SeccionMateriasHoras.tsx`, `components/
SeccionDesignacionSolicitada.tsx`, `PedidoForm.tsx`: **segunda y tercera ronda** — ni Alta ni
    Cambio exigen mínimo 1 materia (Baja sí); `permiteVacio`/`esAlta` (segunda ronda, solo Alta) se
    eliminaron en la tercera al extender la regla a Cambio, junto con el guard de `quitarMateria`.
  - `components/TablaMisPedidos.tsx`, `pages/misPedidos.css`: **segunda ronda** — Ver/Editar/Eliminar
    siempre se renderizan; `disabled` (vía `puedeEditarPedido`/`puedeEliminarPedido`) en vez de
    condicionales, con estilo semitransparente (`opacity: 0.4`).
  - Tests afectados (no exhaustivo): `PedidoForm.test.tsx`, `pedidoValidacion.test.ts`,
    `maquinaEstados.test.ts`, `detalleAdapters.test.ts`, `EstadoAvance.test.tsx`,
    `ModalConfirmacionAccion.test.tsx`, `TablaRevision.test.tsx`, `tableroRevisionModelo.test.ts`,
    `pedidosApi.test.ts`, `filtrosTablero.test.ts` (nuevo), `MisPedidosPage.test.tsx`,
    `flujoAprobacion.test.tsx`.
- **Sin impacto en backend**: sigue siendo store mock + `localStorage`; no hay endpoint ni contrato
  cross-module involucrado.
- **Sin impacto en el grafo de dependencias**: cambios acotados a `frontend/src/features/designaciones/`.
- **Rollback**: cambio acotado al frontend de un módulo, sin migraciones de datos — revertir el/los PR
  restaura el radio de 4 opciones, la precarga "Sin novedad", el form sin el checkbox de agente externo,
  la Tabla de revisión sin filtro de Carrera y sin el fondo de fila rojo/amarillo, Alta/Cambio
  exigiendo mínimo 1 materia, y el form sin el selector de departamento a cargo del agente externo.

## Estado (segunda ronda)

El cliente revisó la primera pasada y pidió 4 ajustes adicionales — **todos implementados**:

- ~~**Devuelto: badge lleno solo en la celda Estado**~~ — **superado**: el cliente pidió marcar **la
  fila entera**, no solo la celda. Ídem para **prioritario** (que antes solo tenía el ícono de
  bandera en su columna, sin fondo de fila) — pedido nuevo, no estaba en el alcance original. Ahora
  `.adoc-tabla-row` recibe un modificador de fondo completo: **rojo** (`--danger-100`) si
  `prioritario`, si no **amarillo** (`--warning-100`) si `estado === "devuelto"`. Si un pedido es
  ambas cosas, **gana rojo** (prioritario) — confirmado con el cliente. La celda Estado vuelve a ser
  texto de color plano (ya no pill) — la fila entera es la señal, evita duplicar el mismo aviso dos
  veces. El ícono de bandera en la columna Prioritario se mantiene (no es redundante con el fondo,
  sigue siendo útil al escanear esa columna puntual).
- **Materias opcionales en Alta** — **implementado**, regla de negocio nueva (no estaba en el pedido
  original): el docente se puede dar de alta solo con cargo y dedicación, sin ninguna materia
  asignada todavía — se le asignan después. Antes el sistema exigía como mínimo 1 asignación en
  cualquier novedad. Ahora Alta puede quedar con 0 filas (el botón "Quitar" queda disponible incluso
  en la última fila, a diferencia de Baja/Cambio, que siguen exigiendo mínimo 1).
- **Filtro renombrado de "Propuesta" a "Carrera"** — **implementado**: resuelve la nota abierta de la
  primera ronda (el filtro decía "Propuesta" y la columna "Carrera", dos rótulos para el mismo dato).
  Ahora dicen lo mismo en toda la pantalla; la clave interna del filtro (`filtros.propuesta` →
  `filtros.carrera`) se renombró en el mismo cambio para no dejar el código desalineado con la UI.
- **Botones Ver/Editar/Eliminar fijos en "Mis pedidos"** — **implementado**: antes "Editar" y la X de
  "Eliminar" desaparecían de la fila cuando el pedido no era editable/eliminable (layout saltaba de
  fila a fila). Ahora los 3 botones son siempre visibles, en la misma posición; cuando la acción no
  aplica, el botón queda **deshabilitado** (semitransparente, `opacity: 0.4`, mismo estilo `ghost`)
  en vez de ocultarse.

## Estado (tercera ronda)

- **Materias opcionales también en Cambio** — **implementado**: el cliente pidió extender la regla de
  la segunda ronda (materias opcionales en Alta) a "Cambio de cargo o dedicación" — también se tiene
  que poder borrar la materia hasta dejar la lista vacía. Como en la práctica las dos novedades
  comparten el mismo componente editable (`SeccionDesignacionSolicitada`), la regla terminó siendo una
  sola para ambas: `validarPedido` ya no exige materias en Alta ni en Cambio (**Baja sigue
  exigiéndolas** — su listado refleja lo que el docente ya tiene, no se tocó); se aprovechó para
  simplificar el código de la segunda ronda (`permiteVacio`/`esAlta` quedaron sin uso y se eliminaron
  en vez de dejarse condicionados). Ver D-8 en `design.md`.

## Estado (cuarta ronda)

- **Selector de departamento a cargo del agente externo** — **implementado**: al marcar "Docente es
  agente externo" (Alta/Cambio), aparece un nuevo `Select` **"Departamento a cargo"** con un catálogo
  cerrado de 7 opciones que pasó el cliente: Departamento de Arquitectura, de Salud, de Derecho, de
  Económicas, de Humanidades, de Odontología, y Secretaría Académica. El selector solo aparece (y solo
  se exige) cuando el checkbox está marcado — al desmarcarlo, el valor se limpia. Nuevo campo
  `departamentoAgenteExterno` (tipo `DepartamentoAgenteExterno`, unión cerrada de las 7 opciones) en
  `PedidoDesignacion`/`DatosEditablesPedido`, junto a `esAgenteExterno` (D-2). Ver D-10 en `design.md`.

## Estado (quinta ronda)

- **Flechita de devuelto junto a la bandera de prioridad** — **implementado, corregido en la sexta
  ronda**: la columna Prioritario de la Tabla de revisión (hoy solo mostraba la bandera roja cuando el
  pedido era prioritario) suma un segundo ícono — una flechita ámbar (`corner-up-left`, el mismo que ya
  usa `EstadoPedidoPill` para "Devuelto" en Mis Pedidos) — cuando el pedido está devuelto. Primer
  intento: dos casilleros fijos (uno por ícono). El cliente lo corrigió en la sexta ronda — ver abajo.

## Estado (sexta ronda)

- **Posicionamiento de la bandera/flechita corregido** — **implementado**: el cliente marcó que los
  casilleros fijos de la quinta ronda "no están bien posicionados" y aclaró la idea real: **un solo
  ícono siempre centrado** (misma posición sea cual sea) y, **con los dos**, un espacio en el medio con
  uno de cada lado (bandera a la izquierda, flechita a la derecha) — no dos casilleros fijos, uno de
  los cuales quedaba vacío y corría el ícono solo hacia un costado. Se sacaron los casilleros: ahora
  los dos íconos son hijos condicionales directos de la celda, que ya centraba su contenido — con uno
  o dos, el centrado de flexbox los deja exactamente donde el cliente pidió, sin lógica de
  posicionamiento propia. Ver D-11 en `design.md` (revisado).

## Notas abiertas para la revisión del cliente

Las 4 quedaron **resueltas** tras la revisión del cliente:

- ~~**Consecuencia de negocio de eliminar "Sin novedad"**~~ — **confirmado**: el cliente aclaró que
  "Sin novedad" no se va a volver a usar nunca más, así que no hace falta ningún mecanismo de
  reemplazo para la reconfirmación automática — la eliminación queda como alcance final, no parcial.
- ~~**Nombre de la carrera "Ingeniería en Informática" vs. "Ingeniería Informática"**~~ — **confirmado
  irrelevante**: el nombre es a modo informativo, no hace falta unificarlo. Se mantiene
  `"Ingeniería en Informática"` (el que ya usa el código).
- ~~**Catálogo de 5 carreras "por ahora"**~~ — **confirmado que no molesta**: el cliente aclaró que la
  lista completa de carreras va a depender del backend más adelante; 5 por ahora está bien.
- ~~**Header de la columna nueva: "Carrera" vs. "Propuesta"**~~ — **resuelto en la segunda ronda**: el
  cliente pidió unificar; el filtro pasó a llamarse "Carrera" también.
