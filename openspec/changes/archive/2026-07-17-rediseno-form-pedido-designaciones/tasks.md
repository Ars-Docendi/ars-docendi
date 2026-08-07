## 1. Modelo de datos y catálogos

- [x] 1.1 En `frontend/src/features/designaciones/types.ts`: agregar `AsignacionMateria { materia: string; horas: number }`; reemplazar `materiaAsociada: string` por `asignaciones: AsignacionMateria[]` en `PedidoDesignacion` y `DatosEditablesPedido`; agregar `horasExternas: number` y `horasInvestigacion: number` (dejan de ser opcionales, default `0`); agregar `tipoBaja?: "Renuncia" | "Jubilación" | "Otro"` y `tipoBajaDetalle?: string`; eliminar `haceHorasOtroDepto`.
- [x] 1.2 Extender el tipo `Dedicacion` con `"Categoría 0"`.
- [x] 1.3 En `api/catalogos.ts`: agregar `"Categoría 0"` a `DEDICACIONES` (queda 0–6); reemplazar `DocenteExistente.materiaActual: string` por `materiasActuales: AsignacionMateria[]` (mínimo 1 elemento) y migrar los 7 docentes seed de `DOCENTES_EXISTENTES` a la nueva forma.
- [x] 1.4 Actualizar `api/pedidosSeed.ts`: migrar los pedidos mock existentes de `materiaAsociada` a `asignaciones: [{ materia, horas }]`, agregar `horasExternas`/`horasInvestigacion` de ejemplo, quitar `haceHorasOtroDepto`, y agregar `tipoBaja` a los pedidos seed con novedad "Baja".

## 2. Validación

- [x] 2.1 En `pedidoValidacion.ts`: reemplazar la validación de `materiaAsociada` por validación de `asignaciones` (al menos 1 fila, cada una con materia y horas > 0) en Alta **y** Cambio.
- [x] 2.2 Agregar `tipoBaja` a `CampoPedido`/`ErroresValidacion`; validar que sea obligatorio cuando `novedad === "Baja"`, y que `tipoBajaDetalle` sea obligatorio cuando `tipoBaja === "Otro"`.
- [x] 2.3 Confirmar que no se agrega validación cruzada de horas contra dedicación (D2) — sin cambios de comportamiento ahí, solo test que lo documente explícitamente.
- [x] 2.4 Actualizar `pedidoValidacion.test.ts`: casos existentes que usaban `materiaAsociada` pasan a `asignaciones`; agregar casos para `tipoBaja` obligatorio y `tipoBajaDetalle` obligatorio en "Otro"; agregar caso que confirma ausencia de validación cruzada horas/dedicación.

## 3. Form de pedido — sección de materias y horas

- [x] 3.1 En `components/SeccionDesignacionSolicitada.tsx`: reemplazar el `Select` único de materia por una lista repetible de filas materia (`Select`) + horas (`Input`), con acción "Agregar materia" y quitar fila, compartida entre `esAlta` y `esCambio` (en Cambio se precarga con `materiasActuales` del docente, pero queda igual de editable); deshabilitar/ocultar "quitar" cuando queda 1 sola fila (mínimo 1 obligatorio). Para Baja, mostrar el mismo listado de `materiasActuales` (materia + horas) pero íntegramente de solo lectura (sin `Select`, sin `Input`, sin agregar/quitar). Para Sin novedad, mostrar la materia vigente única de solo lectura (sin lista).
- [x] 3.1b En la misma sección, quitar cualquier hint/copy de "solo podés solicitar un cargo superior" del `Select` de Cargo solicitado — cargo y dedicación solicitados son selección libre entre todo el catálogo en Alta y Cambio (sin restricción de jerarquía; ver D-6).
- [x] 3.2 Agregar los campos "Horas de investigación" y "Horas externas (otro depto.)" en la misma sección, visibles cuando `esAlta || esCambio`.
- [x] 3.3 En `components/PedidoForm.tsx`: actualizar `datosIniciales`, `actualizar`, `seleccionarDocente` y el render para usar `asignaciones`/`horasExternas`/`horasInvestigacion` en vez de `materiaAsociada`/`haceHorasOtroDepto`; quitar el `Toggle` "hace más horas en otro Departamento".
- [x] 3.4 En `components/SeccionDocentePedido.tsx`: actualizar las referencias a `materia`/`materiaAsociada` para leer la asignación vigente del docente seleccionado.

## 4. Form de pedido — tipificación de Baja

- [x] 4.1 En `PedidoForm.tsx` (o un nuevo `SeccionTipoBaja.tsx` si la sección crece), agregar el `Select` "Tipo de baja" (Renuncia / Jubilación / Otro) antes de "Motivo de la baja", visible solo cuando `esBaja`.
- [x] 4.2 Cuando `tipoBaja === "Otro"`, mostrar un campo de texto libre para `tipoBajaDetalle`.

## 5. Lectores read-only del modelo

- [x] 5.1 `components/DatosActualesPanel.tsx`: quitar la columna "Materia" del panel (queda representada en la sección de materias y horas, no duplicada) en Cambio y en Baja — el panel pasa a mostrar solo Antigüedad, Cargo actual y Dedicación (con su transición actual→solicitada en Cambio).
- [x] 5.2 `components/TablaMisPedidos.tsx`: mostrar materia(s) del pedido — si hay más de una asignación, listarlas o resumir ("Programación I +1").
- [x] 5.3 `components/TablaRevision.tsx` y `components/PedidoCard.tsx`: mismo tratamiento que 5.2 para la columna/campo de materia.
- [x] 5.4 `components/ResumenPedido.tsx`: actualizar el resumen para listar todas las asignaciones (materia + horas) en vez de una sola materia; agregar horas de investigación/externas si el resumen las muestra.
- [x] 5.5 `components/detalleAdapters.ts` y `components/tableroRevisionModelo.ts`: actualizar cualquier mapeo que lea `materiaAsociada` o `haceHorasOtroDepto`.

## 6. Tests

- [x] 6.1 Actualizar `PedidoForm.test.tsx` con casos de: agregar/quitar/cambiar materias en Alta y en Cambio, no poder quitar la última fila en ninguna de las dos, carga de horas de investigación/externas, cargo/dedicación solicitados sin restricción en Cambio, y el flujo de tipo de baja (incluyendo "Otro" con detalle obligatorio).
- [x] 6.2 Actualizar `PedidoCard.test.tsx`, `TablaRevision.test.tsx`, `detalleAdapters.test.ts`, `tableroRevisionModelo.test.ts`, `pedidosApi.test.ts` y `maquinaEstados.test.ts` donde referencien `materiaAsociada`/`haceHorasOtroDepto`.
- [x] 6.3 Correr `dotnet test backend/ArsDocendi.slnx` (no debería verse afectado, pero confirma que no hay breakage cross-module) y `pnpm --filter frontend lint` + la suite de tests del frontend completa.

## 7. Cierre

- [x] 7.1 Actualizar `docs/product/designs/rediseno-designaciones-exploracion.md`: marcar los temas A+B+D como implementados (no solo mockeados) y enlazar este change.
- [x] 7.2 Si `docs/product/designs/proyecto-docente-design-spec.md` describe el form vigente con el modelo anterior, actualizarlo para reflejar `asignaciones[]`/horas externas/tipo de baja.
- [x] 7.3 Registrar en `docs/quality/tech-debt.md` la fragmentación del catálogo de cargos (`CARGOS` vs `CARGOS_DOCENTES` vs jerarquía de 7) detectada durante el mockup, para el futuro change del tema C.
- [x] 7.4 Verificar manualmente en el navegador (`pnpm --filter frontend dev`): crear un pedido de Alta con 2 materias, un pedido de Cambio editando horas, y un pedido de Baja con cada tipo de baja (incluido "Otro").

## 8. Cambio — dedicación restringida y resumen de cambios (D-7/D-8/D-9)

- [x] 8.1 En `types.ts`: agregar `horasInvestigacionActuales: number` y `horasExternasActuales: number` a `DocenteExistente`.
- [x] 8.2 En `api/catalogos.ts`: poblar esos dos campos en los 7 `DOCENTES_EXISTENTES` (valores variados, con y sin diferencia respecto al form); agregar y exportar `indiceDedicacion(d: Dedicacion): number` (parsea el número de `"Categoría N"`).
- [x] 8.3 En `pedidoValidacion.ts`: en Cambio, rechazar si `indiceDedicacion(dedicacionSolicitada) >= indiceDedicacion(dedicacionActual)` (Categoría 0 = mayor jerarquía; debe ser estrictamente mejor).
- [x] 8.4 En `components/SeccionDesignacionSolicitada.tsx`: recibir `dedicacionActual: Dedicacion | null`; cuando no es null (= es Cambio), filtrar las `<option>` de "Dedicación solicitada" a `indiceDedicacion(d) < indiceDedicacion(dedicacionActual)`.
- [x] 8.5 Reescribir `components/DatosActualesPanel.tsx`: nuevos props `cargoSolicitado?`, `materiasActuales` (reemplaza el prop `materia` simple), `materiasSolicitadas?`, `horasInvestigacionActuales?/Solicitadas?`, `horasExternasActuales?/Solicitadas?`. Franja superior con transición de cargo (nueva) y dedicación (ya existía); sub-secciones "Materias" (comparación por nombre: sin-cambios/horas-cambiadas/agregada/quitada) y "Horas" (investigación/externas), ambas solo cuando llegan los props "solicitados" (Cambio).
- [x] 8.6 En `components/SeccionDocentePedido.tsx`: pasar los nuevos props a `DatosActualesPanel`; `SeccionMateriasHoras` de Baja pasa a leer `materiasActuales` (antes `asignaciones`).
- [x] 8.7 En `components/PedidoForm.tsx`: calcular `docenteSeleccionado` desde `opcionesDocente`; en `seleccionarDocente`, precargar también `horasInvestigacion`/`horasExternas` desde `docente.horasInvestigacionActuales`/`horasExternasActuales`; pasar `dedicacionActual` a `SeccionDesignacionSolicitada`; pasar cargo/dedicación/materias/horas "actuales" (desde `docenteSeleccionado`) y "solicitados" (desde `datos`, solo si `esCambio`) a `SeccionDocentePedido`.
- [x] 8.8 CSS (`pedidoForm.css`): sub-secciones verticales dentro del recuadro gris (`.adoc-pf-datos-sub`, fila con nombre + valor/transición, estilos para "agregada"/"quitada").
- [x] 8.9 Actualizar `docs/product/designs/screens.pen` (frame `tZANr`): el recuadro gris de Cambio pasa a mostrar la transición de cargo, dedicación, materias (con horas) y horas de investigación/externas — ejemplo estático coherente con los valores ya mockeados en la sección "Designación solicitada".
- [x] 8.10 Tests: actualizar `PedidoForm.test.tsx` (Select de dedicación filtrado a mejores; validación bloquea dedicación igual/peor) y `pedidoValidacion.test.ts` (caso "solo puede mejorar"). Correr suite completa + lint de nuevo.
