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
