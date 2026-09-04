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
