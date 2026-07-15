## 1. Seed de datos

- [x] 1.1 En `api/pedidosSeed.ts`: reducir `SEMILLAS` de "Ingeniería de Software" a 4 (Laura Giménez
      borrador, Valeria Suárez en_revision_coordinador, Pablo Herrera devuelto, Brenda Ortiz rechazado);
      eliminar Diego Morales, Sofía Romano, Martín Acosta, Lucía Fernández (semilla), Florencia Cabrera,
      Hernán Vidal, Gabriel Núñez. Conservar Mariano Tévez (otra carrera) sin cambios.
- [x] 1.2 Correr toda la suite de designaciones y confirmar que nada rompe por la reducción (118/118
      antes de seguir con el resto del change).

## 2. Form de pedido — Guardar y enviar / reenviar

- [x] 2.1 En `components/PedidoForm.tsx`: extender la firma de `onGuardar` a
      `(datos: DatosEditablesPedido, opciones?: { enviar?: boolean }) => void`; agregar el segundo botón
      del footer ("Guardar y enviar" o "Guardar y reenviar" según `pedidoInicial?.estado === "devuelto"`),
      que corre `validarPedido` y llama `onGuardar(datos, { enviar: true })`.
- [x] 2.2 En `pages/PedidoFormPage.tsx`: `handleGuardar` recibe el segundo parámetro; si
      `opciones?.enviar`, encadena la mutación correspondiente tras guardar — `crearPedido` → `enviarPedido`
      (creación), `editarPedido` → `enviarPedido` (borrador existente) o `editarPedido` →
      `reenviarPedido` (devuelto existente) — usando los hooks ya existentes.
- [x] 2.3 Tests en `PedidoForm.test.tsx`: "Guardar y enviar" aparece para un pedido nuevo o en
      borrador, "Guardar y reenviar" para uno devuelto; ambos bloqueados si la validación falla; ambos
      llaman `onGuardar` con `{ enviar: true }` cuando pasa (22/22 tests del archivo).
- [x] 2.4 Test de integración en `flujoAprobacion.test.tsx`: editar un pedido devuelto y click en
      "Guardar y reenviar" lo reenvía y retoma la etapa de retorno (verificado a través del flujo
      devolución → reenvío).

## 3. Filtro de Mis Pedidos (estilo Usuarios)

- [x] 3.1 Nuevo archivo `components/filtrosMisPedidos.ts`: `FiltrosMisPedidosState`
      (`docente`/`numero`/`tipo`/`estado`), `FILTROS_INICIALES`, `aplicarFiltrosMisPedidos`, y
      `normalizarTexto` local. Header/label "Novedad" → "Tipo".
- [x] 3.2 En `pages/MisPedidosPage.tsx`: reemplazado el buscador combinado + `Select` de estado fijo
      por el bloque de filtro estilo `FiltrosUsuarios.tsx` — Docente/N° siempre visibles, Tipo/Estado vía
      "+ Añadir filtro" con botón "×" para quitarlos.
- [x] 3.3 Tests en `MisPedidosPage.test.tsx`: filtrar por Docente y por N° acota la lista; agregar y
      quitar el filtro Estado funciona (8/8 tests del archivo).

## 4. Tabla — sin "Prof.", fila clickeable, botón Editar

- [x] 4.1 En `components/TablaMisPedidos.tsx`: sin prefijo "Prof."; header "TIPO"; la fila
      (`role="row"`) tiene `onClick`/`onKeyDown` → navega al detalle; botón "Editar" inline (con
      `stopPropagation`) visible solo cuando `puedeEditarPedido(pedido, actor)`; sin columna de kebab.
- [x] 4.2 Eliminado `components/MenuAccionesPedido.tsx` (confirmado sin otros consumidores) y las
      funciones huérfanas `cancelarPedido`/`useCancelarPedido` (sin consumidores tras sacar la acción
      Cancelar; la transición pura `aplicarAccion({tipo:"cancelar"})` en `maquinaEstados.ts` se conserva,
      sigue testeada).
- [x] 4.3 En `pages/MisPedidosPage.tsx`: ya no pasa `onEnviar`/`onCancelar`/`onReenviar`; se retiraron
      el modal de "Cancelar pedido" y sus hooks asociados.
- [x] 4.4 CSS en `pages/misPedidos.css`: fila clickeable (`.adoc-mp-row--clickeable`, hover/focus
      accesible), columna de Editar más ancha, bloque de filtro estilo Usuarios
      (`.adoc-mp-filtros`/`-fila`/`-input`/`-opcional`/`-quitar`).
- [x] 4.5 Tests en `MisPedidosPage.test.tsx`: click en una fila navega al detalle; el botón Editar
      aparece solo en pedidos editables y navega a `/designaciones/pedidos/:id/editar` sin disparar la
      navegación al detalle.

## 5. Cierre (primera ronda)

- [x] 5.1 `pnpm --filter frontend lint` + suite de tests del frontend completa: 137/137 tests, tsc
      limpio, eslint sin issues.
- [x] 5.2 Confirmado por grep: sin referencias a `MenuAccionesPedido`, `useCancelarPedido`/
      `cancelarPedido`, ni `onEnviar`/`onCancelar`/`onReenviar` en `pages/MisPedidosPage.tsx` /
      `components/TablaMisPedidos.tsx`.
- [x] 5.3 Actualizado `docs/product/designs/proyecto-docente-design-spec.md` (Flujo principal, Layout
      / IA de Mis pedidos, botonera del form, decisión de reenvío, alcance del patrón kebab).

## 6. Segunda ronda — Ver/Editar formato Usuarios, Eliminar, filtro angosto

- [x] 6.1 `api/maquinaEstados.ts`: predicado `puedeEliminarPedido(pedido, actor)` — `borrador` + actor
      Jefe de Cátedra (D-7).
- [x] 6.2 `api/pedidosStore.ts`: función `eliminar(id)` (saca del store, no es transición).
- [x] 6.3 `api/pedidosApi.ts`: `eliminarPedido(id, actor)` — valida `borrador` + rol JC vía
      `ErrorDominioPedido`; import de `ErrorDominioPedido` agregado a la importación de `maquinaEstados`.
- [x] 6.4 `hooks/useAccionesPedido.ts`: `useEliminarPedido(actor)` (invalida `["pedidos"]` en
      `onSuccess`, mismo patrón que las demás mutaciones).
- [x] 6.5 `components/lucide.tsx`: ícono `IconoX`.
- [x] 6.6 `components/ModalEliminarPedido.tsx` (nuevo): copia del patrón de `ModalEliminarPeriodo.tsx`
      (D-8) — título "Eliminar pedido", confirmación con el nombre del docente, aviso "no se puede
      deshacer", botones Cancelar/Eliminar.
- [x] 6.7 `components/TablaMisPedidos.tsx`: botones "Ver" (nuevo) y "Editar" reformateados a
      `Button variant="ghost" size="sm"` (D-6); X roja "Eliminar" (`<button>` nativo + `aria-label`)
      condicional a `puedeEliminarPedido`; todos con `stopPropagation` para no disparar la navegación de
      la fila.
- [x] 6.8 `pages/MisPedidosPage.tsx`: `useEliminarPedido` + estado del pedido a eliminar +
      `ModalEliminarPedido` wireado; `onEliminar` pasado a la tabla.
- [x] 6.9 `pages/misPedidos.css`: filtro Docente de `220px` a `150px` de `flex-basis`; última columna
      de la grilla ensanchada (96px → 220px) para Ver+Editar+X; clases `.adoc-mp-acc`/`.adoc-mp-eliminar`.
- [x] 6.10 `pages/DetallePedidoPage.tsx`: botón "Volver" pasa de link fijo a `navigate(-1)`; botón
      "Eliminar" (condicional a `puedeEliminarPedido`) junto a Volver, con `ModalEliminarPedido`; al
      confirmar, navega a `/designaciones/mis-pedidos`.
- [x] 6.11 Tests: `pedidosApi.test.ts` (3 casos: elimina borrador propio, rechaza si no es borrador,
      rechaza si el actor no es JC); `MisPedidosPage.test.tsx` (Ver siempre presente, X roja solo en
      borrador, click en Ver no duplica navegación, eliminar pide confirmación y saca la fila; ajustados
      los tests de Editar que usaban `.querySelector("button")` posicional, ahora ambiguo con "Ver"
      agregado — pasan a `within(fila).getByRole("button", {name:"Editar"})`); `flujoAprobacion.test.tsx`
      (Volver por los dos orígenes posibles vía `MemoryRouter` con `initialEntries`/`initialIndex`;
      eliminar un borrador desde su propio detalle).
- [x] 6.12 Specs: ADDED "Eliminar un pedido en borrador" + delta de la Lista (Ver/X roja/filtro
      angosto) en `specs/pedidos-designacion/spec.md`; delta del botón Volver en
      `rediseno-revision-solo-grilla/specs/aprobacion-pedidos-designacion/spec.md` (change separado, ya
      dueño de ese requirement).

## 7. Cierre (segunda ronda)

- [x] 7.1 `pnpm exec tsc --noEmit` + `pnpm exec vitest run` (146/146) + `pnpm exec eslint .` — todo
      verde. De paso se corrigió un error de tsc preexistente sin relación con esta ronda:
      `detalleAdapters.ts` (tema E) no tenía `despriorizar` en los mapas exhaustivos
      `VERBO_POR_ACCION`/`ETIQUETA_POR_ACCION` de `AccionHistorial`.
- [x] 7.2 `npx openspec validate --all --strict` — 19/19 items (specs + changes) OK.
- [x] 7.3 Actualizado `docs/product/designs/proyecto-docente-design-spec.md` para la segunda ronda:
      Flujo principal, Layout/IA de "Mis pedidos" (Ver, X roja, filtro Docente angosto), Layout/IA del
      detalle (Volver = navigate(-1), botón Eliminar), Decisiones de diseño (Eliminar gated a borrador,
      modal reusa el patrón de Períodos), patrón transversal de menú kebab (alcance actualizado).

## 8. Tercera ronda — filtro genérico reutilizable + Legajo

- [x] 8.1 `types.ts`: `DocentePedido.legajo?` (opcional, D-10); `DocenteExistente.legajo` (obligatorio).
- [x] 8.2 `api/catalogos.ts`: `legajo` en las 7 entradas de `DOCENTES_EXISTENTES` ("1001"–"1007").
- [x] 8.3 `api/pedidosSeed.ts`: `SemillaPedido.legajo?`; poblado en Laura Giménez ("1002"), Valeria
      Suárez ("1005"), Pablo Herrera ("1006") — mismos valores que su entrada en el catálogo — y Mariano
      Tévez ("2001", no está en el catálogo por ser de otra carrera). Brenda Ortiz (Alta) queda sin legajo
      a propósito (D-10).
- [x] 8.4 `components/PedidoForm.tsx`: `seleccionarDocente` propaga `legajo` desde el catálogo; el
      fallback de `opcionesDocente` (editar un pedido cuyo docente no está en el catálogo) usa
      `pedidoInicial.docente.legajo ?? ""`.
- [x] 8.5 `shared/ui/FiltrosLista.tsx` (nuevo, D-9) + `shared/ui/FiltrosLista.css` (nuevo): componente
      genérico config-driven (`fijos`/`opcionales`, controlado por `valores`/`onChange`); cada opcional
      declara su `valorInicial` propio para el reset al quitarlo. `shared/ui/FiltrosLista.test.tsx` (nuevo,
      5 tests): campo fijo dispara `onChange`, "+ Añadir filtro" solo ofrece los no agregados, reset a `""`
      (texto) vs. `valorInicial` (select), el selector desaparece cuando se agregaron todos los opcionales.
- [x] 8.6 `components/filtrosMisPedidos.ts`: `FiltrosMisPedidosState` suma `legajo` + índice de string
      (`[clave: string]: string`, requerido por la constraint genérica de `FiltrosLista`); `FILTROS_INICIALES`
      y `aplicarFiltrosMisPedidos` actualizados.
- [x] 8.7 `pages/MisPedidosPage.tsx`: reemplazado el bloque de filtro inline (JSX + estado
      `activados`/`disponibles` manual) por `<FiltrosLista fijos={FILTROS_FIJOS} opcionales={FILTROS_OPCIONALES} .../>`
      — config: fijos Docente/N°, opcionales Legajo/Tipo/Estado (Tipo y Estado con `valorInicial: "todos"`).
- [x] 8.8 `components/TablaMisPedidos.tsx` + `pages/misPedidos.css`: columna **LEGAJO** (mono, junto a
      Docente; "—" si falta); `--mp-cols` gana una columna (100px); el CSS del filtro se retira de
      `misPedidos.css` (movido a `shared/ui/FiltrosLista.css`, `.adoc-filtros-*`).
- [x] 8.9 Tests: `MisPedidosPage.test.tsx` (columna Legajo con su valor; agregar/quitar el filtro
      opcional Legajo).
- [x] 8.10 `pnpm exec tsc --noEmit` + `pnpm exec vitest run` (153/153) + `pnpm exec eslint .` — todo
      verde.
- [x] 8.11 Specs: delta de la Lista "Mis pedidos" en `specs/pedidos-designacion/spec.md` (legajo en la
      tabla + como filtro opcional).
- [x] 8.12 `npx openspec validate --all --strict` — 19/19 items OK.
- [x] 8.13 Actualizado `docs/product/designs/proyecto-docente-design-spec.md`: Layout/IA de "Mis
      pedidos" (columna Legajo, filtro Legajo, mención de `FiltrosLista`), Decisiones de diseño (legajo
      opcional en el pedido / obligatorio en el catálogo), nuevo "Patrón transversal — filtro de lista"
      documentando `shared/ui/FiltrosLista.tsx` como el patrón reutilizable de cara a futuras pantallas.

## 9. Cuarta ronda — Legajo obligatorio en Baja/Cambio [BR-designaciones-018]

- [x] 9.1 `pedidoValidacion.ts`: tercer `else if` en el bloque de campos comunes del docente — Baja o
      Cambio con `!datos.docente.legajo?.trim()` marca `errores.docente` (D-11).
- [x] 9.2 `pedidoValidacion.test.ts`: `datosBase()` gana `legajo: "1001"` por defecto (no rompe ningún
      test previo — Alta nunca lo necesitó, Baja/Cambio ya asumían un docente existente). Nuevo describe
      "BR-designaciones-018": `bajaExigeLegajo`, `cambioExigeLegajo` (ambos sin legajo → error), "Baja con
      legajo no marca error de docente", "en Alta no aplica la restricción".
- [x] 9.3 `docs/business-rules/designaciones.md`: nueva `BR-designaciones-018` (statement/rationale/
      provenance/fuente normativa pendiente/ejemplos/roles) + fila en el mapping a tests + nota en
      Assumptions (alcance acotado a Baja/Cambio, "Sin novedad" queda a confirmar); `pnpm generate-indexes`.
- [x] 9.4 Specs: nuevo MODIFIED "Adjuntos y justificación obligatorios por novedad" (título igual al de
      la spec base) en `specs/pedidos-designacion/spec.md`, con los 3 scenarios BR-002/003/004 existentes
      reproducidos (delta reemplaza el requirement completo) + 2 scenarios nuevos (Baja/Cambio exigen
      legajo; Alta no exige legajo).
- [x] 9.5 `pnpm exec tsc --noEmit` + `pnpm exec vitest run` (157/157) + `pnpm exec eslint .` — todo
      verde.
- [x] 9.6 `npx openspec validate --all --strict` — verificado.
- [x] 9.7 Actualizado `docs/product/designs/proyecto-docente-design-spec.md` (Decisiones de diseño del
      form: legajo obligatorio en Baja/Cambio, BR-018; mapping de BR extendido a BR-001..004, BR-018).

## 10. Quinta ronda — re-seed del legajo + botón Editar en el detalle

- [x] 10.1 Investigado por qué "los docentes no-Alta no tienen legajo" pese a que el seed (ronda 3) ya
      lo tenía poblado: `pedidosStore.ts` solo siembra si `localStorage` está vacío bajo su clave — un
      navegador con datos persistidos de antes de la ronda 3 sigue viendo el seed viejo. Confirmado por
      lectura de `pedidosSeed.ts` (Laura/Valeria/Pablo/Mariano ya tenían legajo; solo Brenda, Alta,
      correctamente sin él).
- [x] 10.2 `api/pedidosStore.ts`: bump de clave `adoc.mock.pedidos.v2` → `.v3` (D-12) — mismo mecanismo
      que el bump v1→v2 ya documentado en el archivo (SCRUM-8).
- [x] 10.3 `pages/DetallePedidoPage.tsx`: nuevo botón "Editar" (`variant="secondary"`, ícono
      `IconoSquarePen`, entre Volver y Eliminar) condicional a `puedeEditarPedido(pedido, actor)` — navega
      a `/designaciones/pedidos/:id/editar` (D-13). Gap real detectado: el detalle nunca tuvo esta acción,
      solo "Mis pedidos".
- [x] 10.4 Tests en `flujoAprobacion.test.tsx`: ruta stub `/designaciones/pedidos/:id/editar` agregada
      a `renderDetalle`; helper `idDevueltoDelJC()`; 3 tests nuevos — Editar en borrador propio navega,
      Editar en devuelto propio navega, Editar ausente en un pedido `en_revision_coordinador`.
- [x] 10.5 `pnpm exec tsc --noEmit` + `pnpm exec vitest run` (160/160) + `pnpm exec eslint .` — todo
      verde.
- [x] 10.6 Specs: delta del requirement "Detalle del pedido role-aware con cadena de aprobación e
      historial" en `rediseno-revision-solo-grilla/specs/aprobacion-pedidos-designacion/spec.md` (botón
      Editar + 2 scenarios nuevos); `design.md` de ese change amendado con la nota de corrección #2.
      `npx openspec validate --all --strict`.
- [x] 10.7 Actualizado `docs/product/designs/proyecto-docente-design-spec.md` (Layout/IA del detalle:
      botón Editar entre Volver y Eliminar, orden Volver·Editar·Eliminar; línea de solo-lectura por rol
      actualizada para reflejar Editar en borrador/devuelto).

## 11. Sexta ronda — se restauran los 7 pedidos de ejemplo retirados [revierte D-5, ver D-14]

- [x] 11.1 `api/pedidosSeed.ts`: restaurados Diego Morales (`Sin novedad`, borrador), Sofía Romano
      (`Baja`, borrador — cubre la novedad que el seed no tenía ningún ejemplo de), Martín Acosta
      (`Cambio`, borrador), Lucía Fernández (`Cambio`, `en_revision_coordinador`), Florencia Cabrera
      (`Cambio`, `en_revision_secretaria`), Hernán Vidal (`Cambio`, `en_revision_decanato`) y Gabriel
      Núñez (`Cambio`, `en_lote`) — `SEMILLAS` de "Ingeniería de Software" vuelve a 11 entradas (+ Mariano
      Tévez, otra carrera). Diego/Sofía/Lucía/Gabriel reusan DNI/legajo/cargo/dedicación/materia de su
      entrada en `DOCENTES_EXISTENTES`; Martín/Florencia/Hernán son DNIs/legajos nuevos sin colisión.
- [x] 11.2 `api/pedidosStore.ts`: bump `adoc.mock.pedidos.v3` → `.v4`.
- [x] 11.3 `pages/MisPedidosPage.test.tsx`: removida la aserción de conteo total de "Editar"
      (`getAllByRole(...).length === 2`), quedó obsoleta al crecer el seed (ahora hay más borradores); las
      aserciones por fila (`within(fila).queryByRole(...)`) ya cubrían la regla sin depender del tamaño
      del seed.
- [x] 11.4 `pnpm exec tsc --noEmit` + `pnpm exec vitest run` (160/160) + `pnpm exec eslint .` — todo
      verde.
- [x] 11.5 `npx openspec validate --all --strict`.
- [x] 11.6 Documentado en `proposal.md` (D-5 tachada, sección "Sexta ronda") y `design.md` (D-5
      marcada `[REVERTIDA, ver D-14]`, nueva D-14, riesgo del seed reducido tachado como moot).
