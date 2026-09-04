## Context

`frontend/src/features/tareas` existe como placeholder (`IndexPage.tsx` con "Módulo en construcción", `types.ts` vacío). No hay backend (`Modules.Tareas`) más allá del ping de smoke test. El resto de los módulos frontend ya resolvieron este mismo problema con un patrón consistente que este change replica:

- **Designaciones** (`features/designaciones`): store mock singleton en memoria + localStorage (`pedidosStore.ts`), un seam de API que simula async (`pedidosApi.ts`), hooks de React Query (`usePedidos.ts`, `useAccionesPedido.ts`), y derivación de rol/ámbito vía `useActorContexto` → `useCurrentUser`.
- **Docentes / Roles / Usuarios**: mismo patrón `mockStore.ts` + `IndexPage.tsx` con tabla + modales.

Este change sigue el mismo patrón para Tareas: no hay necesidad de diseño de API HTTP real todavía, así que el foco del diseño es el modelo de datos, la máquina de estados y la reutilización de componentes de `shared/ui`.

## Goals / Non-Goals

**Goals:**

- Modelar `Tarea` con un ciclo de estados claro y sus transiciones permitidas.
- Reutilizar los primitivos ya validados en Designaciones (`PageHeader` con `actions`, `FiltrosLista`, `AuditLog`, patrón de store mock) en vez de reinventar UI.
- Dejar la lógica de visibilidad/permisos aislada en funciones puras (espejo de `maquinaEstados.ts` de Designaciones) para que sea fácil de testear y de reemplazar el día que exista backend real.

**Non-Goals:**

- Persistencia real / API HTTP de `Modules.Tareas` (el store vive en `localStorage`, por navegador, no compartido entre usuarios — igual que Docentes/Roles/Usuarios hoy).
- Sistema de permisos configurable (eso es `roles-membresia`, change de otro equipo — este change solo lee `Role` vía `useCurrentUser`, no lo modifica).
- Asignación múltiple, adjuntos, notificaciones, parametrización del umbral del semáforo.

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
  estado: EstadoTarea;
  porcentajeAvance: number; // 0-100, lo completa el Responsable
  solucion?: string; // detalle de resolución; obligatorio al pasar a "resuelta"
  responsable: { nombre: string; rol: Rol };
  creadoPor: { nombre: string; rol: Rol };
  comentarios: ComentarioTarea[];
  historial: EventoHistorialTarea[];
}
```

**Máquina de estados** (`features/tareas/api/maquinaEstadosTarea.ts`, funciones puras testeables — espejo de `maquinaEstados.ts` de Designaciones):

- El **Responsable** mueve la tarea libremente entre `pendiente` / `en_curso` / `pausa` / `resuelta` (no es una secuencia estricta: puede, por ejemplo, volver de `resuelta` a `en_curso` si la autoridad la reabre, o pasar de `pendiente` directo a `pausa` si tiene una consulta antes de arrancar).
- `→ pausa`: requiere comentario (se agrega automáticamente como primer `ComentarioTarea` del hilo, para que la autoridad vea el motivo sin abrir el historial).
- `→ resuelta`: requiere que el campo `solucion` no esté vacío — el Responsable debe ingresar el detalle de cómo se resolvió antes de que la transición se confirme.
- `→ cancelada`: reservada a la **autoridad creadora** — el Responsable no puede cancelar, solo la autoridad. Disponible desde cualquier estado no terminal.
- `cancelada` y `resuelta` son terminales para el Responsable; solo la autoridad creadora puede reabrir una tarea `resuelta` (llevarla de vuelta a `en_curso`) o revertir una `cancelada`.

**Permisos** (funciones puras, mismo archivo):

- `puedeCrearTarea(actor)`: `true` si `actor.rol` ∈ {Secretaría, Decanato, Administración} — controla la visibilidad del botón "Nueva Tarea".
- `puedeEditarCampos(tarea, actor)`: `true` si `actor.nombre === tarea.creadoPor.nombre` — cubre Título, Descripción, fechas, Prioridad y Responsable.
- `puedeCambiarEstado(tarea, actor, estadoDestino)`: si `estadoDestino === "cancelada"` → solo la autoridad creadora; si no → `true` para el Responsable (`actor.nombre === tarea.responsable.nombre`) o la autoridad creadora.
- `puedeEditarAvance(tarea, actor)`: `true` si `actor.nombre === tarea.responsable.nombre` o es la autoridad creadora — cubre `porcentajeAvance` y `solucion`, que **no** son parte de `puedeEditarCampos` (el Responsable los completa aunque no pueda editar el resto de la tarea).

**Semáforo de vencimiento** (`features/tareas/components/semaforoTarea.ts`, función pura): el umbral es relativo a la duración de la tarea, no un número fijo de días. Con `totalDias = fechaFin - fechaInicio` y `transcurridos = hoy - fechaInicio`, el porcentaje transcurrido `= transcurridos / totalDias`:

- `< 50%` transcurrido → `"green"` — caso normal, **sin resaltado** (no es que la fila se pinte de verde; simplemente no se destaca).
- `≥ 50%` y `< 80%` transcurrido → `"yellow"` → el **fondo de toda la fila** (`TablaTareas.tsx`, no solo la celda Fecha Fin) se pinta de amarillo.
- `≥ 80%` transcurrido (resta ≤20% del plazo) — incluye vencida (`>100%`) — → `"red"` → fondo de fila rojo.

Se aplica solo si el estado es no terminal (`pendiente`/`en_curso`/`pausa`); una tarea `resuelta` o `cancelada` no muestra semáforo (fondo normal). En el Detalle de la tarea (con más espacio que una celda de tabla) se usa en cambio el componente `TrafficLight` de `@ars-docendi/ui`, que sí muestra label + fecha.

**Columnas del listado**: además de Nro, Título, Fecha Inicio, Fecha Fin, Prioridad y Estado, la tabla agrega **Autor** y **Responsable** (nombre de cada actor) y **% Avance** (el valor de `porcentajeAvance`, mostrado como barra o número). Con 9 columnas la tabla se acerca al límite razonable de una fila legible; si en la práctica queda muy angosta, es un ajuste de estilos (`TablaTareas.tsx`), no de alcance.

**Orden del listado** (`features/tareas/components/ordenTareas.ts`, función pura, espejo del patrón de `semaforoTarea.ts`): por defecto ordena por `fechaInicio` ascendente. Cada header de `TablaTareas.tsx` es un botón clickeable que llama `onOrdenar(clave)`; `IndexPage.tsx` guarda el estado `{ clave, direccion }` y aplica `ordenarTareas` sobre las tareas ya filtradas (orden corre después del filtro). Un click sobre la columna activa alterna asc/desc (`siguienteOrden`); sobre una columna distinta, arranca en ascendente. Comparadores por tipo: texto (Título/Autor/Responsable) alfabético sin distinguir mayúsculas, fechas ISO comparadas como string (ya ordenan cronológicamente), Prioridad/Estado por un rango explícito (no alfabético — "Alta" no es mayor que "Baja" por orden de letras), Nro/% Avance numérico.

**Solución y % de avance, exclusivos del Responsable**: `porcentajeAvance` (0-100) y `solucion` los completa el Responsable — independientes de `puedeEditarCampos` (que sigue siendo exclusivo de la autoridad creadora). `porcentajeAvance` es un campo informativo de seguimiento manual: no dispara transiciones de estado automáticas (llegar a 100% no fuerza `resuelta`, ni marcar `resuelta` fuerza 100%) — quedan desacoplados a propósito para no adivinar una regla de negocio que no se pidió.

**Indicador de Pausa en el listado**: la fila de una tarea en estado `pausa` se resalta (badge/ícono distintivo en la columna Estado, visible para cualquiera que vea el listado, pero con foco en que la autoridad creadora la note) — mismo mecanismo visual que ya usa `EstadoPedidoBadge`/`EstadoPedidoPill` en Designaciones, adaptado a `EstadoTarea`.

**Pantallas y rutas** (`features/tareas/routes.tsx`):

- `/tareas` (índice, reemplaza el placeholder actual) → `pages/IndexPage.tsx`: listado único para todos los roles, con `PageHeader` (`actions` = botón "Nueva Tarea" condicionado a `puedeCrearTarea`), `FiltrosLista` (un filtro por columna, ver "Filtros del listado" más abajo, mismo componente que Designaciones) y `TablaTareas`.
- `/tareas/:id` → `pages/DetalleTareaPage.tsx`: layout de dos columnas como `DetallePedidoPage` (columna principal: descripción + comentarios + `AuditLog` de `historial`; rail lateral: datos de la tarea + acciones de cambio de estado).
- Creación: modal (`ModalNuevaTarea`), no página aparte — mismo patrón que otros módulos que usan modal para altas simples (ver `ModalNuevoDocente`, `ModalNuevoRol`).

**Store mock** (`features/tareas/api/tareasStore.ts` + `tareasSeed.ts` + `tareasApi.ts`): mismo patrón exacto que `pedidosStore.ts` — singleton en memoria hidratado de `localStorage` (clave `adoc.mock.tareas.v1`), `structuredClone` en lecturas/escrituras, seam de API async simulado consumido solo por hooks de React Query (`useTareas.ts`).

**Filtros del listado — uno por cada columna de la tabla**: Nro de Tarea, Título, Autor, Responsable, Fecha Inicio, Fecha Fin, Prioridad, % Avance y Estado. Siguiendo el patrón ya usado en Designaciones (`FiltrosLista`), no todos están visibles de entrada:

- **Fijos** (siempre visibles, primera fila): Nro de Tarea, Responsable, Título.
- **Opcionales** (se agregan vía "+ Añadir filtro"): Autor, Estado, Prioridad, % Avance, Fecha Inicio, Fecha Fin.

`shared/ui/FiltrosLista.tsx` hoy solo soporta campos `"texto"` y `"select"`. Este change le agrega cuatro tipos más:

- `"fecha"` (input `type="date"`, ya usado en otros formularios del proyecto vía `Input` de `@ars-docendi/ui`): semántica "hasta esta fecha" (`≤`) — el filtro de Fecha Inicio muestra tareas cuya Fecha Inicio es anterior o igual a la fecha elegida, y análogamente para Fecha Fin.
- `"numero"` (input `type="number"`), usado para % Avance. El valor de negocio de este filtro es marginal (nadie va a buscar "tareas con exactamente 40% de avance" con frecuencia) — se incluye únicamente para cumplir la regla "un filtro por columna", así que se mantiene con la semántica más simple posible: **coincidencia exacta** (`porcentajeAvance === valor ingresado`), sin invertir esfuerzo de diseño en umbrales o rangos que nadie pidió.
- `"buscable"` (combobox: input de texto que filtra una lista de opciones a medida que se escribe, y se selecciona un resultado de la lista desplegada — no un `<select>` nativo), usado para **Responsable**: con muchos usuarios candidatos, tipear para buscar y elegir es más usable que desplazarse por un `<select>` largo. Recibe las mismas `opciones: {value, label}[]` que `"select"`, solo cambia la interacción de elegirlas.
- `"multiSelect"` (botón + desplegable de checkboxes — nuevo componente `shared/ui/MultiSelectFiltro.tsx`), usado para **Estado**: el usuario quiere ver, por ejemplo, "Pendiente y En curso" a la vez, algo que un `<select>` de una sola opción no permite. El valor viaja como string CSV (`"pendiente,en_curso"`) para seguir encajando en el `Record<string, string>` genérico de `FiltrosLista`; vacío equivale a "todos" (sin filtrar). Solo se agregó a `CampoFiltroOpcional` (no a `CampoFiltroFijo`) porque es lo único que este change necesita — YAGNI.

Las cuatro son extensiones aditivas y retrocompatibles, disponibles para cualquier feature que las necesite después.

**Selector de Responsable, reutilizado en filtro y alta**: el mismo combobox buscable (`features/tareas/components/SelectorResponsable.tsx`, envoltorio delgado sobre el tipo `"buscable"` de `FiltrosLista`, o un componente standalone si el formulario de alta no pasa por `FiltrosLista`) se usa tanto en el filtro Responsable del listado como en el campo Responsable del formulario "Nueva Tarea" — es la misma pregunta ("elegí una persona de una lista, buscando por texto") en dos lugares, no dos componentes distintos. Las opciones salen de `features/tareas/api/personasSeed.ts`: un catálogo mock de candidatos (nombre + rol) acotado a Tareas — no se importa desde `features/usuarios` ni `features/docentes` (aislamiento de features); el día que exista un directorio real de usuarios (`Modules.Portal` u otro), este catálogo se reemplaza por esa fuente sin tocar el combobox.

**Configuraciones de filtro guardadas**: el usuario puede guardar la combinación actual de filtros — los fijos (Nro de Tarea, Responsable, Título) y los opcionales que haya agregado, con todos sus valores — con un nombre, y volver a aplicarla después. Esto es específico de Tareas (no se generaliza a `shared/`, para no acoplar otras pantallas a un mecanismo que todavía no necesitan): `features/tareas/api/filtrosGuardadosStore.ts` — mismo patrón de store mock en `localStorage` (clave `adoc.mock.tareas.filtros.v1`), con las configuraciones asociadas al `nombre` del actor actual (no hay autenticación real, así que "por usuario" es un mock por nombre, igual que el resto del módulo). Un `Select` en la barra de filtros permite aplicar una configuración guardada; un botón "Guardar filtros" abre un modal simple para nombrarla.

## Risks / Trade-offs

- **[Riesgo] Datos no compartidos entre usuarios** (localStorage es por navegador) → Mitigación: es el mismo trade-off ya aceptado en Docentes/Roles/Usuarios; no bloquea la demo ni el flujo de UI. Se resuelve cuando exista `Modules.Tareas` backend (fuera de alcance).
- **[Riesgo] Permisos solo client-side** (cualquiera con acceso a la consola podría forzar la UI) → Mitigación: aceptable mientras no hay backend; cuando se construya `Modules.Tareas`, el Controller debe re-validar rol/ownership server-side — dejar anotado en `docs/quality/tech-debt.md` al mergear.
- **[Riesgo] Umbrales del semáforo hardcodeados (50% / 80%)** → Mitigación: aislados en una función pura (`semaforoTarea.ts`) para que parametrizarlos después sea un cambio de una sola función, no una reescritura.

## Open Questions

- ¿El comentario al marcar `pausa` es obligatorio (bloquea la transición si está vacío) u opcional? Asumido: obligatorio, para que la autoridad siempre vea el motivo de la consulta. A confirmar en la definición de flujo detallada.
- ¿Reabrir una tarea `resuelta` o revertir una `cancelada` queda dentro de alcance de este change, o se pospone? Asumido en este diseño: sí, como acción disponible solo para la autoridad creadora. A confirmar.
- ¿`porcentajeAvance` debe sincronizarse automáticamente con el Estado (ej. `resuelta` ⇒ 100%)? Asumido: no, son campos independientes que el Responsable completa por separado. A confirmar.
