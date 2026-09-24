## Context

`frontend/src/features/tareas` existe como placeholder (`IndexPage.tsx` con "Módulo en construcción", `types.ts` vacío). No hay backend (`Modules.Tareas`) más allá del ping de smoke test. El resto de los módulos frontend ya resolvieron este mismo problema con un patrón consistente que este change replica:

- **Designaciones** (`features/designaciones`): store mock singleton en memoria + localStorage (`pedidosStore.ts`), un seam de API que simula async (`pedidosApi.ts`), hooks de React Query (`usePedidos.ts`, `useAccionesPedido.ts`), y derivación de rol/ámbito vía `useActorContexto` → `useCurrentUser`.
- **Docentes / Roles / Usuarios**: mismo patrón `mockStore.ts` + `IndexPage.tsx` con tabla + modales.

Este change sigue el mismo patrón para Tareas: no hay necesidad de diseño de API HTTP real todavía, así que el foco del diseño es el modelo de datos, la máquina de estados y la reutilización de componentes de `shared/ui`.

## Goals / Non-Goals

**Goals:**

- Modelar `Tarea` con un ciclo de estados claro y sus transiciones permitidas.
- Reutilizar los primitivos ya validados en Designaciones (`PageHeader` con `actions`, `AuditLog`, el `Table` del design system + `FiltroEncabezado` para filtros por columna, patrón de store mock) en vez de reinventar UI.
- Dejar la lógica de visibilidad/permisos aislada en funciones puras (espejo de `maquinaEstados.ts` de Designaciones) para que sea fácil de testear y de reemplazar el día que exista backend real.

**Non-Goals:**

- Persistencia real / API HTTP de `Modules.Tareas` (el store vive en `localStorage`, por navegador, no compartido entre usuarios — igual que Docentes/Roles/Usuarios hoy). Incluye a Proyectos: mismo mock store, mismo trade-off.
- Sistema de permisos configurable (eso es `roles-membresia`, change de otro equipo — este change solo lee `Role` vía `useCurrentUser`, no lo modifica).
- Asignación múltiple, adjuntos, notificaciones, parametrización del umbral del semáforo.
- Cálculo automático del Estado o el `porcentajeAvance` de una tarea padre en base a sus tareas hijas — se editan a mano, igual que cualquier tarea (decisión explícita, ver Decisions).
- Límite mecánico a la profundidad de la jerarquía padre/hijas (se permite anidar más de un nivel; un límite práctico de UI, si hace falta, es un ajuste de estilos posterior, no de alcance).

## Decisions

**Modelo de datos** (`features/tareas/types.ts`), espejo de `PedidoDesignacion`:

```ts
export type EstadoTarea = "pendiente" | "en_curso" | "pausa" | "resuelta" | "cancelada";
export type Prioridad = "alta" | "media" | "baja";

export interface ComentarioTarea {
  id: string;
  autor: string; // nombre del actor
  rolAutor: Rol;
  texto: string;
  fecha: string; // ISO
}

export interface EventoHistorialTarea {
  id: string;
  accion: "crear" | "cambiar_estado" | "editar" | "cancelar";
  porRol: Rol;
  porNombre: string;
  estado: EstadoTarea; // estado al momento del evento
  detalle?: string;
  fecha: string;
}

export interface Tarea {
  id: string;
  numero: number; // correlativo legible, asignado por el store al crear
  titulo: string;
  descripcion: string;
  fechaInicio: string; // ISO (solo fecha)
  fechaFin: string; // ISO (solo fecha) — vencimiento
  prioridad: Prioridad;
  tipo: TipoTarea;
  estado: EstadoTarea;
  porcentajeAvance: number; // 0-100, lo completa el Responsable
  solucion?: string; // detalle de resolución; obligatorio al pasar a "resuelta"
  responsable: { nombre: string; rol: Rol };
  creadoPor: { nombre: string; rol: Rol };
  comentarios: ComentarioTarea[];
  historial: EventoHistorialTarea[];
  proyectoId?: string; // opcional en tareas de primer nivel; heredado obligatorio si tareaPadreId existe
  tareaPadreId?: string; // presente solo si es una tarea hija
  tareasRelacionadasIds: string[]; // vínculo simple bidireccional, sin jerarquía
}

export type TipoTarea =
  "extension" | "administrativa" | "posgrado" | "investigacion" | "academica" | "decanato";

export type EstadoProyecto = "abierto" | "finalizado" | "cancelado";

export interface Proyecto {
  id: string;
  numero: number;
  nombre: string;
  descripcion: string;
  fechaInicio: string; // ISO
  fechaFin: string; // ISO
  estado: EstadoProyecto;
  responsable: { nombre: string; rol: Rol }; // rol restringido a Secretaría o Decanato
}
```

**Máquina de estados** (`features/tareas/api/maquinaEstadosTarea.ts`, funciones puras testeables — espejo de `maquinaEstados.ts` de Designaciones):

- El **Responsable** mueve la tarea libremente entre `pendiente` / `en_curso` / `pausa` / `resuelta` (no es una secuencia estricta: puede, por ejemplo, volver de `resuelta` a `en_curso` si la autoridad la reabre, o pasar de `pendiente` directo a `pausa` si tiene una consulta antes de arrancar).
- `→ pausa`: requiere comentario (se agrega automáticamente como primer `ComentarioTarea` del hilo, para que la autoridad vea el motivo sin abrir el historial).
- `→ resuelta`: requiere que el campo `solucion` no esté vacío — el Responsable debe ingresar el detalle de cómo se resolvió antes de que la transición se confirme.
- `→ cancelada`: reservada a la **autoridad creadora** — el Responsable no puede cancelar, solo la autoridad. Disponible desde cualquier estado no terminal.
- `cancelada` y `resuelta` son terminales para el Responsable; solo la autoridad creadora puede reabrir una tarea `resuelta` (llevarla de vuelta a `en_curso`) o revertir una `cancelada`.

**Permisos** (funciones puras, mismo archivo):

- `puedeCrearTarea(actor)`: `true` si `actor.rol` ∈ {Secretaría Académica, Decanato, Administrativo} — controla la visibilidad del botón "Nueva Tarea".
- `puedeEditarCampos(tarea, actor)`: `true` si `actor.nombre === tarea.creadoPor.nombre` — cubre Título, Descripción, fechas, Prioridad y Responsable.
- `puedeCambiarEstado(tarea, actor, estadoDestino)`: si `estadoDestino === "cancelada"` → solo la autoridad creadora; si no → `true` para el Responsable (`actor.nombre === tarea.responsable.nombre`) o la autoridad creadora.
- `puedeEditarAvance(tarea, actor)`: `true` si `actor.nombre === tarea.responsable.nombre` o es la autoridad creadora — cubre `porcentajeAvance` y `solucion`, que **no** son parte de `puedeEditarCampos` (el Responsable los completa aunque no pueda editar el resto de la tarea).

**Semáforo de vencimiento** (`features/tareas/components/semaforoTarea.ts`, función pura): el umbral es relativo a la duración de la tarea, no un número fijo de días. Con `totalDias = fechaFin - fechaInicio` y `transcurridos = hoy - fechaInicio`, el porcentaje transcurrido `= transcurridos / totalDias`:

- `< 50%` transcurrido → `"green"` — caso normal, **sin resaltado** (no es que la fila se pinte de verde; simplemente no se destaca).
- `≥ 50%` y `< 80%` transcurrido → `"yellow"` → el **fondo de toda la fila** (`TablaTareas.tsx`, no solo la celda Fecha Fin) se pinta de amarillo.
- `≥ 80%` transcurrido (resta ≤20% del plazo) — incluye vencida (`>100%`) — → `"red"` → fondo de fila rojo.

Se aplica solo si el estado es no terminal (`pendiente`/`en_curso`/`pausa`); una tarea `resuelta` o `cancelada` no muestra semáforo (fondo normal). En el Detalle de la tarea (con más espacio que una celda de tabla) se usa en cambio el componente `TrafficLight` de `@ars-docendi/ui`, que sí muestra label + fecha.

**Columnas del listado**: además de Nro, Título, Fecha Inicio, Fecha Fin, Prioridad y Estado, la tabla agrega **Autor** y **Responsable** (nombre de cada actor), **% Avance** (el valor de `porcentajeAvance`) y **Acciones** (botón "Ver" por fila). Con 10 columnas la tabla se acerca al límite razonable de una fila legible; si en la práctica queda muy angosta, es un ajuste de estilos, no de alcance — la tabla scrollea horizontalmente por sí sola (`.adoc-tabla-scroll`), sin arrastrar la página entera.

**Tabla y modelo de filtros/orden/colores — el mismo que la Tabla de revisión de Designaciones** (`designaciones/components/TablaRevision.tsx` + `tableroRevisionModelo.ts` + `filtrosTablero.ts`), no un modelo propio de Tareas. Se reemplazó el diseño original de este documento (`FiltrosLista` con filtros fijos/opcionales en una fila aparte + configuraciones guardadas) después de comparar ambas pantallas — mantener dos modelos de tabla distintos en la misma app era la inconsistencia real a evitar, no una preferencia estética:

- **Tabla**: `Table`/`Table.Root`/`Table.Head`/`Table.Row`/`Table.HeaderCell`/`Table.Body`/`Table.Cell` de `@ars-docendi/ui` (el mismo compound component que ya usan Usuarios/Docentes/Períodos/Revisión), no un grid CSS a mano. `TablaTareas.tsx` queda con muy poco CSS propio (`tablaTareas.css`): la librería trae la grilla, el hover, el estado vacío (`.empty`) y la fuente monoespaciada (`.adoc-mono`) — lo único que Tareas necesita agregar es el fondo de fila del semáforo, que no tiene equivalente en la librería.
- **Filtros — uno por columna, en el propio header** (`features/tareas/components/filtrosTareas.ts` + `shared/ui/FiltroEncabezado.tsx`, componente ya existente reutilizado tal cual, no reinventado): cada `Table.HeaderCell` lleva un ícono de filtro que abre un popover (`FiltroEncabezado`) con el control de esa columna. No hay una fila de filtros generales separada de la tabla — a diferencia del diseño anterior, ningún filtro queda "siempre visible" por fuera del header; todos arrancan cerrados y se indican con un puntito cuando están activos. Nro de Tarea/Título/Fecha Inicio/Fecha Fin filtran por texto libre (`Input`, comparación sin distinguir mayúsculas/acentos; las fechas comparan contra el texto ya formateado dd/mm/aaaa, no contra el ISO); Autor/Responsable/Prioridad/Estado filtran con checkboxes (`Opciones`, un componente local que replica el de Revisión) que permiten elegir varios valores a la vez; % Avance filtra con un `Input type="number"` por coincidencia exacta. Las opciones de Autor y Responsable se calculan de las tareas visibles (`opcionesColumnasTareas`, igual que `opcionesColumnasTablero` de Revisión) — no de un catálogo estático — así el filtro nunca ofrece un nombre que no aparece en ninguna fila.
- **Orden — ciclo de 3 estados por header, no 2** (`features/tareas/components/ordenTareas.ts`): `Table.HeaderCell` trae el `th` clickeable, el `aria-sort` y la flechita — acá solo vive el criterio. Un click ordena ascendente, un segundo click sobre la misma columna pasa a descendente, un tercer click vuelve a `orden = null` (sin columna activa) — y `null` no es "sin orden", es el **orden por defecto: Fecha Inicio ascendente**, la única exigencia explícita del alcance original. Comparadores por tipo: texto (Título/Autor/Responsable) alfabético sin distinguir mayúsculas, fechas ISO comparadas como string (ya ordenan cronológicamente), Prioridad/Estado por un rango explícito (no alfabético — "Alta" no es mayor que "Baja" por orden de letras), Nro/% Avance numérico.
- **Colores**: el semáforo de vencimiento (ver más arriba) sigue coloreando el fondo de la fila — eso no cambió. `EstadoTareaBadge` sigue siendo un componente propio (no `StatusBadge` de la librería, que no cubre `en_curso`/`pausa`/`resuelta`), pero su CSS pasó a usar los tokens de tipografía/espaciado (`--text-micro-size`, `--weight-medium`, `--space-*`) que el resto de la app ya adoptó, en vez de valores en píxeles sueltos.
- **Acciones por fila**: un botón "Ver" en una columna `Acciones` (como en Revisión), no toda la fila clickeable — es más explícito y da un target de foco/teclado nativo sin manejar `onKeyDown` a mano.

**Autocontenida en `TablaTareas.tsx`**: a diferencia del diseño original (estado de filtros/orden en `IndexPage.tsx`), ahora `orden` y los filtros de columna viven en `useState` dentro de `TablaTareas`, igual que el comportamiento por defecto de `TablaRevision.tsx` — el componente recibe `tareas` + `onSeleccionar` y no necesita que la página le administre nada. Esto **saca de alcance** la función de "guardar configuraciones de filtros" que este documento describía antes: no hay estado en la página para guardar, y Revisión (el modelo a igualar) tampoco tiene un equivalente. Si se vuelve a pedir, se resuelve levantando el estado de filtros a `IndexPage.tsx` otra vez.

**Solución y % de avance, exclusivos del Responsable**: `porcentajeAvance` (0-100) y `solucion` los completa el Responsable — independientes de `puedeEditarCampos` (que sigue siendo exclusivo de la autoridad creadora). `porcentajeAvance` es un campo informativo de seguimiento manual: no dispara transiciones de estado automáticas (llegar a 100% no fuerza `resuelta`, ni marcar `resuelta` fuerza 100%) — quedan desacoplados a propósito para no adivinar una regla de negocio que no se pidió.

**Indicador de Pausa en el listado**: la fila de una tarea en estado `pausa` se resalta (badge/ícono distintivo en la columna Estado, visible para cualquiera que vea el listado, pero con foco en que la autoridad creadora la note) — mismo mecanismo visual que ya usa `EstadoPedidoBadge`/`EstadoPedidoPill` en Designaciones, adaptado a `EstadoTarea`.

**Pantallas y rutas** (`features/tareas/routes.tsx`):

- `/tareas` (índice, reemplaza el placeholder actual) → `pages/IndexPage.tsx`: listado único para todos los roles, con `PageHeader` (`actions` = botón "Nueva Tarea" condicionado a `puedeCrearTarea`) y `TablaTareas` (autocontenida: filtros y orden viven adentro, ver más arriba). `IndexPage.tsx` no mantiene estado de filtros/orden.
- `/tareas/:id` → `pages/DetalleTareaPage.tsx`: layout de dos columnas como `DetallePedidoPage` (columna principal: descripción + comentarios + `AuditLog` de `historial`; rail lateral: datos de la tarea + acciones de cambio de estado).
- Creación: modal (`ModalNuevaTarea`), no página aparte — mismo patrón que otros módulos que usan modal para altas simples (ver `ModalNuevoDocente`, `ModalNuevoRol`).

**Store mock** (`features/tareas/api/tareasStore.ts` + `tareasSeed.ts` + `tareasApi.ts`): mismo patrón exacto que `pedidosStore.ts` — singleton en memoria hidratado de `localStorage` (clave `adoc.mock.tareas.v2` — subida desde `v1` al agregar `tipo`/`proyectoId`/`tareaPadreId`/`tareasRelacionadasIds` a `Tarea`, para forzar el resembrado de navegadores con el shape viejo), `structuredClone` en lecturas/escrituras, seam de API async simulado consumido solo por hooks de React Query (`useTareas.ts`).

**Selector de Responsable, reutilizado en el alta/edición de tarea** (`features/tareas/components/SelectorResponsable.tsx`, envoltorio sobre `shared/ui/ComboboxBuscable.tsx`): se usa en el campo Responsable del formulario "Nueva Tarea"/edición — ahí sí conviene buscar por texto entre los candidatos posibles (a diferencia del filtro de la tabla, que deriva sus opciones de los datos visibles — ver más arriba), pero **no todos**: las opciones se filtran por la jerarquía de asignación (ver más abajo), no el catálogo completo. Las opciones salen de `features/tareas/api/personasSeed.ts`: un catálogo mock de candidatos (nombre + rol) acotado a Tareas — no se importa desde `features/usuarios` ni `features/docentes` (aislamiento de features); el día que exista un directorio real de usuarios (`Modules.Portal` u otro), este catálogo se reemplaza por esa fuente sin tocar el combobox. `shared/ui/FiltrosLista.tsx`/`ComboboxBuscable.tsx`/`MultiSelectFiltro.tsx` (los tipos `"fecha"`/`"numero"`/`"buscable"`/`"multiSelect"` agregados en una iteración anterior de este change) quedan como capacidades genéricas disponibles en `shared/ui`, pero **Tareas ya no los usa** para su propia tabla — los reemplazó adoptar el modelo de `Table` + `FiltroEncabezado` de Revisión.

**Jerarquía de asignación de Responsable** (`features/tareas/api/maquinaEstadosTarea.ts`): un rank map explícito, de mayor a menor autoridad. Los strings usan los nombres de rol **exactos** que devuelve `GET /api/desarrollo/identidades` (sembrados por `Modules.Identity`) — no abreviaturas propias de Tareas: un bug real, descubierto en la validación manual de este mismo change, fue usar formas cortas ("Secretaría", "Administración", "Coordinador") que no matcheaban contra `actor.rol` (derivado de `useCurrentUser` → `rol.nombre` del backend), dejando a Secretaría Académica y Administrativo sin poder usar sus propios permisos en la app real —

```ts
const ORDEN_JERARQUIA: readonly Rol[] = [
  "Decanato",
  "Secretaría Académica",
  "Administrativo",
  "Coordinador de Carrera",
  "Jefe de Cátedra",
  "Docente",
];

/** Menor índice = mayor autoridad. Ausente en el mapa = sin autoridad (nunca asigna). */
function nivelJerarquia(rol: Rol): number {
  return ORDEN_JERARQUIA.indexOf(rol);
}

/** El actor solo puede asignar Responsables de su mismo nivel o inferior (nunca superior). */
export function puedeAsignarComoResponsable(
  actor: ActorTarea,
  candidato: PersonaCandidata,
): boolean {
  return nivelJerarquia(candidato.rol) >= nivelJerarquia(actor.rol);
}
```

`SelectorResponsable.tsx` filtra `personasSeed.ts` con `puedeAsignarComoResponsable(actor, candidato)` antes de pasarlo como `opciones` al combobox — el candidato inválido directamente no aparece en la búsqueda, en vez de dejar elegirlo y rechazar al confirmar. Se aplica en el alta, en la edición (reasignar Responsable es parte de `puedeEditarCampos`, exclusivo de la autoridad creadora) y al crear una tarea hija. Como `ROLES_QUE_CREAN` (Decanato/Secretaría Académica/Administrativo) son justo los tres primeros niveles del rank map, la asignación entre pares del mismo nivel solo es alcanzable entre esos tres roles — Coordinador de Carrera/Jefe de Cátedra/Docente nunca son `actor` de esta función, solo `candidato`.

**Proyectos** (`features/tareas/api/proyectosStore.ts` + `proyectosSeed.ts` + `proyectosApi.ts`): mismo patrón exacto que `tareasStore.ts` — mock store en memoria hidratado de `localStorage` (clave `adoc.mock.tareas.proyectos.v1`), consumido solo por hooks de React Query (`useProyectos.ts`, `useAccionesProyecto.ts`). Es un store separado del de Tareas, no un campo embebido — las tareas solo guardan `proyectoId` como referencia.

**Permisos de Proyecto** (mismo archivo o uno nuevo `maquinaEstadosProyecto.ts`, funciones puras): `puedeCrearProyecto(actor)` y `puedeCambiarEstadoProyecto(actor)` son `true` si `actor.rol` ∈ {Secretaría Académica, Decanato} — más restrictivo que `puedeCrearTarea` (que también incluye Administrativo). El permiso de cambiar Estado es por **rol**, no por ownership: cualquier usuario de esos dos roles puede cambiar el estado de cualquier Proyecto, no solo el que figura como su Responsable — a diferencia de Cancelar una Tarea, que sí es exclusivo de la autoridad creadora puntual. El selector de Responsable del formulario "Nuevo Proyecto" reutiliza `puedeAsignarComoResponsable(actor, candidato)` (el mismo rank map de Tareas) filtrando además a {Secretaría Académica, Decanato}: Decanato puede asignarse el Proyecto a sí mismo o a Secretaría Académica; Secretaría Académica solo puede asignárselo a sí misma.

**Relación simple entre tareas**: `tareasRelacionadasIds: string[]` en cada tarea, mantenido bidireccional por la capa de API mock (`agregarRelacion(idA, idB)` escribe el id en ambos lados, `quitarRelacion` lo saca de ambos) — no es un cálculo derivado, es estado persistido en las dos tareas para que la consulta de lectura sea trivial. No participa de la máquina de estados: agregarla o quitarla no genera un evento de historial (es metadata de navegación, no un cambio de la tarea en sí).

**Jerarquía padre/hijas**: `tareaPadreId?: string` en la tarea hija es la única referencia — no hay una lista de `hijasIds` en el padre, se calcula filtrando `tareas.filter(t => t.tareaPadreId === padre.id)` (evita mantener dos fuentes de verdad sincronizadas). Es **agrupamiento organizacional, no un mecanismo de cálculo**: el Estado y el `porcentajeAvance` de cada tarea (padre o hija) se editan a mano con las mismas reglas de `maquinaEstadosTarea.ts` ya definidas, sin ningún rollup automático — la tarea padre no sabe, a nivel de cálculo, cuánto llevan sus hijas. Crear una hija reutiliza `puedeCrearTarea(actor)` (mismos roles que crean cualquier tarea) y `ModalNuevaTarea.tsx` con `tareaPadreId` pre-cargado; el campo Proyecto no se muestra en ese caso porque se hereda del padre (`tarea.proyectoId = padre.proyectoId`, asignado por el store al crear, no editable después). La jerarquía admite más de un nivel (una hija puede tener sus propias hijas) sin límite mecánico en el modelo.

**Pantalla inicial por Proyecto**: `IndexPage.tsx` deja de renderizar un único `TablaTareas` y pasa a agrupar las tareas por `proyectoId` (`agruparTareasPorProyecto(tareas, proyectosAbiertos)`, función pura en `components/agrupacionProyectos.ts`) en una lista de cuadros: el cuadro "Generales" siempre primero (tareas con `proyectoId` vacío), y luego un cuadro por cada Proyecto **en estado Abierto** que tenga al menos una tarea, ordenados por `proyecto.fechaFin` descendente — los Proyectos Finalizados o Cancelados no generan cuadro, sin importar si todavía tienen tareas no terminales. Cada cuadro es un componente `CuadroProyecto.tsx` con un título (el nombre del Proyecto, o "Generales") y una `TablaTareas` **sin cambios respecto al formato ya existente** (Nro, Título, Autor, Responsable, Fecha Inicio, Fecha Fin, Prioridad, % Avance, Estado, Acciones — el Tipo no se agrega como columna, solo vive en el formulario de alta y el Detalle) acotada a ese subconjunto. El título de cada cuadro de Proyecto es un link a `/tareas/proyectos/:id`; el título "Generales" no lo es. Botón "Nuevo Proyecto" en el `PageHeader`, condicionado a `puedeCrearProyecto(actor)`, junto al ya existente "Nueva Tarea".

**Acceso manual a Proyectos Finalizados/Cancelados**: como la pantalla inicial solo cubre los Proyectos Abiertos, se agrega una pantalla `ListadoProyectosPage.tsx` (`/tareas/proyectos`) con **todos** los Proyectos sin importar su Estado — una tabla simple (Nombre, Responsable, Fecha Fin, Estado), sin el modelo de filtros por columna de `TablaTareas` (no son tareas, y el volumen esperado de Proyectos es mucho menor). Un link "Ver todos los proyectos" en la pantalla inicial navega ahí; desde cada fila se navega al Detalle del Proyecto correspondiente.

## Risks / Trade-offs

- **[Riesgo] Datos no compartidos entre usuarios** (localStorage es por navegador) → Mitigación: es el mismo trade-off ya aceptado en Docentes/Roles/Usuarios; no bloquea la demo ni el flujo de UI. Se resuelve cuando exista `Modules.Tareas` backend (fuera de alcance).
- **[Riesgo] Permisos solo client-side** (cualquiera con acceso a la consola podría forzar la UI) → Mitigación: aceptable mientras no hay backend; cuando se construya `Modules.Tareas`, el Controller debe re-validar rol/ownership server-side — dejar anotado en `docs/quality/tech-debt.md` al mergear.
- **[Riesgo] Umbrales del semáforo hardcodeados (50% / 80%)** → Mitigación: aislados en una función pura (`semaforoTarea.ts`) para que parametrizarlos después sea un cambio de una sola función, no una reescritura.

## Open Questions

- ¿El comentario al marcar `pausa` es obligatorio (bloquea la transición si está vacío) u opcional? Asumido: obligatorio, para que la autoridad siempre vea el motivo de la consulta. A confirmar en la definición de flujo detallada.
- ¿Reabrir una tarea `resuelta` o revertir una `cancelada` queda dentro de alcance de este change, o se pospone? Asumido en este diseño: sí, como acción disponible solo para la autoridad creadora. A confirmar.
- ¿`porcentajeAvance` debe sincronizarse automáticamente con el Estado (ej. `resuelta` ⇒ 100%)? Asumido: no, son campos independientes que el Responsable completa por separado. A confirmar.
