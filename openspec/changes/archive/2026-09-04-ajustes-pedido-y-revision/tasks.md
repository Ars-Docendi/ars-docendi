## 1. Tipo `Novedad`: eliminar "Sin novedad" (fundacional — D-3)

- [x] 1.1 En `types.ts`, sacar `"Sin novedad"` del union type `Novedad`. Dejar que el compilador de
      TypeScript guíe el resto de esta sección (cada uso roto es un caller a corregir).
- [x] 1.2 `PedidoForm.tsx`: `NOVEDADES` pasa a `["Alta", "Baja", "Cambio de cargo o dedicación"]`;
      `datosIniciales()` defaultea `novedad: pedido?.novedad ?? "Alta"` (D-1); eliminar la constante
      `esSinNovedad` y el condicional `!esSinNovedad` que envolvía "Justificación" y
      `SeccionAdjuntosPedido` (con solo 3 novedades, esas secciones son siempre aplicables).
- [x] 1.3 `SeccionDocentePedido.tsx` y `DatosActualesPanel.tsx`: eliminar la prop `mostrarMateria` (y la
      columna "Materia" de la franja superior que controlaba) — sin "Sin novedad" queda sin ningún
      caller que la ponga en `true` (D-3).
- [x] 1.4 `MisPedidosPage.tsx` y `TableroRevisionPage.tsx`: sacar `"Sin novedad"` de los arrays/opciones
      del filtro Tipo.
- [x] 1.5 `NovedadChip.tsx` y `ModalConfirmacionAccion.tsx`: sacar la entrada `"Sin novedad"` de sus
      `Record<Novedad, …>` (el compilador ya lo exige al achicar el tipo).
- [x] 1.6 `api/pedidosSeed.ts`: eliminar las semillas de precarga "Sin novedad" (comentario "docentes
      del período anterior precargados") y el comentario de cabecera que las describe — sin migrarlas a
      otra novedad (D-3). Revisar que el resto del seed (Alta/Baja/Cambio en varios estados) siga
      cubriendo los escenarios de prueba existentes.
- [x] 1.7 Revisar `pedidoValidacion.ts` y `api/maquinaEstados.ts`: confirmar que no quede ninguna
      referencia textual a `"Sin novedad"` (no deberían tener ramas propias — ver D-3 — pero
      verificar tras el achique del tipo).
- [x] 1.8 `docs/business-rules/designaciones.md`: actualizar la nota de BR-018 en "Assumptions (a
      confirmar)" que menciona a "Sin novedad" — ya no aplica, esa novedad no existe.

## 2. Botón "Docente es agente externo" (D-2)

- [x] 2.1 `types.ts`: agregar `esAgenteExterno: boolean` a `DatosEditablesPedido` y
      `PedidoDesignacion`.
- [x] 2.2 `PedidoForm.tsx`: `datosIniciales()` defaultea `esAgenteExterno: pedido?.esAgenteExterno ??
false`; al seleccionar un docente existente (`seleccionarDocente`), arranca en `false` (sin
      "valor actual" — D-2, no hay campo `*Actuales` en `DocenteExistente`).
- [x] 2.3 `SeccionDesignacionSolicitada.tsx`: agregar `Checkbox` "Docente es agente externo" (de
      `@ars-docendi/ui`) junto al `Field` de "Horas externas (otro depto.)"; nuevas props
      `esAgenteExterno: boolean` / `onEsAgenteExterno: (valor: boolean) => void`.
- [x] 2.4 `ResumenPedido.tsx`: agregar la fila "Agente externo" (Sí/No) junto a "Horas externas" en el
      detalle del pedido.
- [x] 2.5 Confirmar que el checkbox solo se muestra en Alta y Cambio (misma condición `muestraSolicitud`
      que ya gatea toda la sección `SeccionDesignacionSolicitada`).

## 3. Filtro Carrera y columna Carrera en Revisión (D-5, D-6)

- [x] 3.1 `components/filtrosTablero.ts`: agregar constante con el catálogo cerrado de 5 carreras
      ("Ingeniería en Informática", "Ingeniería Industrial", "Ingeniería Civil", "Ingeniería Mecánica",
      "Ingeniería Electrónica" — ver nota abierta sobre el nombre exacto en `proposal.md`) y el mapeo
      cerrado nombre completo → abreviatura (Informática/Industrial/Civil/Mecánica/Electrónica — D-6);
      agregar `carrera` a `FiltrosTablero` y a `FILTROS_INICIALES` (renombrado de `propuesta` en la
      segunda ronda); `aplicarFiltros` filtra por igualdad exacta contra `pedido.carrera` cuando el
      filtro está activo.
- [x] 3.2 `pages/TableroRevisionPage.tsx`: agregar "Carrera" a `FILTROS_OPCIONALES` (`tipo: "select"`,
      mismo patrón que Prioridad), con las 5 opciones del catálogo.
- [x] 3.3 Confirmar que el filtro se combina por AND con el resto (vista/Nombre/Tipo/Legajo/Prioridad),
      reusando el mismo mecanismo de `FiltrosLista` — sin lógica nueva de combinación.
- [x] 3.4 `components/TablaRevision.tsx`: agregar la columna **Carrera** (entre Legajo y Asignatura) en
      el head y en cada fila de las 4 secciones, mostrando el nombre abreviado vía el mapeo de 3.1.
- [x] 3.5 `components/revision.css`: actualizar `grid-template-columns` de `.adoc-tabla-head`/
      `.adoc-tabla-row` de 7 a 8 columnas para acomodar Carrera.

## 4. Devolución más llamativa en la Tabla de revisión (D-4, superado por D-7 en la sección 7)

- [x] 4.1 ~~`.adoc-estado-avance.alerta` pill lleno~~ — implementado en la primera ronda, **revertido**
      en la ronda 2 (sección 7): el fondo lleno se movió de la celda a la fila entera.
- [x] 4.2 `components/EstadoAvance.tsx`: sin cambios de contenido en ninguna ronda.
- [x] 4.3 Verificado visualmente en ambas rondas.

## 5. Tests

- [x] 5.1 Actualizar fixtures que siembran `novedad: "Sin novedad"` en:
      `PedidoForm.test.tsx`, `pedidoValidacion.test.ts`, `maquinaEstados.test.ts`,
      `detalleAdapters.test.ts`, `EstadoAvance.test.tsx`, `ModalConfirmacionAccion.test.tsx`,
      `TablaRevision.test.tsx`, `tableroRevisionModelo.test.ts`, `pedidosApi.test.ts` — reescribir cada
      caso a la novedad que corresponda (Alta/Baja/Cambio) sin perder lo que el test verificaba.
- [x] 5.2 `PedidoForm.test.tsx`: reemplazar el test "en 'Sin novedad' muestra el selector de docente y
      oculta solicitud/documentación" por un test equivalente sobre el nuevo default ("Alta" al abrir un
      pedido nuevo) y agregar un test para el checkbox "Docente es agente externo" (aparece en Alta y
      Cambio, se guarda en `esAgenteExterno`).
- [x] 5.3 Agregar test de `filtrosTablero.ts` (`aplicarFiltros`) para el filtro Propuesta: acota por
      carrera exacta, se combina con Tipo/Nombre.
- [x] 5.3b Agregar test de `TablaRevision.tsx` (o del mapeo en `filtrosTablero.ts`) que confirme la
      abreviatura correcta para cada una de las 5 carreras del catálogo.
- [x] 5.4 Agregar/ajustar test de `EstadoAvance.tsx` (o snapshot de clases CSS) confirmando que un
      pedido `devuelto` recibe la clase que aplica el fondo lleno.
- [x] 5.5 Correr la suite completa del feature (`frontend`) y confirmar 0 tests rotos por el achique del
      tipo `Novedad`.

## 7. Fila entera roja/amarilla para prioritario/devuelto (D-7, segunda ronda)

- [x] 7.1 `components/TablaRevision.tsx`: `claseFondoFila(pedido)` — `adoc-tabla-row--prioritario` si
      `pedido.prioritario`, si no `adoc-tabla-row--devuelto` si `estado === "devuelto"`, si no `""`.
      Prioritario gana el empate (confirmado con el cliente).
- [x] 7.2 `components/revision.css`: `.adoc-tabla-row--prioritario` (fondo `--danger-100`) y
      `.adoc-tabla-row--devuelto` (fondo `--warning-100`), con `:hover` que mantiene el fondo
      (`filter: brightness(0.96)` en vez de resetear a `--color-bg-canvas`).
- [x] 7.3 Revertir el pill de `.adoc-estado-avance.alerta` (tarea 4.1) a texto de color plano — el fondo
      lleno ahora vive en la fila, un pill amarillo dentro de una fila amarilla es ruido.
- [x] 7.4 Tests en `TablaRevision.test.tsx`: fila devuelta tiene la clase `--devuelto`; fila prioritaria
      tiene la clase `--prioritario` (y la no prioritaria no); un pedido prioritario Y devuelto tiene
      `--prioritario` y NO `--devuelto`.

## 8. Materias opcionales en Alta y Cambio (D-8 — segunda ronda: solo Alta; tercera ronda: extendido a Cambio)

- [x] 8.1 (segunda ronda) `pedidoValidacion.ts`: la validación "al menos una materia" pasa a
      `datos.novedad !== "Alta" && datos.asignaciones.length === 0`.
- [x] 8.1b (tercera ronda) `pedidoValidacion.ts`: se acota más — pasa a
      `datos.novedad === "Baja" && datos.asignaciones.length === 0` (Alta y Cambio quedan sin mínimo;
      Baja es la única que lo sigue exigiendo).
- [x] 8.2 (segunda ronda) `components/SeccionMateriasHoras.tsx`: nueva prop `permiteVacio` — el botón
      "Quitar" se muestra incluso en la última fila cuando está en `true`.
- [x] 8.2b (tercera ronda) `components/SeccionMateriasHoras.tsx`: **se elimina** la prop `permiteVacio`
      — el botón "Quitar" se muestra siempre que `!soloLectura`, sin condición de longitud (ya no hace
      falta distinguir, Alta y Cambio se comportan igual).
- [x] 8.3 (segunda ronda, superado) `components/SeccionDesignacionSolicitada.tsx`: prop `esAlta` →
      **eliminada en la tercera ronda** junto con `permiteVacio` (quedaba sin uso).
- [x] 8.4 (tercera ronda) `components/PedidoForm.tsx`: se saca el prop `esAlta` pasado a
      `SeccionDesignacionSolicitada`; `quitarMateria` **pierde el guard por completo** (la función nunca
      se invoca para Baja, no hace falta distinguir novedad).
- [x] 8.5 Tests: `pedidoValidacion.test.ts` (Alta y Cambio con `asignaciones: []` no tienen errores;
      Baja sigue exigiendo mínimo 1) y `PedidoForm.test.tsx` (Alta y Cambio permiten vaciar la lista
      completa y guardar/enviar).

## 9. Renombrar filtro "Propuesta" a "Carrera" (segunda ronda)

- [x] 9.1 `filtrosTablero.ts`: `FiltrosTablero.propuesta` → `carrera`; `FILTROS_INICIALES` y
      `aplicarFiltros` actualizados.
- [x] 9.2 `TableroRevisionPage.tsx`: `clave`/`etiqueta` del filtro opcional pasan de "propuesta"/
      "Propuesta" a "carrera"/"Carrera"; label de la opción "todos" pasa a "Carrera: Todas".
- [x] 9.3 Tests: `filtrosTablero.test.ts` y `TablaRevision.test.tsx` actualizados a la clave `carrera`.

## 10. Botones Ver/Editar/Eliminar fijos en Mis Pedidos (D-9, segunda ronda)

- [x] 10.1 `components/TablaMisPedidos.tsx`: "Editar" y la X de "Eliminar" dejan de estar envueltos en
      `{condición && <Button>}` — se renderizan siempre, con `disabled={!condición}`
      (`puedeEditarPedido`/`puedeEliminarPedido`).
- [x] 10.2 `pages/misPedidos.css`: `.adoc-mp-acc :disabled { opacity: 0.4; cursor: not-allowed; }` +
      `.adoc-mp-eliminar:disabled:hover` sin fondo rojo.
- [x] 10.3 Tests: `MisPedidosPage.test.tsx` (Editar/Eliminar siempre presentes, habilitados/
      deshabilitados según el pedido) y `flujoAprobacion.test.tsx` (tras reenviar, Editar queda
      deshabilitado en vez de ausente).

## 12. Departamento a cargo del agente externo (D-10, cuarta ronda)

- [x] 12.1 `types.ts`: nuevo tipo `DepartamentoAgenteExterno` (unión cerrada de 7 literales); campo
      opcional `departamentoAgenteExterno` en `PedidoDesignacion` y `DatosEditablesPedido`.
- [x] 12.2 `api/catalogos.ts`: nueva constante `DEPARTAMENTOS_AGENTE_EXTERNO` con las 7 opciones.
- [x] 12.3 `pedidoValidacion.ts`: nuevo campo `departamentoAgenteExterno` en `CampoPedido`; error si
      `esAgenteExterno` es `true` y no hay departamento seleccionado.
- [x] 12.4 `SeccionDesignacionSolicitada.tsx`: `Select` "Departamento a cargo" condicional (solo si
      `esAgenteExterno`), nuevas props `departamentoAgenteExterno`/`onDepartamentoAgenteExterno`.
- [x] 12.5 `PedidoForm.tsx`: nueva función `cambiarEsAgenteExterno` que limpia
      `departamentoAgenteExterno` al desmarcar el checkbox; `datosIniciales()` y `seleccionarDocente`
      actualizados.
- [x] 12.6 `api/pedidosApi.ts`: `crearPedido` persiste `departamentoAgenteExterno`.
- [x] 12.7 `ResumenPedido.tsx`: la fila "Agente externo" muestra el nombre del departamento cuando
      corresponde, en vez de solo "Sí"/"No".
- [x] 12.8 Tests: `pedidoValidacion.test.ts` (exige departamento solo si `esAgenteExterno`) y
      `PedidoForm.test.tsx` (selector aparece/desaparece con el checkbox, se limpia al desmarcar,
      bloquea "Guardar y enviar" sin departamento).

## 14. Flechita de devuelto junto a la bandera de prioridad (D-11 — quinta ronda, corregido en la sexta)

- [x] 14.1 `NovedadChip.tsx`: nuevo `DevueltoFlechaIcono` (ícono `corner-up-left`, mismo que
      `EstadoPedidoPill` usa para "Devuelto"), color `--warning-500`.
- [x] 14.2 (quinta ronda, descartado) `TablaRevision.tsx`/`revision.css`: dos casilleros fijos
      (`.adoc-tabla-prio-slot`) — **revertido en la sexta ronda**, el cliente marcó que un ícono solo
      quedaba mal posicionado (pegado a un costado en vez de centrado).
- [x] 14.3 (sexta ronda) `TablaRevision.tsx`: se sacan los casilleros — los dos íconos vuelven a ser
      hijos condicionales directos de `.adoc-tabla-prio` (bandera primero, flechita después).
- [x] 14.4 (sexta ronda) `revision.css`: se elimina `.adoc-tabla-prio-slot`; `.adoc-tabla-prio` suma
      `gap: 6px` sobre el `justify-content: center` que ya tenía — centra 1 ícono solo, o el par con
      espacio en el medio si hay 2.
- [x] 14.5 `revision.css`: columna Prioritario del grid pasa de `44px` a `60px` (lugar para 2 íconos +
      gap).
- [x] 14.6 Tests en `TablaRevision.test.tsx`: un pedido devuelto (no prioritario) muestra solo la
      flechita; con los dos, la celda tiene exactamente 2 hijos, bandera primero y flechita segunda
      (por `aria-label`).

## 15. Verificación final

- [x] 15.1 `openspec validate ajustes-pedido-y-revision --strict` sin errores.
- [x] 15.2 Lint + typecheck + tests del frontend en verde (209/209).
- [x] 15.3 Probar manualmente en el navegador: form de pedido (Alta sin materias, checkbox de agente
      externo), Revisión (filtro/columna Carrera, filas roja/amarilla), Mis Pedidos (botones fijos
      semitransparentes cuando no aplican) — validado por el cliente.
- [x] 15.4 Probar manualmente el selector de departamento a cargo (aparece/desaparece con el checkbox,
      exige selección para enviar) — validado por el cliente ("funciona perfecto").
- [x] 15.5 Probar manualmente el ícono de devuelto/bandera en Revisión (centrado con 1 solo, separados
      con los 2) — validado por el cliente con el ejemplo de Verónica Salas (prioritario + devuelto).

## 16. Rediseño de columnas de la Tabla de revisión (D-12 — séptima ronda)

El cliente pidió sacar el color por estado de la fila. Al revisarlo salió el problema de fondo: las
secciones ya son etapas del circuito, así que la celda Estado repetía la etapa, y el fondo de fila
repetía por tercera vez señales que ya vivían en su propia celda. Cada señal pasa a tener una columna.

- [x] 16.1 `TablaRevision.tsx` / `revision.css`: se eliminan las columnas **Carrera** y **Asignatura**
      — una designación puede tener más de una de cada una, así que la celda no las mostraba completas.
      Ambas siguen enteras en el detalle; Carrera sigue existiendo como filtro.
- [x] 16.2 `revision.css`: se elimina el fondo de fila por estado (`.adoc-tabla-row--prioritario` y
      `--devuelto`) y con él la prevalencia arbitraria entre prioritario y devuelto — antes, un pedido
      que era las dos cosas mostraba solo el rojo y tapaba la devolución.
- [x] 16.3 `EstadoAvance` → `SituacionPedido` (archivo y test renombrados): la celda deja de repetir
      la etapa que ya dice el header de la sección. En revisión muestra solo el stepper (la etapa queda
      en un texto `.adoc-sr-only` para lectores de pantalla); devuelto suma un chip ámbar; Aceptado y
      Rechazado pasan de "dot + texto teñido" a chip verde / rojo.
- [x] 16.4 `tableroRevisionModelo.ts`: nuevo `teLoDevolvieron` (`propietarioActual` === rol del actor).
      El chip de un devuelto dice "Devuelto — corregís vos" cuando la pelota es del actor, y "Devuelto
      por {nombre}" cuando es de otro; el rol queda en el `title`, no en el texto visible.
- [x] 16.5 Nueva columna **Prioridad** (con header, antes era una columna muda de 60px): chip rojo
      "Prioritario" con la bandera. Reemplaza al fondo rojo de fila y, a diferencia de un fondo, es una
      columna real — ordenable y filtrable más adelante.
- [x] 16.6 `tableroRevisionModelo.ts`: nuevos `inicioEnCircuito` y `ultimaActualizacion` (fecha + días
      transcurridos). **Inicio** toma el primer `enviar`, no el `crear` del borrador: el tiempo en
      borrador no es tiempo de revisión y haría incomparables dos filas.
- [x] 16.7 `instanteDeCorte`: en un pedido cerrado los días se congelan en la fecha de cierre, para que
      se lean "tardó N días" en vez de seguir creciendo contra hoy. Un contador de 0 días no se muestra.
- [x] 16.8 Nueva columna **Inicio** + `Fecha última actualización` renombrada a **Últ. actualización**,
      ambas con los días en gris al lado — el par (24 d de vida / 3 d parado acá) es lo que hace
      comparables dos filas de un vistazo.
- [x] 16.9 `revision.css`: chip genérico `.adoc-chip` con tonos, compartiendo la regla base con
      `.adoc-novedad-chip` — con el fondo de fila afuera, los chips son los únicos portadores de color
      y tienen que leerse como un solo lenguaje en las tres columnas.
- [x] 16.10 Limpieza de código muerto: `PrioridadFlagIcono` y `DevueltoFlechaIcono` (los reemplazan los
      chips), y las reglas `.adoc-estado-avance*`, `.adoc-estado-dot`, `.adoc-bandera-*` de
      `revision.css`. Deja sin efecto las tasks 14.1–14.6.
- [x] 16.11 Grid de 8 a 7 columnas, con los mínimos calibrados para que entren en el ancho de contenido
      de una pantalla de 1440 y el sobrante vaya a Situación (la única celda con texto largo).
- [x] 16.12 Tests actualizados y agregados: `SituacionPedido.test.tsx` (7 casos, reescrito),
      `TablaRevision.test.tsx` (columnas nuevas, sin fondo de fila) y `tableroRevisionModelo.test.ts`
      (contadores de días con `vi.setSystemTime`, `teLoDevolvieron`). 201/201 en verde.
- [x] 16.13 Validar con el cliente en el navegador.

## 17. Pestañas + `Table` del design system, y CSS a la guía (D-13 — octava ronda)

El cliente marcó dos cosas: que las secciones desplegables (idea nuestra, no pedido suyo) no le
convencían, y que la grilla de revisión usaba un formato distinto al del resto del sistema.

- [x] 17.1 Auditoría: `TablaRevision` y `TablaMisPedidos` eran las **únicas** tablas del sistema
      hechas a mano con CSS Grid de `div`/`span`. Usuarios, Docentes, Roles y Períodos usan el
      componente `Table` de `@ars-docendi/ui`. Además, el chip propio duplicaba `StatusBadge`, que ya
      define los `kind` `devuelto`, `aprobado`, `rechazado` y `prioritario`.
- [x] 17.2 `TablaRevision.tsx` reescrito: `Tabs` + **una sola** `Table` del design system. Las 4
      secciones desplegables (4 heads repetidos, sin poder comparar entre etapas) pasan a 5 pestañas
      con contador: Mi bandeja / En Coordinación / En Secretaría / En Decanato / Finalizados.
- [x] 17.3 `tableroRevisionModelo.ts`: `construirColumnas`/`seccionInicialDelActor` → `PESTANIAS`,
      `pedidosDePestania`, `pestaniaInicial`. Se elimina el mini-stepper (`avancePedido`,
      `avanceEtapaRetorno`, `PASO_DE_ETAPA`, `TOTAL_PASOS`): era 100% derivable de la etapa.
- [x] 17.4 (revertido en 18.1) Pestaña "Mi bandeja" con `esTrabajoTuyo` = `esTuTurno` OR
      `teLoDevolvieron`.
- [x] 17.5 `SituacionPedido.tsx` eliminado junto con su CSS de chips propios. La celda Estado ahora usa
      `EstadoPedidoBadge` (→ `StatusBadge`), que suma el badge "Prioritario" al lado — los dos a la
      vez, sin la prevalencia arbitraria que tenía el fondo de fila.
- [x] 17.6 `EstadoPedidoBadge` acepta `etiqueta` opcional: un Devuelto dice de quién depende que avance
      ("Devuelto — corregís vos" / "Devuelto — espera a {área}") sin duplicar el mapeo de kinds.
- [x] 17.7 `areaQueCorrige`: un devuelto queda a cargo del **área**, no de una persona. El nombre de
      quien devolvió pasa al `title` (auditoría, no trabajo pendiente).
- [x] 17.8 Se eliminan los contadores de días de las columnas de fecha (pedido del cliente: con la
      fecha alcanza) y el filtro `vista` con su `Select` suelto, reemplazado por la pestaña Mi bandeja.
- [x] 17.9 `revision.css` de 347 a ~130 líneas: solo el chip de novedad, el avatar y la celda Docente.
      Toda la grilla a mano, los chips propios y las secciones desplegables se fueron.
- [x] 17.10 CSS a la guía en toda la app: 174 sustituciones exactas (61 `font-size`, 55 `font-weight`,
      57 de espaciado, 11 blancos crudos → `--color-bg-raised`), sin cambio visual. Más `font-weight`
      700/300 → `--weight-semibold`/`--weight-regular` (la escala corta en 600) y los colores crudos de
      `index.css` → tokens.
- [x] 17.11 Sombras difusas → elevación por anillo: `estado-acciones.css` usa `--elev-3`. El design
      system separa planos con anillos, no con blur.
- [x] 17.12 Muestrario del seed (`pedidosSeed.ts` v5): un ejemplo de cada caso visto desde Secretaría
      —prioritario puro, devuelto a otra área, devuelto al propio actor, prioritario + devuelto,
      aceptado prioritario— con fechas variadas vía `diasDesdeEnvio`/`diasDesdeUltimoEvento`.
- [x] 17.13 Tests reescritos: `TablaRevision.test.tsx` (14 casos sobre pestañas + `Table`),
      `tableroRevisionModelo.test.ts` (pestañas, `pestaniaInicial`, `etiquetaEstado`, `areaQueCorrige`)
      y `TableroRevisionPage.test.tsx`. 210/210 en verde.
- [x] 17.14 Pendiente de decisión del cliente: los `font-size` de 11px/10px/9px y los de 15/17/22px
      quedan fuera de la escala tipográfica (que va de 12px a 36px). Se dejaron como están.
- [x] 17.15 `TablaMisPedidos` sigue siendo una grilla a mano: migrarla a `Table` para no dejar dos
      criterios conviviendo.
- [x] 17.16 Validar con el cliente en el navegador.

## 18. Pestaña "Todos" y badge locativo (D-14 — novena ronda)

El cliente marcó que "Mi bandeja" confundía —para un revisor es el mismo conjunto que la pestaña de su
propia etapa, con dos rótulos distintos— y pidió una pestaña para ver todo, y que el badge diga
siempre dónde está el pedido ahora.

- [x] 18.1 Fuera la pestaña "Mi bandeja" y `esTrabajoTuyo`/`teLoDevolvieron`. Las pestañas quedan:
      **Todos · En Coordinación · En Secretaría · En Decanato · Finalizados**.
- [x] 18.2 Nueva pestaña "Todos": el ámbito sin agrupar, sin los `borrador` (no entraron al circuito).
      No filtra nada más, así que ningún pedido del ámbito queda invisible.
- [x] 18.3 **Bug encontrado y arreglado**: los pedidos `cancelado` no caían en ninguna sección ni
      pestaña — eran invisibles en el tablero. Ahora entran en "Todos" y en "Finalizados", que pasa a
      cubrir los tres terminales (`en_lote`, `rechazado`, `cancelado`).
- [x] 18.4 `pestaniaInicial`: Administración abre en "Todos" (antes "Mi bandeja").
- [x] 18.5 `etiquetaEstado` deja de depender del actor y pasa a ser **locativa**: un devuelto dice
      "Devuelto · en {área}" tomando `propietarioActual` —dónde está el pedido ahora— en vez de
      `etapaRetorno`, que es a dónde va a volver. Se van las variantes "corregís vos" / "espera a".
- [x] 18.6 `pedidosDePestania` ya no recibe el actor: el reparto es igual para todos los que ven la
      pantalla, solo cambia en qué pestaña abre cada rol.
- [x] 18.7 Tests actualizados (209/209 en verde) y spec del change reescrita.
- [x] 18.8 Resuelto en la sección 20: la pestaña pasa a seguir a `propietarioActual`, igual que el
      badge, y se suma la pestaña "En Cátedra" para los devueltos que quedan del lado de la Cátedra.

## 19. Seed de devoluciones consistente con BR-014 (D-15)

- [x] 19.1 **Bug de datos**: la semilla de Camila Ferreyra declaraba `etapaRetorno:
"en_revision_secretaria"` con `propietarioActual: "Secretaría"`, una combinación que la máquina
      de estados nunca produce — devuelto desde Secretaría lo corrige el Coordinador [BR-014].
- [x] 19.2 Arreglo de raíz: `SemillaPedido` ya no declara `propietarioActual` ni `devueltoPor`. Solo
      declara `etapaRetorno`, y de ahí se derivan quién firma la devolución (`REVISOR_DE_ETAPA`) y
      quién corrige (`CORRIGE_LO_DEVUELTO_DESDE`), espejo de `ROL_DE_ETAPA` y `PROPIETARIO_DEVOLUCION`
      de `maquinaEstados.ts`. La semilla ya no puede expresar una combinación inválida.
- [x] 19.3 `pedidosStore` v6 para forzar el re-seed. Verificado en el navegador: los 6 devueltos del
      seed cumplen BR-014.

## 20. Pestañas por área donde ESTÁ el pedido (D-16 — décima ronda)

Cerrando la incoherencia que el cliente detectó en pantalla: Pablo Herrera aparecía bajo "En
Coordinación" con el badge "Devuelto · en Cátedra". La pestaña seguía `etapaRetorno` (a dónde vuelve)
y el badge `propietarioActual` (dónde está).

- [x] 20.1 `perteneceAEtapa` → `perteneceAArea`: un devuelto se ubica por `propietarioActual`, no por
      `etapaRetorno`. Pestaña y badge pasan a decir lo mismo.
- [x] 20.2 Nueva pestaña **En Cátedra**: la Cátedra no revisa, pero retiene los devueltos desde
      Coordinación, pendientes de corregir y reenviar. Antes se veían bajo "En Coordinación", como si
      el Coordinador todavía los tuviera.
- [x] 20.3 `ETIQUETA_ETAPA` + `PESTANIA_DE_ETAPA` → una sola tabla `AREAS` (id, etiqueta, rol y etapa
      opcional). La Cátedra es el área sin etapa de revisión.
- [x] 20.4 Pestañas finales: **Todos · En Cátedra · En Coordinación · En Secretaría · En Decanato ·
      Finalizados**, que se leen como el circuito de izquierda a derecha.
- [x] 20.5 Test nuevo: "En Decanato nunca tiene devueltos" — sale solo de BR-014 (la devolución
      retrocede un nivel y no hay etapa por encima de Decanato). 211/211 en verde.

## 21. El badge no repite lo que dice la pestaña (D-17)

- [x] 21.1 `etiquetaEstado` pasa a depender de la pestaña abierta: dentro de una pestaña de área el
      badge dice "En revisión" / "Devuelto" a secas; en "Todos" nombra el área ("En revisión ·
      Coordinador", "Devuelto · en Cátedra"). Antes, dentro de "En Coordinación" cada fila repetía
      "Coordinador" o "Coordinación", que es justo lo que la pestaña ya dice.
- [x] 21.2 Tests: 5 casos nuevos de `etiquetaEstado` por pestaña + los de `TablaRevision` verifican el
      cambio de etiqueta al pasar de la pestaña de área a "Todos". 213/213 en verde.
- [x] 21.3 Abierto: el badge de `en_lote` dice "En lote" (etiqueta por defecto de `EstadoPedidoBadge`).
      Es jerga interna; "Aceptado" se lee mejor. Pendiente de decidir con el cliente.

## 22. Badge "Prioritario" en la paleta danger (D-18)

- [x] 22.1 El badge pasa de `neutral-900` sólido (el único invertido del sistema) a `danger` outline —
      `--danger-100` / `--danger-700` / `--danger-500`—, el mismo tratamiento que los otros seis. Va en
      `frontend/src/index.css`, no en el CSS de la feature, porque el badge se usa en toda la app.
- [x] 22.2 Registrado como **TD-004**: es un override local de `@ars-docendi/ui`, o sea CSS nuestro
      pisando la librería — justo el patrón que este change vino a eliminar. Corresponde subirlo a
      `ui-lib`, publicar release y bumpear la dependencia. Riesgo comunicado y aceptado por el cliente:
      queda visualmente idéntico al badge "Rechazado" (misma paleta), que puede convivir con un
      prioritario en las pestañas "Todos" y "Finalizados".

## 23. Estado y Área como columnas separadas (D-19)

- [x] 23.1 La columna Estado se parte en dos: **Estado** (el estado desnudo: "En revisión",
      "Devuelto", "En lote", "Rechazado") y **Área** ("Cátedra" / "Coordinación" / "Secretaría" /
      "Decanato", o "—" en los terminales). Reemplaza a la solución de la sección 21, donde la
      etiqueta del badge cambiaba según la pestaña.
- [x] 23.2 `etiquetaEstado` deja de depender de la pestaña: el badge nunca lleva el área.
- [x] 23.7 La columna **Área** aparece SOLO en la pestaña "Todos". En una pestaña de área su valor
      sería idéntico en todas las filas y ya lo dice la pestaña; en Finalizados no hay área.
- [x] 23.3 Nuevo `areaActual`, derivado con el mismo criterio que el reparto en pestañas (la etapa que
      revisa, o `propietarioActual` si está devuelto): columna y pestaña no pueden contradecirse.
- [x] 23.4 `.adoc-estado-badges` sin `flex-wrap`: con la columna más angosta, Estado + Prioritario se
      apilaban y esas filas quedaban más altas que el resto.
- [x] 23.5 Borradas 29 líneas de CSS huérfano (`.adoc-tablero-filtros`, del Select de "vista" que se
      eliminó en la sección 18). `revision.css` quedó en 97 líneas, desde las 347 originales.
- [x] 23.6 Tests actualizados: 5 casos de `etiquetaEstado`/`areaActual` + la columna Área en los
      headers. 213/213 en verde.

## 24. Filtros y ordenamiento por columna (D-20)

- [x] 24.1 **Ordenamiento por columna**, cableando el `sort` / `onSortChange` que `Table.HeaderCell` ya
      traía y ninguna pantalla del sistema usaba. Ciclo asc → desc → sin orden manual, para poder
      volver al orden por defecto de la pestaña, que es información y no un orden arbitrario. Fechas
      por epoch ms (no por el texto "dd/mm/aaaa") y legajos con `localeCompare` numérico.
- [x] 24.2 Filtro **Período** en vez del rango de fechas que se había implementado primero: los
      períodos son una entidad del dominio, ya creados y nombrados, y el pedido ya tiene `periodoId`.
      Un control en vez de dos y sin adivinar fechas de corte.
- [x] 24.3 Filtro **Sin movimiento** (+7 / +15 / +30 días sobre el último evento del historial):
      reemplaza al contador de días por fila que el cliente pidió sacar.
- [x] 24.4 El filtro **Carrera** solo se le ofrece a quien ve más de una carrera. Para un Coordinador,
      cuyo ámbito ES una carrera [BR-009], no acotaba nada.
- [x] 24.5 (revertido) Se probó mover los filtros a `Table.Toolbar` con todos visibles. El cliente lo
      marcó como ruido visual —siete controles en dos filas— y además los filtros dentro de la tabla se
      leían como si fueran de la pestaña abierta, cuando en realidad son globales. Vuelven arriba de
      las pestañas, con `FiltrosLista` y su revelado progresivo.
- [x] 24.6 `FiltrosLista`: el "+ Añadir filtro" pasa de un `<select>` crudo con la clase interna
      `.adoc-select` copiada a mano al componente `Select` de la librería.
- [x] 24.7 Tests: rango de período, días sin movimiento, orden por columna y su ciclo, y el filtro
      Carrera según rol. 228/228 en verde.
- [x] 24.8 **Período** arranca en el período abierto (`activo`), no en "Todos": un revisor trabaja
      sobre el período en curso. Por arrancar aplicado pasa a ser filtro **fijo** y no opcional — uno
      que acota desde el vamos no puede estar escondido, o se ve una lista recortada sin saber por qué.
      "Todos los períodos" queda al final de la lista y el abierto se rotula "(abierto)".
- [x] 24.9 Nota de datos: todas las semillas están en el período "1", así que cambiar de período deja
      la tabla vacía. Sumar pedidos de otros períodos si se quiere demostrar el filtro.
