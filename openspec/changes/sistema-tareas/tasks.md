## 1. Modelo de datos y store mock

- [x] 1.1 Definir en `features/tareas/types.ts` los tipos `Tarea` (incluye `porcentajeAvance: number` y `solucion?: string`), `EstadoTarea` (`"pendiente" | "en_curso" | "pausa" | "resuelta" | "cancelada"`), `Prioridad`, `ComentarioTarea`, `EventoHistorialTarea` y `ActorTarea` (nombre + rol, usado para `responsable` y `creadoPor`), según `design.md`.
- [x] 1.2 Crear `features/tareas/api/tareasSeed.ts` con datos de ejemplo que cubran variedad de estados, prioridades, fechas y porcentajes de avance (incluyendo casos que disparen cada color de semáforo, una tarea en Pausa con comentario y una tarea Resuelta con Solución completa).
- [x] 1.3 Crear `features/tareas/api/personasSeed.ts`: catálogo mock de candidatos a Responsable/Autor (nombre + rol), acotado a Tareas — sin importar desde `features/usuarios` ni `features/docentes` (aislamiento de features).
- [x] 1.4 Crear `features/tareas/api/tareasStore.ts`: singleton en memoria hidratado/persistido en `localStorage` (clave `adoc.mock.tareas.v1`), con `leerTodos`, `buscar`, `guardar`, `sembrarTareas`, `reiniciarStoreTareas` — mismo patrón que `designaciones/api/pedidosStore.ts` (copias con `structuredClone`, sin mutación por referencia).
- [x] 1.5 Crear `features/tareas/api/tareasApi.ts`: seam de API simulada (async) que consume el store — `listarTareas`, `obtenerTarea`, `crearTarea`, `editarTarea`, `cambiarEstadoTarea`, `editarAvance` (porcentaje + solución), `agregarComentario`.

## 2. Lógica de negocio pura

- [x] 2.1 Crear `features/tareas/api/maquinaEstadosTarea.ts` con funciones puras: `puedeCrearTarea(actor)`, `puedeEditarCampos(tarea, actor)`, `puedeCambiarEstado(tarea, actor, estadoDestino)`, `puedeEditarAvance(tarea, actor)`, `transicionValida(estadoActual, estadoDestino, actor, tarea)`.
- [x] 2.2 Agregar tests unitarios de `maquinaEstadosTarea.ts` cubriendo los escenarios de `specs/flujo-estado-tareas/spec.md` (el Responsable no puede cancelar ni editar campos, solo la autoridad creadora cancela/edita, Pausa exige comentario, Resuelta exige Solución, etc.).
- [x] 2.3 Crear `features/tareas/components/semaforoTarea.ts` con la función pura de cálculo de color según `design.md` (verde <50%, amarillo 50–80%, rojo ≥80%, solo para estados no terminales).
- [x] 2.4 Agregar tests unitarios de `semaforoTarea.ts` cubriendo los escenarios de `specs/tablero-tareas/spec.md`.

## 3. Hooks de datos (React Query)

- [x] 3.1 Crear `features/tareas/hooks/useActorTareas.ts`: deriva `{ nombre, rol }` directamente de `shared/auth/useCurrentUser` (sin importar nada de `features/designaciones` — features aisladas).
- [x] 3.2 Crear `features/tareas/hooks/useTareas.ts` con `useListadoTareas()`, `useTarea(id)`.
- [x] 3.3 Crear `features/tareas/hooks/useAccionesTarea.ts` con `useCrearTarea()`, `useEditarTarea()`, `useCambiarEstadoTarea()`, `useEditarAvance()`, `useAgregarComentario()`, invalidando las queries correspondientes tras cada mutación.

## 4. Pantalla de listado (`tablero-tareas`)

- [x] 4.1 Crear `features/tareas/components/EstadoTareaBadge.tsx`: badge de estado con color por estado y resaltado distintivo para Pausa (indicador visual en la columna Estado, según `specs/tablero-tareas/spec.md`).
- [x] 4.2 Crear `features/tareas/components/TablaTareas.tsx`: tabla con columnas Nro, Título, Autor, Responsable, Fecha Inicio, Fecha Fin, Prioridad, % Avance, Estado (`EstadoTareaBadge`); el fondo de toda la fila se colorea según `semaforoTarea` (amarillo/rojo; verde = sin resaltado), fila clickeable que navega a `/tareas/:id`.
- [x] 4.3 Extender `shared/ui/FiltrosLista.tsx` con tipos de campo nuevos (además de `"texto"`/`"select"`): `"fecha"` (input `type="date"`), `"numero"` (input `type="number"`) y `"buscable"` (combobox: input de texto que filtra `opciones` a medida que se escribe, se elige un resultado de la lista desplegada) en `CampoFiltroFijo` y `CampoFiltroOpcional`; `"multiSelect"` (nuevo `shared/ui/MultiSelectFiltro.tsx` — botón + desplegable de checkboxes, valor CSV) solo en `CampoFiltroOpcional`. Cambio aditivo — no debe alterar el comportamiento de los consumidores existentes (Designaciones, Docentes, Usuarios, Roles).
- [x] 4.11 Cambiar el filtro Estado de `IndexPage.tsx` de `"select"` (una opción) a `"multiSelect"` (varias a la vez); actualizar `filtrosTareas.ts` (`estado: string` CSV en vez de `EstadoTarea | "todos"`) y `aplicarFiltrosTareas` para chequear pertenencia al conjunto seleccionado.
- [x] 4.4 Crear `features/tareas/components/SelectorResponsable.tsx`: envoltorio sobre el tipo `"buscable"`, con las opciones provistas por `personasSeed.ts`; se usa tanto en el filtro Responsable como en el formulario "Nueva Tarea" (tarea 5.1).
- [x] 4.5 Crear `features/tareas/components/filtrosTareas.ts`: estado y lógica de filtrado por Nro de Tarea, Responsable (`SelectorResponsable`) y Título (fijos), Autor, Estado, Prioridad, % Avance, Fecha de Inicio y Fecha de Fin (opcionales) — Fecha Inicio/Fin con semántica "hasta esta fecha" (`≤`) y % Avance con coincidencia exacta, según `specs/tablero-tareas/spec.md`.
- [x] 4.6 Crear `features/tareas/api/filtrosGuardadosStore.ts`: store mock en `localStorage` (clave `adoc.mock.tareas.filtros.v1`) para configuraciones de filtros guardadas, indexadas por nombre del actor actual.
- [x] 4.7 Crear `features/tareas/hooks/useFiltrosGuardados.ts`: `useFiltrosGuardados(actor)` (lista las configuraciones del actor), `useGuardarFiltros()`, conectados a `filtrosGuardadosStore.ts`.
- [x] 4.8 Crear `features/tareas/components/ConfiguracionesFiltro.tsx`: selector para aplicar una configuración guardada + botón "Guardar filtros" (modal simple para nombrarla).
- [x] 4.9 Reescribir `features/tareas/pages/IndexPage.tsx`: reemplaza el placeholder actual. `PageHeader` con botón "Nueva Tarea" (condicionado a `puedeCrearTarea`), `FiltrosLista` (usa `filtrosTareas.ts`) + `ConfiguracionesFiltro` y `TablaTareas`, manejando Loading/Empty/Error/Success con `useListadoTareas`.
- [x] 4.10 Crear `features/tareas/components/ordenTareas.ts` (orden por defecto Fecha Inicio ascendente, `ordenarTareas`/`siguienteOrden`) con tests unitarios; headers de `TablaTareas.tsx` clickeables para ordenar (alterna asc/desc), estado de orden en `IndexPage.tsx` aplicado después del filtro.

## 5. Creación de tarea

- [x] 5.1 Crear `features/tareas/components/ModalNuevaTarea.tsx`: formulario con Título, Descripción, Fecha Inicio, Fecha Fin, Prioridad, Responsable (`SelectorResponsable`, tarea 4.4) — validaciones de campos obligatorios y Fecha Fin ≥ Fecha Inicio, según `specs/tareas/spec.md`.
- [x] 5.2 Conectar el modal a `useCrearTarea` desde `IndexPage.tsx`; la tarea creada nace en estado `pendiente`.

## 6. Pantalla de Detalle de Tarea

- [x] 6.1 Crear `features/tareas/components/ComentariosTarea.tsx`: hilo de comentarios (lista ordenada + input para agregar uno nuevo), conectado a `useAgregarComentario`.
- [x] 6.2 Crear `features/tareas/components/AccionesEstadoTarea.tsx`: controles para que el Responsable cambie entre Pendiente/En curso/Pausa/Resuelta (Pausa exige comentario obligatorio, Resuelta exige completar el campo Solución) y actualice el % de avance (0-100); y para que la autoridad creadora edite campos o cancele — visibilidad derivada de `maquinaEstadosTarea.ts`.
- [x] 6.3 Crear `features/tareas/pages/DetalleTareaPage.tsx`: layout de dos columnas (columna principal: descripción + Solución (si existe) + `ComentariosTarea` + `AuditLog` del historial; rail lateral: datos de la tarea incluido % Avance, Responsable, Autor + `AccionesEstadoTarea`) — mismo patrón que `designaciones/pages/DetallePedidoPage.tsx`. Maneja tarea inexistente con mensaje de error y enlace de vuelta al listado.
- [x] 6.4 Registrar en el historial (`EventoHistorialTarea`) cada creación, cambio de estado, actualización de % de avance, edición de campos y cancelación, con actor, rol, estado resultante y fecha.

## 7. Rutas

- [x] 7.1 Actualizar `features/tareas/routes.tsx`: ruta índice → `IndexPage`, ruta `:id` → `DetalleTareaPage`.
- [x] 7.2 Verificar breadcrumbs en ambas pantallas (`Inicio › Tareas` y `Inicio › Tareas › Detalle de la tarea`).

## 8. Documentación (mismo PR, invariante #6)

- [x] 8.1 Completar `docs/architecture/domains/tareas.md`: sección "Entidades principales" con `Tarea` y sus campos, sección "Decisiones registradas" con el ciclo de estados y el semáforo por porcentaje transcurrido.
- [x] 8.2 Actualizar `docs/business-rules/tareas.md` si alguna regla de este change proviene de una decisión institucional citable; si no, dejar constancia de que las reglas de estado/permisos son decisiones de producto (no normativa) y no requieren `BR-tareas-NNN`.

## 9. Validación manual

- [x] 9.1 Levantar el dev server y recorrer el flujo completo: crear tarea como Secretaría/Decanato/Administración, verificar que Jefe de Cátedra/Coordinador/Docente no ven el botón "Nueva Tarea", cambiar estados como Responsable (incluida Pausa con comentario obligatorio y Resuelta con Solución obligatoria), actualizar el % de avance como Responsable, intentar cancelar o editar campos como Responsable (debe rechazarse), cancelar y editar campos como la autoridad creadora, verificar semáforo en los tres colores, el indicador de Pausa y las columnas Autor/Responsable/% Avance en el listado.
- [x] 9.2 Verificar los filtros: Nro de Tarea, Responsable y Título visibles de entrada; Autor, Estado, Prioridad, % Avance, Fecha Inicio y Fecha Fin ocultos hasta agregarlos; búsqueda y selección en el combobox de Responsable (filtro y formulario de alta); comportamiento "hasta esta fecha" en los filtros de fecha y coincidencia exacta en % Avance; y que guardar/aplicar una configuración de filtros funcione y sea distinta por usuario (probar con al menos dos roles distintos vía el selector de usuario mock).

## 10. Rediseño de la tabla — mismo modelo que la Tabla de revisión de Designaciones

La sección 4 (arriba) documenta el diseño original de la tabla (`FiltrosLista` con filtros fijos/opcionales + configuraciones guardadas). Después de comparar con `designaciones/components/TablaRevision.tsx` (rediseñada sobre el design system en un change posterior de otro equipo), se reemplazó ese modelo por el mismo que usa Revisión, para que la app no tenga dos patrones de tabla distintos. Las tareas de esta sección reemplazan el comportamiento de 4.2–4.9, no lo suman.

- [x] 10.1 Reescribir `features/tareas/components/TablaTareas.tsx` sobre `Table`/`Table.Root`/`Table.Head`/`Table.Row`/`Table.HeaderCell`/`Table.Body`/`Table.Cell` de `@ars-docendi/ui` (mismo compound component que `TablaRevision.tsx`), con una columna Acciones (botón "Ver" por fila en vez de fila clickeable). El semáforo de vencimiento sigue coloreando el fondo de la fila (`tablaTareas.css`, reducido a eso: la librería trae grilla/hover/empty-state/mono).
- [x] 10.2 Reescribir `features/tareas/components/filtrosTareas.ts`: de `FiltrosTareasState` (Record de strings para `FiltrosLista`) a `FiltrosColumnasTareas` (texto para Nro/Título/Fecha Inicio/Fecha Fin, arrays para Autor/Responsable/Prioridad/Estado, string numérico para % Avance) + `opcionesColumnasTareas(tareas)` que deriva las opciones de Autor/Responsable de las tareas visibles (como `opcionesColumnasTablero`). Semántica de fecha pasa de "hasta esta fecha" (`≤` sobre el ISO) a texto libre sobre la fecha ya formateada dd/mm/aaaa (coincide con cómo filtra Revisión sus columnas de fecha).
- [x] 10.3 Reescribir `features/tareas/components/ordenTareas.ts`: de un ciclo de 2 estados (asc/desc, siempre una columna activa) a 3 estados (asc → desc → `null`), donde `null` es el orden por defecto (Fecha Inicio ascendente) y ningún header queda marcado como activo — mismo ciclo que `siguienteOrden` de `tableroRevisionModelo.ts`.
- [x] 10.4 Montar en cada `Table.HeaderCell` de `TablaTareas.tsx` un `FiltroEncabezado` (`shared/ui/FiltroEncabezado.tsx`, reutilizado tal cual, sin cambios) con `Input` (columnas de texto) o una lista de checkboxes local (columnas de selección múltiple) — mismo patrón que `EncabezadoRevision`/`Opciones` de `TablaRevision.tsx`.
- [x] 10.5 Simplificar `features/tareas/pages/IndexPage.tsx`: sacar el estado de `filtros`/`orden` (ahora vive dentro de `TablaTareas`, autocontenida) y el bloque `FiltrosLista` + `ConfiguracionesFiltro`; la página pasa a solo `tareas` + `onSeleccionar`.
- [x] 10.6 Eliminar `features/tareas/components/ConfiguracionesFiltro.tsx`, `features/tareas/api/filtrosGuardadosStore.ts` y `features/tareas/hooks/useFiltrosGuardados.ts` — la función de guardar configuraciones de filtros sale de alcance (Revisión no tiene equivalente; ver `proposal.md`). `shared/ui/FiltrosLista.tsx`/`ComboboxBuscable.tsx`/`MultiSelectFiltro.tsx` (con los tipos agregados en la sección 4) quedan sin tocar en `shared/`, disponibles para otras features, pero Tareas ya no los usa para su tabla — `SelectorResponsable.tsx` (sobre `ComboboxBuscable`) se conserva porque lo sigue usando `ModalNuevaTarea.tsx`.
- [x] 10.7 Convertir a tokens del design system (`--space-*`, `--text-*-size`, `--weight-*`) el CSS de tamaño/tipografía que seguía en píxeles sueltos en `estadoTarea.css` (los colores ya usaban tokens `--color-status-*`).
- [x] 10.8 Actualizar tests: reescribir `ordenTareas.test.ts` para el ciclo de 3 estados; crear `filtrosTareas.test.ts` para `aplicarFiltrosColumnas`/`opcionesColumnasTareas`.
- [x] 10.9 Verificar en el navegador (con el backend real levantado — login vía identidad de desarrollo): filtros por header en cada columna, checkboxes de Autor/Responsable/Prioridad/Estado, orden de 3 estados por click, semáforo en la fila, botón "Ver" navega al detalle.

## 11. Proyectos, Tipo, relaciones y jerarquía de tareas (frontend — el backend queda para después)

Amplía el modelo de Tareas y agrega la entidad Proyecto, según `specs/tareas/spec.md`, `specs/tablero-tareas/spec.md` y el nuevo `specs/proyectos/spec.md`. Sigue siendo frontend-first (mock store), mismo patrón que las secciones anteriores.

### 11.1 Modelo de datos y store mock de Proyectos

- [x] 11.1.1 Agregar en `features/tareas/types.ts`: `TipoTarea`, `EstadoProyecto`, `Proyecto`, y en `Tarea` los campos `tipo: TipoTarea`, `proyectoId?: string`, `tareaPadreId?: string`, `tareasRelacionadasIds: string[]`, según `design.md`.
- [x] 11.1.2 Crear `features/tareas/api/proyectosSeed.ts` con Proyectos de ejemplo (Abierto/Finalizado/Cancelado, con y sin tareas asociadas).
- [x] 11.1.3 Crear `features/tareas/api/proyectosStore.ts`: mismo patrón que `tareasStore.ts` (`localStorage`, clave `adoc.mock.tareas.proyectos.v1`, `structuredClone`).
- [x] 11.1.4 Crear `features/tareas/api/proyectosApi.ts`: seam async — `listarProyectos`, `obtenerProyecto`, `crearProyecto`, `cambiarEstadoProyecto`.
- [x] 11.1.5 Actualizar `tareasSeed.ts`: agregar `tipo` a todas las tareas de ejemplo, `proyectoId` en algunas, `tareasRelacionadasIds` en al menos un par de tareas, y al menos una jerarquía padre/hijas de ejemplo (con un nivel de anidamiento extra, para cubrir el caso multinivel).

### 11.2 Lógica de negocio pura

- [x] 11.2.1 Agregar `puedeCrearProyecto(actor)` y `puedeCambiarEstadoProyecto(actor)` (rol ∈ {Secretaría Académica, Decanato}) — `features/tareas/api/maquinaEstadosProyecto.ts`.
- [x] 11.2.2 Agregar `puedeAsignarComoResponsableProyecto(actor, candidato)`: reutiliza `puedeAsignarComoResponsable` (tarea 11.8.1) y además acota `candidato.rol` a {Secretaría Académica, Decanato} — para filtrar el catálogo de `personasSeed.ts` en el selector de Responsable de Proyecto.
- [x] 11.2.3 Confirmar que crear una tarea hija reutiliza `puedeCrearTarea(actor)` sin una función nueva (mismos roles).
- [x] 11.2.4 Tests unitarios de `puedeCrearProyecto`, `puedeCambiarEstadoProyecto` y `puedeAsignarComoResponsableProyecto`.

### 11.3 Proyectos: hooks, alta y Detalle

- [x] 11.3.1 Crear `features/tareas/hooks/useProyectos.ts` (`useListadoProyectos`, `useProyecto(id)`) y `features/tareas/hooks/useAccionesProyecto.ts` (`useCrearProyecto`, `useCambiarEstadoProyecto`).
- [x] 11.3.2 Crear `features/tareas/components/ModalNuevoProyecto.tsx`: Nombre, Descripción, Fecha Inicio, Fecha Fin, Responsable (selector filtrado vía `puedeAsignarComoResponsableProyecto`, tarea 11.2.2) — mismas validaciones de obligatorios y Fecha Fin ≥ Fecha Inicio que `ModalNuevaTarea.tsx`.
- [x] 11.3.3 Crear `features/tareas/pages/DetalleProyectoPage.tsx` (`/tareas/proyectos/:id`): datos del Proyecto, acción de cambiar Estado (Finalizado/Cancelado, condicionada a `puedeCambiarEstadoProyecto`) y la tabla de tareas de ese Proyecto (`TablaTareas` filtrada por `proyectoId`). Maneja Proyecto inexistente con mensaje de error y enlace de vuelta a Tareas.
- [x] 11.3.4 Crear `features/tareas/pages/ListadoProyectosPage.tsx` (`/tareas/proyectos`): tabla simple con **todos** los Proyectos sin importar su Estado (Nombre, Responsable, Fecha Fin, Estado), cada fila navega a `DetalleProyectoPage.tsx` — es la vía de acceso manual a los Finalizados/Cancelados, que no tienen cuadro en la pantalla inicial.
- [x] 11.3.5 Registrar ambas rutas en `features/tareas/routes.tsx` y sus breadcrumbs (`Inicio › Tareas › Proyectos` y `Inicio › Tareas › Proyectos › <Nombre del Proyecto>`).

### 11.4 Tarea: Tipo, Proyecto, relaciones y jerarquía

- [x] 11.4.1 Agregar el campo Tipo (select obligatorio) a `ModalNuevaTarea.tsx`, y un selector de Proyecto opcional — oculto y no editable cuando la tarea que se crea/edita es una hija (hereda el del padre).
- [x] 11.4.2 Mostrar el Tipo en `DetalleTareaPage.tsx` (sección "Datos"). El Tipo NO se agrega como columna ni como filtro de `TablaTareas.tsx` — el listado mantiene su formato actual, según lo confirmado en `design.md`.
- [x] 11.4.3 Agregar el control de "Tareas relacionadas" en `DetalleTareaPage.tsx`: buscador para agregar una relación a una tarea existente, lista de accesos rápidos a las ya relacionadas, y acción para quitar una relación — conectado a `agregarRelacion`/`quitarRelacion` en `tareasApi.ts`.
- [x] 11.4.4 Agregar el control de "Tareas hijas" en `DetalleTareaPage.tsx`: listado de hijas directas (con su propio Estado/% Avance/Responsable) + acción "Nueva tarea hija" que abre `ModalNuevaTarea.tsx` con `tareaPadreId` predefinido y el Proyecto heredado no editable; cada hija de la lista navega a su propio Detalle (donde puede a su vez tener sus propias hijas).
- [x] 11.4.5 Mostrar en el Detalle de una tarea hija una referencia visible a su tarea padre (dato en el rail lateral, con link).

### 11.5 Pantalla inicial reorganizada por Proyecto

- [x] 11.5.1 Crear `features/tareas/components/agrupacionProyectos.ts`: función pura que agrupa las tareas por `proyectoId` en cuadros — "Generales" primero, luego los Proyectos **en estado Abierto** con al menos una tarea, ordenados por `fechaFin` descendente (los Proyectos Finalizados/Cancelados quedan afuera aunque tengan tareas) — con tests unitarios.
- [x] 11.5.2 Crear `features/tareas/components/CuadroProyecto.tsx`: título (nombre del Proyecto, o "Generales") + `TablaTareas` acotada a ese subconjunto, sin cambios en sus columnas; el título es un link a `/tareas/proyectos/:id` salvo en el cuadro "Generales".
- [x] 11.5.3 Reescribir `IndexPage.tsx`: reemplazar el único `TablaTareas` por la lista de `CuadroProyecto` que produce `agrupacionProyectos.ts`, agregar el botón "Nuevo Proyecto" (condicionado a `puedeCrearProyecto`) junto a "Nueva Tarea" en el `PageHeader`, y un link "Ver todos los proyectos" hacia `ListadoProyectosPage.tsx`.

### 11.6 Documentación y specs

- [x] 11.6.1 Actualizar `docs/architecture/domains/tareas.md`: agregar `Proyecto` a "Entidades principales", documentar Tipo, relación simple y jerarquía padre/hijas en "Decisiones registradas".
- [x] 11.6.2 Revisar `docs/business-rules/tareas.md` por si la restricción de rol de Proyectos o la herencia obligatoria de Proyecto en hijas ameritan un `BR-tareas-NNN`; si no, dejar constancia de que son decisiones de producto.

### 11.7 Validación manual

- [x] 11.7.1 Crear un Proyecto como Decanato/Secretaría; verificar que Administrativos ve "Nueva Tarea" pero no "Nuevo Proyecto"; cambiar el Estado del Proyecto a Finalizado y verificar que su cuadro desaparece de la pantalla inicial pero sigue accesible desde "Ver todos los proyectos" → su Detalle. _(Verificado con Decanato en vivo — con la identidad de desarrollo "Administrativo" no se pudo reproducir la comparación de botones por el bug de nombres de rol de la nota al pie; sí queda cubierto por `maquinaEstadosProyecto.test.ts`.)_
- [x] 11.7.2 Crear una tarea sin Proyecto y otra asociada a un Proyecto Abierto; verificar que cada una aparece en el cuadro correcto de la pantalla inicial, y que el orden de los cuadros (Generales primero, resto por Fecha de Fin más reciente) es el esperado.
- [x] 11.7.3 Crear una tarea hija desde el Detalle de una tarea padre; verificar que hereda el Proyecto del padre sin poder editarlo aparte; crear una hija de esa hija (multinivel); verificar que cambiar el % de avance o el Estado de una hija no modifica los del padre.
- [x] 11.7.4 Relacionar dos tareas existentes desde el Detalle y verificar el acceso rápido bidireccional; quitar la relación y verificar que desaparece de ambos lados.

### 11.8 Jerarquía de asignación de Responsable

Restringe a quién se le puede asignar una tarea (o un Proyecto) según el rango de autoridad del actor, por `specs/tareas/spec.md` (Requirement "Jerarquía de asignación de Responsable") y `specs/proyectos/spec.md`. Toca comportamiento ya implementado en la sección 5 (`SelectorResponsable.tsx`, `ModalNuevaTarea.tsx`), que hoy no filtra candidatos por rol.

- [x] 11.8.1 Agregar en `features/tareas/api/maquinaEstadosTarea.ts` el rank map `ORDEN_JERARQUIA` (Decanato > Secretaría Académica > Administrativo > Coordinador de Carrera > Jefe de Cátedra > Docente) y `puedeAsignarComoResponsable(actor, candidato)` (`true` si el nivel del candidato es igual o inferior al del actor), según `design.md`.
- [x] 11.8.2 Filtrar las opciones de `SelectorResponsable.tsx` en `ModalNuevaTarea.tsx` (alta y edición) con `puedeAsignarComoResponsable(actor, candidato)` — reemplaza el catálogo completo sin filtrar que usa hoy.
- [x] 11.8.3 Aplicar el mismo filtro al crear una tarea hija (tarea 11.4.4) y al selector de Responsable de `ModalNuevoProyecto.tsx` (tarea 11.3.2, vía `puedeAsignarComoResponsableProyecto`, tarea 11.2.2).
- [x] 11.8.4 Tests unitarios de `puedeAsignarComoResponsable` cubriendo los escenarios del spec: Decanato asigna a cualquiera, Secretaría Académica no puede asignar a Decanato, Administrativo no puede asignar a Decanato ni Secretaría Académica, pares del mismo nivel permitidos entre Decanato/Secretaría Académica/Administrativo.
- [x] 11.8.5 Verificar en el navegador: el buscador de Responsable en "Nueva Tarea" oculta los candidatos de rango superior al del actor logueado (probado con Decanato, Secretaría Académica y Administrativo, tras el fix de la tarea 11.9.1); lo mismo en "Nuevo Proyecto".

### 11.9 Nombres de rol alineados con las identidades reales de desarrollo

Descubierto en la validación manual de la sección 11.7/11.8: `personasSeed.ts`/`tareasSeed.ts`/`proyectosSeed.ts`/`maquinaEstadosTarea.ts`/`maquinaEstadosProyecto.ts` usaban formas cortas de rol ("Secretaría", "Administración", "Coordinador") — remanente de la era 100% mock, previa a la integración del backend real (PR #23) — que no coincidían con `actor.rol` en la app real (`useCurrentUser` → `rol.nombre` de `GET /api/desarrollo/identidades`, sembrado por `Modules.Identity`: "Secretaría Académica", "Administrativo", "Coordinador de Carrera"). Efecto confirmado en vivo antes del fix: un usuario logueado como "Administrativo" no veía "Nueva Tarea" ni "Nuevo Proyecto"; "Decanato" coincidía por ser de una sola palabra, pero el resto de los flujos (ordenar por jerarquía, filtrar el listado, etc.) sobre una sesión con localStorage previo a este change también rompían por el cambio de shape de `Tarea` (ver 11.9.2).

- [x] 11.9.1 Reemplazar los literales de rol cortos por los nombres exactos que devuelve el backend, en todo `features/tareas` (código, seeds y tests): "Secretaría" → "Secretaría Académica", "Administración" → "Administrativo", "Coordinador" → "Coordinador de Carrera". **No** se tocó `features/designaciones`, que tiene el mismo problema (`TableroRevisionPage.tsx`, `pedidosApi.ts`, etc.) — queda fuera de alcance de `sistema-tareas` por ser código ya mergeado de otro equipo; se deja registrado para un change aparte.
- [x] 11.9.2 Subir la versión de la clave de `localStorage` del store de tareas (`adoc.mock.tareas.v1` → `v2` en `tareasStore.ts`) para que los navegadores con datos previos al agregado de `tipo`/`proyectoId`/`tareaPadreId`/`tareasRelacionadasIds` (shape antiguo de `Tarea`) resiembren en vez de crashear la pantalla al leer una tarea sin esos campos.

### 11.10 Ajustes de feedback sobre la pantalla inicial y el Detalle de tarea

- [x] 11.10.1 Renombrar el cuadro fijo "Sin proyecto" a "Generales" en `CuadroProyecto.tsx`, `agrupacionProyectos.ts` (comentarios/nombres internos) y las specs/`design.md`/`proposal.md` de `sistema-tareas` — mismo comportamiento (siempre primero, incluso vacío), solo cambia la etiqueta. No afecta al "Sin proyecto" que muestran `ModalNuevaTarea.tsx`/`DetalleTareaPage.tsx` para una tarea individual sin Proyecto asociado, que es un dato distinto.
- [x] 11.10.2 Mostrar Fecha de Fin y % de avance en `TareasRelacionadas.tsx` y `TareasHijas.tsx` (esta última ya mostraba % de avance; se agregó la Fecha de Fin en ambas).
