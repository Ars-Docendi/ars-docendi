## 1. Modelo — legajo

- [x] 1.1 En `types.ts`: agregar `legajo?: string` a `DocentePedido` y `legajo: string` a
      `DocenteExistente`.
- [x] 1.2 En `api/catalogos.ts`: agregar `legajo` a las 7 entradas de `DOCENTES_EXISTENTES`.
- [x] 1.3 En `api/pedidosSeed.ts`: agregar `legajo?` a `SemillaPedido`, pasarlo en `desdeSemilla()`, y
      agregar legajo a las semillas de docentes existentes (dejar sin legajo la/las semillas de Alta, para
      ejercitar el caso opcional).
- [x] 1.4 En `components/PedidoForm.tsx`: `seleccionarDocente()` copia `legajo` desde
      `DocenteExistente` (y lo deja `undefined` en el reset cuando no hay match).

> **Nota**: la sección 1 completa se implementó como parte de `mis-pedidos-simplificado` (su D-10),
> antes de retomar este change — no se repitió el trabajo acá, solo se confirmó que el modelo ya
> cumple lo que esta sección pedía (formato de legajo: string de 4 dígitos, `"1001"`–`"1010"`, en vez
> del `"0421"` de D-5 más abajo — ver nota en esa decisión).

## 2. Filtro por nombre/legajo — reusando el componente genérico `FiltrosLista`

- [x] 2.1 En `components/filtrosTablero.ts`: agregado `nombre: string` y `legajo: string` a
      `FiltrosTablero` (+ índice de string `[clave: string]: string`, requerido por la constraint genérica
      de `FiltrosLista` — mismo patrón que `filtrosMisPedidos.ts`); `FILTROS_INICIALES` en `""`; función
      local `normalizarTexto`; `aplicarFiltros` filtra por ambos campos (contiene, normalizado) además de
      lo existente (tipo, prioridad).
- [x] 2.2 En `pages/TableroRevisionPage.tsx`: **cambio de diseño respecto a D-3 (ver D-3 amendada)** —
      en vez de dos `Input` sueltos, se usa `<FiltrosLista>` (`shared/ui/FiltrosLista.tsx`, construido en
      `mis-pedidos-simplificado` D-9): **Nombre** como campo fijo (paridad con "Docente" de Mis Pedidos) +
      **Legajo**, **Tipo** y **Prioridad** como opcionales vía "+ Añadir filtro" (paridad con Legajo/Tipo/
      Estado de Mis Pedidos) — Tipo y Prioridad pasan de ser `Select` siempre visibles a opcionales. El
      filtro de **Vista** ("Mis pendientes"/"Vista completa") queda aparte, en las acciones del
      `PageHeader` — no es un filtro en el sentido de Usuarios/Mis Pedidos, es un switcher de alcance.
- [x] 2.3 Tests en `TableroRevisionPage.test.tsx`: filtrar por Docente acota la lista; agregar y quitar
      el filtro opcional Legajo acota y restaura la lista (mismo patrón que los tests equivalentes de
      `MisPedidosPage.test.tsx`).

## 3. Tabla de revisión — sin "Prof." y agrupada (por etapa del circuito, ver sección 6)

**3.1 retomada e implementada (cuarta ronda)**; **3.2–3.4 quedaron superadas por la sección 6**
(quinta ronda) — el cliente retomó la agrupación, pero con criterio de **etapa del circuito**
(En Coordinación/En Secretaría/En Decanato/Finalizados) en vez de estado de avance. El mecanismo de
sección desplegable descripto en 3.2/3.3 (header `aria-expanded`, `useState` en `true`, CSS del
chevron) se construyó tal cual estaba planeado; lo que cambió es **qué pedidos entran en cada
sección**, ver sección 6 para el detalle real de la implementación:

- [x] 3.1 En `components/TablaRevision.tsx`: quitar el prefijo "Prof." de la columna Docente (queda
      solo `{pedido.docente.nombre}`).
- [x] 3.2 Reestructurar `TablaRevision.tsx`: iterar cada sección de `construirColumnas(...)` y
      renderizar una sección propia (nuevo sub-componente `SeccionEstadoTabla`, con `useState<boolean>`
      inicializado en `true`, header `aria-expanded` + título + contador, y el body de la mini-tabla
      condicional a `expandido`) — implementado con las secciones de la sección 6, no las de estado de
      avance descriptas originalmente acá.
- [x] 3.3 CSS en `revision.css`: clases para el header desplegable de cada sección (título, contador,
      chevron que rota) — mismo lenguaje visual que el resto de la pantalla (tokens del design system).
- [x] 3.4 Test en `TablaRevision.test.tsx`: renderiza las secciones con sus contadores correctos;
      colapsar una sección oculta sus filas sin afectar las demás; las 4 arrancan expandidas; una sección
      vacía muestra su estado vacío.
- [x] 3.5 Test en `TablaRevision.test.tsx`: el nombre del docente se muestra sin el prefijo "Prof.".

## 4. Tests y cierre (secciones 1 y 2)

- [x] 4.1 `pnpm exec tsc --noEmit` + `pnpm exec vitest run` (162/162) + `pnpm exec eslint .` — todo
      verde. Alcance: modelo de legajo (ya cubierto por `mis-pedidos-simplificado`) + filtro Nombre/Legajo/
      Tipo/Prioridad de la sección 2. Para el cierre de las secciones 5 y 6, ver 5.5 y 6.6.
- [x] 4.2 Verificado en `TablaMisPedidos.tsx` y `ModalConfirmacionAccion.tsx` que el prefijo "Prof."
      sigue intacto (grep confirmó ambos sin cambios) — el pedido del cliente fue específico de
      `TablaRevision.tsx`, no se tocó nada más.
- [x] 4.3 Actualizar `docs/product/designs/proyecto-docente-design-spec.md`: reflejar la Tabla
      agrupada en secciones — hecho en la sección 6 (6.5), con el criterio de etapa del circuito.
- [x] 4.4 Actualizado `docs/product/designs/proyecto-docente-design-spec.md` — Layout/IA de la Tabla de
      revisión: filtro estilo `FiltrosLista` (Nombre fijo + Legajo/Tipo/Prioridad opcionales), Vista queda
      en las acciones del header.
- [x] 4.5 `npx openspec validate --all --strict`.

## 5. Columnas Legajo/Tipo/Fecha en la Tabla de revisión (sobre la tabla plana, sin tocar la sección 3)

- [x] 5.1 En `components/tableroRevisionModelo.ts`: agregado `fechaUltimaActualizacion(pedido)`, que
      toma `pedido.historial.at(-1)` (cualquier `AccionHistorial`, no solo "enviar") y lo formatea con
      `formatearFecha` (reusado de `detalleAdapters.ts`, sin duplicar el parseo de fecha).
- [x] 5.2 En `components/TablaRevision.tsx`: agregada columna **Legajo** (`pedido.docente.legajo ??
"—"`) entre Docente y Asignatura; header "Novedad" renombrado a "Tipo" (el chip `NovedadChip` no
      cambió); agregada columna **Fecha última actualización** entre Tipo y Estado.
- [x] 5.3 En `components/revision.css`: `grid-template-columns` de `.adoc-tabla-head` /
      `.adoc-tabla-row` extendido de 5 a 7 columnas (Docente, Legajo, Asignatura, Tipo, Fecha, Estado,
      prioritario); clases `.adoc-tabla-legajo` / `.adoc-tabla-fecha` nuevas (mismo lenguaje visual que
      `.adoc-tabla-asig`).
- [x] 5.4 Test en `TablaRevision.test.tsx`: la tabla muestra legajo del docente (y "—" si no tiene);
      el header dice "Tipo" (no "Novedad"); la columna Fecha última actualización muestra la fecha del
      último evento del historial de cada pedido.
- [x] 5.5 `pnpm exec tsc --noEmit` (limpio) + `pnpm exec vitest run` (159/159, `src/features/designaciones`)
  - `pnpm exec eslint` (limpio); actualizado `docs/product/designs/proyecto-docente-design-spec.md`
    con las columnas nuevas; `npx openspec validate --all --strict` (16/16 OK).

## 6. Agrupación por etapa del circuito (quinta ronda, retoma y reemplaza 3.2–3.4)

- [x] 6.1 En `components/tableroRevisionModelo.ts`: `construirColumnas(pedidos)` reescrito — ya no
      toma `actor`. 4 secciones: `en-coordinacion`/`en-secretaria`/`en-decanato` (un pedido pertenece si
      `estado === etapa` o `estado === "devuelto" && etapaRetorno === etapa`) + `finalizados`
      (`en_lote`/`rechazado`). Orden dentro de una sección de etapa: prioritarios → devueltos → resto por
      `fechaUltimaActualizacionMs` ascendente. Orden dentro de Finalizados: `en_lote` antes que
      `rechazado`; dentro de cada bloque, por fecha descendente.
- [x] 6.2 En `components/tableroRevisionModelo.ts`: nuevo helper `quienDevolvio(pedido)` — último
      evento `"devolver"` del historial, campo `porNombre`. Refactor de `comentarioDe` → `eventoDe`
      (devuelve el evento completo, no solo el comentario) para que `motivoRechazo` y `quienDevolvio`
      compartan la búsqueda sin duplicarla.
- [x] 6.3 En `components/EstadoAvance.tsx`: la celda Estado de un pedido Devuelto muestra "Devuelto
      por {quienDevolvio(pedido) ?? "—"}" en vez de solo "Devuelto".
- [x] 6.4 En `components/TablaRevision.tsx`: nuevo sub-componente `SeccionEstadoTabla` (header
      `<button aria-expanded aria-controls aria-label>` con chevron + título + contador,
      `useState<boolean>` en `true`) — itera las secciones de `construirColumnas` y renderiza sus filas
      vía `FilaTablaRevision`; el head de columnas (Docente/Legajo/Asignatura/Tipo/Fecha/Estado/vacío)
      queda una sola vez arriba de todas las secciones, no repetido por sección. **Superado en la sección
      7** (sexta ronda): el `useState<boolean>` pasó de arrancar siempre en `true` a recibir
      `expandidoInicial` desde el padre (7.1), y el head de columnas pasó a repetirse por sección (7.2).
- [x] 6.5 CSS en `revision.css`: `.adoc-seccion-tabla`, `.adoc-seccion-header`, `.adoc-seccion-chevron`
      (+ `--colapsado` rotado), `.adoc-seccion-titulo`, `.adoc-seccion-subtitulo`, `.adoc-seccion-contador`.
      Actualizado `docs/product/designs/proyecto-docente-design-spec.md` con el criterio de agrupación
      por etapa (reemplaza la mención a "agrupar por estado" que quedaba pendiente).
- [x] 6.6 Tests: `tableroRevisionModelo.test.ts` (4 secciones y ruteo por `etapaRetorno`; orden
      prioritario→devuelto→fecha; orden de Finalizados; `quienDevolvio`); `TablaRevision.test.tsx`
      (títulos de sección, devuelto en la sección de su `etapaRetorno` con "Devuelto por", expandir/
      colapsar sin afectar otras secciones, estado vacío por sección); `EstadoAvance.test.tsx` ("Devuelto
      por {revisor}", fallback "—" sin evento de devolución). `pnpm exec tsc --noEmit` + `pnpm exec
vitest run src/features/designaciones` (167/167) + `pnpm exec eslint` — todo verde.
- [x] 6.7 `npx openspec validate --all --strict`.

## 7. Ajustes de UX sobre las secciones + filtro Tipo fijo + más datos de prueba (sexta ronda)

- [x] 7.1 En `components/tableroRevisionModelo.ts`: nuevo helper `seccionInicialDelActor(actor):
string | null` (D-8) — mapea Coordinador/Secretaría/Decanato a su id de sección; sin match
      (Administración u otro rol) → `null`. En `components/TablaRevision.tsx`: `SeccionEstadoTabla` recibe
      `expandidoInicial` (ya no arranca siempre en `true`); `TablaRevision` calcula
      `seccionInicialDelActor(actor)` una vez y lo pasa a cada sección.
- [x] 7.2 En `components/TablaRevision.tsx`: el head de columnas deja de estar una sola vez arriba de
      todas las secciones — cada `SeccionEstadoTabla` expandida renderiza su propio head, antes de sus
      filas (o del estado vacío).
- [x] 7.3 En `components/EstadoAvance.tsx`: Aceptado (`en_lote`) pasa de stepper completo + "Aceptado"
      a dot verde + "Aceptado" (D-9), unificado con la rama Devuelto/Rechazado; `Stepper` pierde la prop
      `variante` (ya no se usa con `"exito"`).
- [x] 7.4 En `components/revision.css`: `.adoc-tabla` pasa de contenedor único con borde a `display:
flex` con `gap: var(--space-4)`; cada `.adoc-seccion-tabla` gana su propio borde/radius/fondo (antes
      vivía en `.adoc-tabla`, compartido); se quita el `border-top` entre secciones (ya no hace falta,
      quedan separadas por el `gap`); `.adoc-seccion-titulo` pasa a `color: var(--accent-700)` para las 4
      secciones (antes solo las de tono `acento` tenían ese color, `neutro`/Finalizados quedaba igual que
      las columnas); nuevo `.adoc-estado-avance.exito .adoc-estado-dot` (verde) para el dot de Aceptado.
- [x] 7.5 En `shared/ui/FiltrosLista.tsx` / `.css`: `CampoFiltroFijo` pasa a unión discriminada — admite
      `{ tipo: "select", opciones }` además del texto de siempre (`tipo` opcional en esa rama, por
      compatibilidad con las configuraciones existentes); nueva clase `.adoc-filtros-fijo-select`. En
      `pages/TableroRevisionPage.tsx`: **Tipo** se mueve de `FILTROS_OPCIONALES` a `FILTROS_FIJOS`, junto a
      Nombre, al inicio de la barra (D-10).
- [x] 7.6 En `api/pedidosSeed.ts`: 4 semillas de Alta nuevas (en_revision_coordinador,
      en_revision_secretaria, devuelto, en_lote) + 4 de Baja nuevas (en_revision_coordinador,
      en_revision_decanato, devuelto, rechazado) — antes había 1 sola Alta (rechazada) y 1 sola Baja (en
      borrador, ni siquiera visible en esta tabla).
- [x] 7.7 Tests: `TablaRevision.test.tsx` (expansión por rol del actor para Coordinador/Secretaría/
      Decanato; Administración con las 4 colapsadas; head de columnas repetido por sección expandida;
      devuelto visible solo si su sección está expandida); `EstadoAvance.test.tsx` (Aceptado con dot,
      sin `.adoc-pedido-stepper` en el DOM); `FiltrosLista.test.tsx` (campo fijo `select`: siempre
      visible, no detrás de "+ Añadir filtro", dispara `onChange`). `pnpm exec tsc --noEmit` + `pnpm exec
vitest run` (185/185, suite completa) + `pnpm exec eslint` — todo verde.
- [x] 7.8 Actualizado `docs/product/designs/proyecto-docente-design-spec.md` con la expansión por rol,
      el head por sección, el dot de Aceptado, la separación visual entre secciones, el color del título
      y el filtro Tipo fijo.
- [x] 7.9 `npx openspec validate --all --strict`.

## 8. Contador de sección: etiqueta "Total:" + color distinto (séptima ronda)

- [x] 8.1 En `components/TablaRevision.tsx`: el contador pasa de `{seccion.pedidos.length}` a
      `Total: {seccion.pedidos.length}`.
- [x] 8.2 En `components/revision.css`: `.adoc-seccion-contador` pasa de `background:
var(--neutral-200)` / `color: var(--color-text-secondary)` (casi igual al fondo del header de
      sección y al gris de los headers de columna) a `background: var(--accent-100)` / `color:
var(--accent-700)` — mismo par de tokens que `.adoc-pedido-avatar`.
- [x] 8.3 Test en `TablaRevision.test.tsx`: el contador muestra "Total: {n}", no el número solo.
- [x] 8.4 `pnpm exec tsc --noEmit` + `pnpm exec vitest run src/features/designaciones` (171/171) +
      `pnpm exec eslint` — todo verde.

## 9. Fondo de la card de sección: de blanco confundible con la página a verde (octava/novena/décima ronda)

- [x] 9.1 (octava ronda) En `components/revision.css`: `.adoc-seccion-header` usaba `background:
var(--color-bg-canvas)` — el mismo token que el fondo de toda la página (`app/shell/shell.css`).
      Con una sección colapsada, el header (todo lo visible) se perdía contra el fondo. Primer intento:
      `background: var(--color-bg-raised)` (blanco), igual que el resto de la card.
- [x] 9.2 (novena ronda) Pedido de seguimiento: "poneles un color verde claro". `.adoc-seccion-tabla`
      y `.adoc-seccion-header` pasan de `--color-bg-raised` a `--color-status-success-bg`
      (`--success-100`); el contador (8.2) pasa de `--accent-100`/`--accent-700` a `--color-bg-raised`
      (blanco) + `--color-status-success-fg` (`--success-700`) para no perderse contra el nuevo verde
      del header — mismo problema que resolvió 8.2, ahora contra un fondo distinto.
- [x] 9.3 (décima ronda) Pedido de seguimiento: "oscurecé un poco el color verde ese". `--success-100`
      (94% luminosidad) no tiene escalón intermedio hacia `--success-500` (55%, salto grande, se ve como
      badge sólido). `.adoc-seccion-tabla` / `.adoc-seccion-header` pasan a `--accent-200` (86%
      luminosidad, mismo verde-azulado que `--accent-700` del título de sección) — un escalón más oscuro,
      token existente, sin `color-mix()` ni hardcodear un valor. El contador pasa su texto de
      `--color-status-success-fg` a `--accent-700`, unificando la familia de color con el título.
- [x] 9.4 `pnpm exec vitest run src/features/designaciones` (171/171, sin cambios de comportamiento —
      ajustes puramente de CSS) — verde en cada una de las 3 rondas.
- [x] 9.5 (undécima ronda) El cliente rechazó el fill verde ("está horrible") y pidió una
      recomendación. Diagnóstico: `--color-bg-canvas` (fondo de página) es un cálido casi-neutro (matiz
      75°, croma ~0.006); `--accent-*`/`--success-*` viven en matiz ~150-165° con croma bastante más
      alto — cualquier fill sólido en esa familia iba a chocar contra el fondo cálido, sin importar el
      escalón de claridad elegido (por eso ni el verde claro ni el oscurecido funcionaban). Se cambió de
      **enfoque**, no solo de tono: `.adoc-seccion-tabla` vuelve a `background: var(--color-bg-raised)`
      (blanco) + `border-left: 4px solid var(--accent-500)` (franja de identidad, no fill completo) +
      `box-shadow: 0 1px 3px rgba(0,0,0,0.06)` (separación por elevación en vez de por matiz). El
      contador vuelve a `background: var(--accent-100)` / `color: var(--accent-700)` (pill de color,
      como en la séptima ronda) — un área chica de acento sobre una card ahora blanca no reproduce el
      choque de matiz que sí tenía teñida toda la card.
- [x] 9.6 `pnpm exec vitest run src/features/designaciones` (171/171) — verde.
- [x] 9.7 (duodécima ronda) El cliente rechazó también el blanco puro ("el blanco ese es horrible y
      no combina con el fondo beige"). Causa: `--color-bg-raised` = `#ffffff` literal, sin matiz;
      `--color-bg-canvas` (fondo de página) = `--neutral-200`, con matiz 75° (cálido). Un blanco sin
      matiz al lado de un cálido también choca. Se cambió `background` de `.adoc-seccion-tabla` /
      `.adoc-seccion-header` de `var(--color-bg-raised)` a `var(--neutral-100)` — 98% de luminosidad,
      mismo matiz 75° que el fondo, apenas más claro que `--neutral-200` (95%). Franja
      (`border-left: var(--accent-500)`), borde y sombra sin cambios; contador sigue en pill
      `--accent-100`/`--accent-700`.
- [x] 9.8 `pnpm exec vitest run src/features/designaciones` (171/171) — verde.
- [x] 9.9 (decimotercera ronda) El cliente marcó que el head de columnas dentro de cada sección
      expandida no se distinguía ni del fondo de página ni del título del desplegable. Causa:
      `.adoc-tabla-head` usaba `background: var(--color-bg-canvas)` (= `--neutral-200`), el mismo tono
      que el fondo de página, y quedaba pegado a la card/título ahora en `--neutral-100`. Pasa a
      `background: var(--neutral-300)` (91% de luminosidad, mismo matiz 75°) — un escalón más oscuro
      que ambos, dejando una jerarquía clara: título (98%) → head de columnas (91%) → filas (blancas).
- [x] 9.10 `pnpm exec vitest run src/features/designaciones` (171/171) — verde.
- [x] 9.11 (decimocuarta ronda) El cliente rechazó `--neutral-300` ("horrible") y pidió
      explícitamente que el head de columnas tenga el mismo color que el título del desplegable.
      `.adoc-tabla-head` pasa de `--neutral-300` a `--neutral-100` (mismo token que
      `.adoc-seccion-header`) — header y head de columnas quedan como una sola superficie continua; el
      `border-bottom` existente sigue separando el contenido.
- [x] 9.12 `pnpm exec vitest run src/features/designaciones` (171/171) — verde.
- [x] 9.13 (decimoquinta ronda) Con el título y el head de columnas en el mismo color
      (`--neutral-100`), quedaban fundidos en un solo bloque sin nada entre medio. El cliente pidió
      agregar algo que los diferencie. Se agregó `border-top: 1px solid var(--color-border-default)` en
      `.adoc-tabla-head` — línea divisoria entre el botón del título y el head de columnas, sin volver a
      tocar el color de fondo.
- [x] 9.14 `pnpm exec vitest run src/features/designaciones` (171/171) — verde.

## 10. Etapa al costado de "Devuelto por" en la celda Estado (decimosexta ronda)

- [x] 10.1 Bug reportado por el cliente (no visual): "Devuelto por {nombre}" reemplazó por completo
      la referencia a la etapa/estado real del pedido devuelto — el dato que un revisor necesita, junto
      con la sección en la que vive la fila, para el filtro "Mis pendientes". Pedido: que la etapa esté
      al costado de "Devuelto por", no en su lugar.
- [x] 10.2 En `components/tableroRevisionModelo.ts`: nuevo helper `etiquetaEtapaRetorno(pedido)` —
      reusa `ETIQUETA_ETAPA` (el mismo mapa que arma "En Coordinación"/"En Secretaría"/"En Decanato" para
      los estados activos), indexado por `pedido.etapaRetorno` en vez de `pedido.estado`.
- [x] 10.3 En `components/EstadoAvance.tsx`: la rama de Devuelto pasa de `"Devuelto por
{quienDevolvio(pedido) ?? "—"}"` a `"{etiquetaEtapaRetorno(pedido) ?? "Devuelto"} · Devuelto por
{quienDevolvio(pedido) ?? "—"}"` — p. ej. "En Coordinación · Devuelto por M. Díaz", mismo formato
      "{etapa} · {detalle}" que ya usan los estados en revisión.
- [x] 10.4 En `components/revision.css`: `.adoc-estado-avance-txt` suma `overflow: hidden` +
      `text-overflow: ellipsis` (antes solo `white-space: nowrap`, sin recorte) — el texto de la celda
      Estado puede ser más largo ahora, no debe desbordar ni solaparse con la columna Prioritario.
- [x] 10.5 Tests: `tableroRevisionModelo.test.ts` (`etiquetaEtapaRetorno` — etiqueta correcta, y
      `undefined` sin `etapaRetorno`); `EstadoAvance.test.tsx` (texto "{etapa} · Devuelto por {revisor}",
      fallback "Devuelto" sin `etapaRetorno`, fallback "—" sin evento de devolución); `TablaRevision.test.tsx`
      (texto con etapa en la fila real, dentro de su sección). `pnpm exec tsc --noEmit` + `pnpm exec
vitest run src/features/designaciones` (174/174) + `pnpm exec eslint` — todo verde.
- [x] 10.6 `npx openspec validate --all --strict`.

## 11. Mismo stepper que un estado en revisión para Devuelto, con "Devuelto por {nombre} ({rol})" al costado (decimoséptima ronda)

- [x] 11.1 El cliente aclaró el pedido de la sección 10: el fix de texto plano no alcanzaba — quería
      mantener el **mismo stepper de 4 barras** y el mismo formato que ya usan los estados en revisión
      ("las 4 líneas que aclaran y el lugar donde está"), y agregar "Devuelto por {nombre}" **y el
      rol/tipo de usuario** de quien lo devolvió al costado de eso, no en su lugar.
- [x] 11.2 En `components/tableroRevisionModelo.ts`: `etiquetaEtapaRetorno` (sección 10) se reemplaza
      por `avanceEtapaRetorno(pedido)` — mismo shape que `avancePedido` (`{ etiqueta, paso, total }`),
      indexado por `pedido.etapaRetorno` en vez de `pedido.estado`, reusando `PASO_DE_ETAPA`/
      `ETIQUETA_ETAPA` sin duplicar los mapas. Nuevo helper `rolDeQuienDevolvio(pedido)` — mismo patrón
      que `quienDevolvio`, pero devuelve `porRol` del último evento `"devolver"`.
- [x] 11.3 En `components/EstadoAvance.tsx`: la rama de Devuelto reusa el mismo componente
      `<Stepper>` que la rama de "en revisión" — mini-stepper parcial (1-3 de 4 barras, un
      `etapaRetorno` nunca es `en_lote`) + "En {etapa} · {paso}/{total} · Devuelto por {nombre}
      ({rol})". Si no hay `etapaRetorno` (no debería pasar, invariante de dominio), cae a un dot en vez
      de stepper, con el mismo detalle "Devuelto por {nombre} ({rol})" sin perderlo.
- [x] 11.4 Tests: `tableroRevisionModelo.test.ts` (`avanceEtapaRetorno` — etapa/paso correctos y
      `null` sin `etapaRetorno`; `rolDeQuienDevolvio` — rol correcto y `undefined` sin evento);
      `EstadoAvance.test.tsx` (texto completo "En {etapa} · {paso}/{total} · Devuelto por {nombre}
      ({rol})", stepper presente en vez de dot, fallback sin `etapaRetorno`); `TablaRevision.test.tsx`
      (texto completo en la fila real, dentro de su sección). `pnpm exec tsc --noEmit` + `pnpm exec
vitest run src/features/designaciones` (174/174) + `pnpm exec eslint` — todo verde.
- [x] 11.5 `npx openspec validate --all --strict`.
