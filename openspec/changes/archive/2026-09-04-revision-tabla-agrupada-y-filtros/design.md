## Context

`TablaRevision.tsx` hoy renderiza una lista plana: aplana `construirColumnas(pedidos, actor)` (que ya
agrupa en 4 buckets — En revisión / Aceptados / Devueltos / Rechazados) con `.flatMap(...)`, y pinta
cada pedido como una fila `<button>` con el prefijo `Prof. {nombre}` hardcodeado. `filtrosTablero.ts`
tiene un `FiltrosTablero` con `vista`/`tipo`/`prioridad`, sin nada por nombre/legajo. `DocentePedido`/
`DocenteExistente` (`types.ts`) no tienen `legajo`, solo `dni`; el catálogo `DOCENTES_EXISTENTES`
(`catalogos.ts`) y las 12 semillas de `pedidosSeed.ts` tampoco.

La pantalla de Usuarios (`features/usuarios/components/FiltrosUsuarios.tsx`) ya resuelve un filtro por
texto libre (apellido/nombre/documento/legajo/mail) con inputs controlados + comparación normalizada
(minúsculas, sin acentos) — sirve de referencia de UX, aunque su mecanismo de "+ Añadir filtro" (para
4 campos opcionales) es más de lo que hace falta acá (solo 2 campos, siempre relevantes).

## Goals / Non-Goals

**Goals:**

- Agrupar la Tabla de revisión en 4 secciones colapsables, cada una separada visualmente de las
  demás (no apiladas) — **implementado**, con un criterio de agrupación **distinto** al planeado
  originalmente en D-1/D-2 (ver nota de superación ahí): no por estado de avance (En
  revisión/Aceptados/Devueltos/Rechazados), sino por **etapa del circuito** (En Coordinación/En
  Secretaría/En Decanato/Finalizados) — ver D-6/D-7. El default de expansión **no** es "las 4
  expandidas" (así arrancó en la quinta ronda) sino **solo la sección del rol del actor** — D-8.
- Sacar el prefijo "Prof." de la columna Docente de la Tabla de revisión (solo esa tabla) —
  **implementado**, desacoplado de la agrupación en secciones (que sigue pausada, ver bullet
  anterior): son dos cambios independientes en el mismo componente, no hacía falta esperar.
- Filtrar la Tabla por nombre o legajo del docente, con el mismo criterio de "contiene, sin
  distinguir mayúsculas/acentos" que ya usa Usuarios — **implementado**, ver D-3 (amendada).
- ~~Agregar `legajo` al modelo de docente de Designaciones con datos reales (catálogo + semillas)~~ —
  **ya implementado** por `mis-pedidos-simplificado` (D-10) antes de retomar este change, ver D-4/D-5.

**Non-Goals:**

- No se toca `TablaMisPedidos.tsx` ni `ModalConfirmacionAccion.tsx` (conservan "Prof. {nombre}") — el
  pedido del cliente fue específico de la Tabla de revisión.
- No se agrega un input de legajo al form de pedido (Alta sigue sin legajo — un docente nuevo no tiene
  legajo asignado todavía; es una limitación real, no una omisión).
- ~~No se replica el mecanismo "+ Añadir filtro" de Usuarios — con 2 campos fijos alcanza con dos
  `Input` siempre visibles~~ — **revertido**: el cliente pidió explícitamente paridad de patrón con
  Mis Pedidos (que sí usa "+ Añadir filtro"), ver D-3 amendada. Tipo y Prioridad pasan a ser
  opcionales también, no solo Nombre/Legajo.
- No se cambia el set de columnas de la fila al agrupar — sigue siendo `Docente`/`Legajo`/
  `Asignatura`/`Tipo`/`Fecha última actualización`/`Estado`/`Prioritario` (Legajo, Tipo y Fecha
  sumados en la tercera ronda) — se agrupa la MISMA tabla, no se rediseña. Único cambio de contenido
  en una celda: la de Estado de un pedido Devuelto pasa de decir "Devuelto" a "Devuelto por
  {revisor}" (D-7).

## Decisions

### D-1 y D-2: [SUPERADAS — ver D-6/D-7] Agrupar por estado de avance, reusando `construirColumnas(pedidos, actor)`

El plan original agrupaba por **estado de avance** — En revisión (toda la cadena junta) / Aceptados /
Devueltos / Rechazados — reusando la función `construirColumnas(pedidos, actor)` que ya existía (con
"tu turno" del actor como criterio de orden dentro de "En revisión"). Nunca se llegó a implementar
(quedó pausado). Cuando el cliente retomó el pedido, especificó un criterio de agrupación distinto —
**por etapa del circuito**, no por estado de avance — pensado explícitamente para que Secretaría
Académica, Administrativo y Decanato (que ven **todo el departamento**, a diferencia del Coordinador
que ve solo su carrera — BR-009) puedan triangular grandes volúmenes de pedidos por dónde están
trabados en la cadena. D-1/D-2 quedan como referencia histórica; el diseño vigente es D-6/D-7. El
mecanismo de sección desplegable en sí (`SeccionEstadoTabla`, `aria-expanded`, `useState<boolean>` en
`true`, patrón de `Sidebar.tsx#GrupoColapsable`) **se mantiene igual** — lo que cambió es qué pedidos
entran en cada sección y en qué orden, no cómo se despliega/colapsa una sección.

### D-6: Agrupación por etapa del circuito, no por estado de avance

**4 secciones**: **En Coordinación** / **En Secretaría** / **En Decanato** / **Finalizados**
(Aceptados + Rechazados juntos, sin sub-secciones). Un pedido **Devuelto** no tiene sección propia:
vive en la sección de la etapa a la que volvió, `pedido.etapaRetorno` — el dato que
`maquinaEstados.ts#devolver()` ya graba (`etapaRetorno: pedido.estado`, la etapa exacta donde quedó
trabado antes de devolverse). Es una relación 1:1 ya presente en el dominio: no hace falta inventar
un nuevo criterio, solo consumir un campo que ya existía y no se usaba en la vista Tabla.

Se descartó agrupar por `propietarioActual` (quién debe corregir) en su lugar: para una devolución
desde `en_revision_coordinador`, el propietario es el **Jefe de Cátedra** — un rol que no tiene
sección propia en esta tabla (piensa en carreras/departamento, no en cátedras) —, así que ese criterio
dejaría una porción de los devueltos sin dónde mostrarse. `etapaRetorno` siempre resuelve a una de las
3 etapas de revisión, cubre el 100% de los casos.

`construirColumnas` deja de tomar `actor` como parámetro — ya no hay criterio de "tu turno" en el
orden (ver D-7), y el gating por ámbito [BR-009] se sigue aplicando _antes_, sobre `pedidos`, en la
capa de datos — no dentro de esta función de presentación.

**Orden dentro de cada sección de etapa**: prioritarios primero, después devueltos, después el resto
— dentro de cada uno de esos 3 grupos, por fecha de última actualización **ascendente** (el pedido
que espera hace más tiempo, arriba). Se descartó mantener "tu turno" como criterio de orden (como
hacía D-1): con secciones ya particionadas por etapa, ese eje pierde fuerza — un Coordinador ve su
propia carrera únicamente, así que dentro de "En Coordinación" casi todo es "su turno" por
definición; y Administración puede actuar en cualquier etapa (`puedeRevisar` la deja pasar siempre),
así que tampoco discrimina filas ahí. El trío prioritario/devuelto/fecha sí discrimina en los 3 roles.

**Orden dentro de Finalizados**: Aceptados antes que Rechazados (dos bloques implícitos, sin
sub-headers — la columna Estado ya los distingue por color); dentro de cada bloque, por fecha
**descendente** (el cierre más reciente arriba) — a diferencia de las secciones de etapa (donde el
criterio es "el más viejo esperando, arriba"), acá no hay nada pendiente de resolver: es un registro
de lo ya cerrado, así que lo más reciente es lo más relevante para trazabilidad.

### D-7: Fila "Devuelto por {revisor}" en vez de solo "Devuelto"

La celda Estado (`EstadoAvance.tsx`) de un pedido Devuelto pasa de mostrar únicamente la etiqueta
"Devuelto" a mostrar **"Devuelto por {nombre}"**, donde `{nombre}` es quien **devolvió** el pedido
(el revisor que lo rechazó pidiendo corrección) — no quien debe corregirlo ahora
(`propietarioActual`, que a veces es el Jefe de Cátedra). Da contexto de quién encontró el problema
sin abrir el detalle, que es lo que un revisor necesita para triangular rápido un volumen grande.
Nuevo helper `quienDevolvio(pedido)` en `tableroRevisionModelo.ts`: último evento `"devolver"` del
historial, campo `porNombre` — mismo patrón que el `motivoRechazo`/`comentarioDe` que ya existían
para el motivo de un rechazo (se refactorizó `comentarioDe` a `eventoDe` para no duplicar el
`[...historial].reverse().find(...)`).

**Alternativa descartada**: mostrar `propietarioActual` ("esperando a Jefe de Cátedra"). Dado que
`propietarioActual` no siempre es un rol de esta tabla (JC no tiene sección), y lo que un revisor de
Secretaría/Decanato/Administrativo necesita para triangular es "quién encontró el problema", no
"quién tiene la pelota ahora" (esa segunda pregunta ya la contesta la sección en la que vive la
fila).

### D-3: Filtro nombre/legajo — dos `Input` fijos, aplicados en `aplicarFiltros` [AMENDADA]

~~Se agregan `nombre: string` y `legajo: string` a `FiltrosTablero`... dos `Input` siempre visibles~~
— **superada**: el cliente pidió después, explícitamente, "el mismo filtro para la pantalla Revisión
que el que está en Mis Pedidos" — no "dos campos sueltos que también busquen por nombre/legajo", sino
paridad de **patrón**. Para cuando esto se retomó, `mis-pedidos-simplificado` ya había construido
`shared/ui/FiltrosLista.tsx` (componente genérico config-driven, explícitamente pensado para
reutilizarse en otras pantallas — D-9 de ese change). Usarlo acá es exactamente el caso de uso para el
que se construyó, así que se adopta en vez de mantener una segunda implementación de filtro bespoke.

**Diseño final**: `<FiltrosLista>` con **Nombre** como campo fijo (paridad con "Docente" de Mis
Pedidos) y **Legajo** + **Tipo** + **Prioridad** como opcionales vía "+ Añadir filtro" (paridad con
Legajo/Tipo/Estado de Mis Pedidos). Esto implica un cambio adicional no pedido originalmente: **Tipo**
y **Prioridad**, que hoy son `Select` siempre visibles, pasan a ser opcionales también — es lo que hace
que sea "el mismo filtro" en patrón, no solo en los dos campos nuevos. La lógica de `aplicarFiltros`
(filtrado por `nombre`/`legajo` contains-normalizado, más `tipo`/`prioridad` igual que antes) no
cambia — sigue siendo la responsabilidad de `filtrosTablero.ts`; lo único que cambia es qué componente
renderiza los controles.

`FiltrosTablero` suma un índice de string (`[clave: string]: string`) para satisfacer la constraint
genérica `FiltrosLista<T extends Record<string, string>>` — mismo ajuste que ya necesitó
`FiltrosMisPedidosState`.

**Qué queda igual de la decisión original**: la función `normalizarTexto` local en
`filtrosTablero.ts` (no importada de `usuarios`, mismo argumento de features aisladas), y el criterio
de comparación "contiene, sin distinguir mayúsculas/acentos".

**Alternativa descartada** (se mantiene): un único campo de búsqueda combinado. El cliente pidió
explícitamente nombre y legajo como campos separados, espejando Usuarios/Mis Pedidos.

### D-4 y D-5: [YA IMPLEMENTADAS — ver `mis-pedidos-simplificado` D-10]

El modelo de legajo (`DocentePedido.legajo?`/`DocenteExistente.legajo`, catálogo, semillas) se terminó
implementando dentro de `mis-pedidos-simplificado` (su D-10) antes de que se retomara este change —
mismo razonamiento que D-4/D-5 acá (Alta sin legajo todavía, resto del catálogo con legajo
obligatorio). Única diferencia menor: el formato de los valores es `"1001"`–`"1010"` en vez del
`"0421"` de D-5 (4 dígitos igual, pero sin cruzar valores reales con `docentes`/`usuarios` mock
stores) — se acepta la diferencia, ambos son igual de mock/arbitrarios y no hay una fuente única de
verdad para legajo todavía (deuda ya reconocida en el riesgo de abajo). No se re-implementa nada acá.

### D-4 (texto original, referencia): `legajo` opcional en `DocentePedido`, obligatorio en `DocenteExistente`

Un Alta de docente nuevo se tipea a mano (DNI + nombre, sin legajo — el legajo lo asigna
Secretaría/RRHH después, fuera del alcance de este prototipo). Un docente que ya está en el sistema
(`DocenteExistente`, usado en Cambio/Baja/Sin novedad) siempre tiene un legajo real. Por eso:
`DocentePedido.legajo?: string` (falta en pedidos de Alta) vs. `DocenteExistente.legajo: string`
(obligatorio). `PedidoForm.tsx#seleccionarDocente` copia `legajo` desde `DocenteExistente` igual que ya
copia `dni`/`nombre`/`antiguedad`.

### D-5: Datos semilla — formato de legajo igual al de `docentes`/`usuarios`

Legajo como string de 4 dígitos con ceros a la izquierda (`"0421"`), mismo formato que ya usan
`features/docentes/mock/mockStore.ts` y `features/usuarios/mock/mockStore.ts` — consistencia de dato
mock entre features, aunque no hay (todavía) una fuente única de verdad para "legajo docente" entre
Designaciones/Docentes/Usuarios (deuda ya registrada como TD-002/TD-003 en
`docs/quality/tech-debt.md` para el catálogo de cargos; se deja nota similar para legajo si aplica).

### D-8: Expansión por default según el rol del actor (sexta ronda, reemplaza "las 4 expandidas")

La quinta ronda hizo arrancar las 4 secciones expandidas (mismo default que el plan original D-2). El
cliente pidió, en la sexta ronda, que solo arranque expandida la sección del rol del actor: Coordinador
→ "En Coordinación", Secretaría → "En Secretaría", Decanato → "En Decanato" — las otras 3 arrancan
colapsadas. Administración no tiene una sección "propia" en este esquema (ve las 3 etapas por igual,
`puedeRevisar` la deja actuar en cualquiera) — el cliente lo resolvió explícitamente ("da igual" cuál
arranque expandida para ese rol) → se optó por que las 4 arranquen colapsadas para Administración, en
vez de forzar una arbitraria.

Nuevo helper puro `seccionInicialDelActor(actor): string | null` en `tableroRevisionModelo.ts`: mapea
`Rol → id de sección` para los 3 roles de etapa, `undefined`/sin match → `null`. `TablaRevision` calcula
el resultado una vez y lo pasa como `expandidoInicial` a cada `SeccionEstadoTabla` (que sigue siendo
dueña de su propio `useState`, igual que D-2 — el padre solo decide el valor inicial, no controla el
estado desde afuera).

**Efecto derivado**: como ya no hay garantía de que las 4 secciones estén expandidas a la vez, un head
de columnas único arriba de todas (como tenía la quinta ronda) quedaba visualmente desconectado de las
secciones colapsadas — de ahí que cada sección pase a tener su propio head (ver Impact/tasks.md sección
7), pedido explícito del cliente en la misma ronda ("las columnas tienen que tener el nombre debajo de
las desplegables").

### D-9: Aceptado con dot en vez de stepper completo

La celda Estado de un pedido Aceptado (`en_lote`) mostraba un stepper completo de 4 barras verdes +
"Aceptado" (mismo componente `Stepper` que usan los estados en revisión, con las 4 barras llenas). El
cliente pidió que sea "similar a Rechazado: un punto verde y Aceptado, sacá las 4 líneas del flujo" —
tiene sentido: Aceptado es un estado terminal, no hay "avance" que comunicar (a diferencia de "En
Coordinación · 1/4", donde el stepper parcial sí aporta información). Se unificó con la rama de
Devuelto/Rechazado (dot de color + etiqueta), agregando el tono `exito` (verde) a esa rama en vez de
un caso especial con `Stepper`. El componente `Stepper` perdió su prop `variante` (ya no se usa con
`"exito"` en ningún lado) — se simplificó en vez de dejar la prop muerta.

### D-10: Filtro Tipo — de opcional a fijo, junto a Nombre

El cliente pidió que el filtro **Tipo** aparezca "al inicio, al costado del filtro por Docente" — fijo,
no detrás de "+ Añadir filtro" como quedó en la segunda ronda (paridad de patrón con Mis Pedidos, D-3
amendada). Esto **rompe** esa paridad a propósito: es un pedido explícito y posterior del cliente,
específico de esta pantalla — Mis Pedidos no cambia.

`shared/ui/FiltrosLista.tsx` solo soportaba campos fijos de texto (`<Input>`); se extendió
`CampoFiltroFijo` a una unión discriminada (`{ tipo?: "texto"; placeholder; ancho? }` | `{ tipo:
"select"; opciones }`), con `tipo` opcional en la rama de texto para que las configuraciones
existentes (Mis Pedidos, Usuarios si adoptara este componente) sigan compilando sin tocarlas. Es un
cambio en un componente compartido (`shared/ui/`) pero aditivo — no se evaluó una copia bespoke del
filtro porque el mecanismo "+ Añadir filtro"/fijos ya está pensado como config-driven para esto
exactamente.

## Risks / Trade-offs

- **[Riesgo] Legajo duplicado/hardcodeado en 3 lugares** (`docentes`, `usuarios`, `designaciones` mock
  stores) sin una fuente única → aceptado como deuda de prototipo (mock-only, sin backend real
  todavía); se registra en `docs/quality/tech-debt.md` junto a TD-002/TD-003 si no está ya cubierto.
- **[Riesgo] Con solo una sección expandida por default, un revisor puede no notar pedidos en otras
  etapas** (p. ej. un Coordinador no ve por default si hay pedidos suyos ya en Secretaría/Decanato) →
  aceptado: es el trade-off explícito que pidió el cliente (foco en la etapa propia); el contador de
  cada sección (visible aunque esté colapsada) sigue comunicando cuántos pedidos hay en las demás.
- **[Riesgo] Un pedido Devuelto sin `etapaRetorno`** (no debería pasar — `devolver()` siempre lo
  setea) no pertenecería a ninguna sección de etapa y desaparecería de la vista → aceptado: es un
  invariante de dominio ya garantizado por `maquinaEstados.ts`, no una validación defensiva de más;
  si se rompiera sería un bug de dominio, no algo que la vista deba enmascarar.

## Migration Plan

**Entregado** (1–4: ya via `mis-pedidos-simplificado`; 5–23 en pasadas sucesivas de este change):

1. ~~`types.ts`: agregar `legajo`...~~ ya hecho en `mis-pedidos-simplificado`.
2. ~~`api/catalogos.ts`: agregar `legajo`...~~ ya hecho en `mis-pedidos-simplificado`.
3. ~~`api/pedidosSeed.ts`: `SemillaPedido.legajo?`...~~ ya hecho en `mis-pedidos-simplificado`.
4. ~~`components/PedidoForm.tsx`: `seleccionarDocente()` copia `legajo`.~~ ya hecho en
   `mis-pedidos-simplificado`.
5. `components/filtrosTablero.ts`: `FiltrosTablero` suma `nombre`/`legajo` (+ índice de string) +
   `FILTROS_INICIALES` + `aplicarFiltros` + `normalizarTexto` local.
6. `pages/TableroRevisionPage.tsx`: `<FiltrosLista>` (D-3 amendada) en vez de `Input` sueltos — Nombre
   fijo, Legajo/Tipo/Prioridad opcionales; Vista queda separado en las acciones del header.
7. Tests: `TableroRevisionPage.test.tsx` — filtrar por Docente, agregar/quitar el filtro Legajo.
8. Specs: aplicar el delta de "Filtro de pedidos por nombre o legajo del docente" (ADDED) en
   `tablero-revision-tabla` — reescrito para describir `FiltrosLista` en vez de dos `Input` sueltos.
9. `components/tableroRevisionModelo.ts` / `TablaRevision.tsx` / `revision.css`: columnas **Legajo**
   y **Fecha última actualización**, header "Novedad" → "Tipo" (tercera ronda, ver sección 5 de
   `tasks.md`).
10. `components/TablaRevision.tsx`: quitar el prefijo "Prof." de la columna Docente (cuarta ronda) —
    desacoplado de la reestructura en secciones (punto 11 abajo).
11. `components/tableroRevisionModelo.ts`: `construirColumnas` reescrito para agrupar por etapa del
    circuito (D-6) — ya no toma `actor`; nuevos helpers `quienDevolvio` (D-7) y `eventoDe` (refactor
    de `comentarioDe`, sin cambiar su comportamiento).
12. `components/EstadoAvance.tsx`: celda Estado de un Devuelto muestra "Devuelto por {revisor}" (D-7).
13. `components/TablaRevision.tsx`: reestructurado en `SeccionEstadoTabla` por sección de
    `construirColumnas` (D-2, mecanismo sin cambios; D-6, contenido de cada sección).
14. CSS (`revision.css`): clases `.adoc-seccion-*` para el header desplegable de cada sección.
15. Tests: `tableroRevisionModelo.test.ts` (agrupación por etapa, orden prioritario→devuelto→fecha,
    `quienDevolvio`), `TablaRevision.test.tsx` (secciones, colapsar/expandir, "Devuelto por"),
    `EstadoAvance.test.tsx` ("Devuelto por {revisor}").
16. Specs: aplicar el delta MODIFIED de "Vista Tabla del tablero de revisión" en
    `tablero-revision-tabla` (agrupación por etapa) — reescrito para describir el criterio D-6/D-7 en
    vez del estado de avance original.
17. `components/tableroRevisionModelo.ts`: nuevo helper `seccionInicialDelActor(actor)` (D-8).
18. `components/TablaRevision.tsx`: `SeccionEstadoTabla` recibe `expandidoInicial` (en vez de arrancar
    siempre en `true`); cada sección renderiza su propio head de columnas (ya no uno compartido arriba
    de las 4) — D-8.
19. `components/EstadoAvance.tsx`: Aceptado (`en_lote`) pasa de stepper completo a dot verde + texto,
    unificado con la rama Devuelto/Rechazado; `Stepper` pierde la prop `variante` (D-9).
20. `components/revision.css`: cada sección es su propia card (borde/radius/fondo) separada por `gap`
    en `.adoc-tabla` (antes: un único contenedor con `border-top` entre secciones); título de sección
    en `--accent-700` para las 4 (antes solo las de tono `acento`); `.adoc-estado-avance.exito
.adoc-estado-dot` para el dot verde de Aceptado.
21. `shared/ui/FiltrosLista.tsx` / `.css`: `CampoFiltroFijo` admite `{ tipo: "select", opciones }`
    (D-10); `pages/TableroRevisionPage.tsx` mueve **Tipo** de `FILTROS_OPCIONALES` a `FILTROS_FIJOS`,
    junto a Nombre.
22. `api/pedidosSeed.ts`: 4 semillas de Alta + 4 de Baja nuevas (en revisión en cada etapa, devuelto,
    en_lote, rechazado) — antes había 1 sola de cada novedad.
23. Tests: `tableroRevisionModelo.test.ts` no cambia (la agrupación por etapa/orden no cambió, solo el
    default de expansión, que vive en el componente); `TablaRevision.test.tsx` (expansión por rol,
    head por sección, Administración sin default), `EstadoAvance.test.tsx` (Aceptado con dot, sin
    stepper), `FiltrosLista.test.tsx` (campo fijo select).

**Mockup diferido**: esta iteración no sincroniza `ebl4U` en `screens.pen` (decisión explícita para
no consumir tiempo en Pencil en este change) — queda como deuda de mockup, a resolver en una pasada
posterior dedicada.

**Rollback**: revertir el PR restaura la Tabla plana original — sin filtros, sin legajo/tipo/fecha,
sin secciones, con el prefijo "Prof."; el legajo del modelo NO se revierte acá (pertenece a
`mis-pedidos-simplificado`); sin migraciones de datos (prototipo mock).

## Open Questions

- Ninguna bloqueante. Confirmado con el cliente: legajo se agrega al modelo (no solo se reusa DNI); la
  agrupación es por etapa del circuito (no por estado de avance); "Devuelto por" muestra a quien
  devolvió (no al propietario actual); dentro de Finalizados los Aceptados van antes que los
  Rechazados; el default de expansión es solo la sección del rol del actor (no las 4), con
  Administración arrancando las 4 colapsadas por no tener sección propia; las secciones van separadas
  visualmente (no apiladas) y con su propio head de columnas; Aceptado se ve como Rechazado (dot +
  texto, sin stepper); y el filtro Tipo pasa a fijo junto a Nombre (rompiendo a propósito la paridad
  de patrón con Mis Pedidos).
