# Domain: Tareas

## Propósito

**Seguimiento de tareas internas** del departamento: tablero tipo Trello con semáforo de vencimiento. Es coordinación administrativa liviana, no gestión de proyectos complejos ni un circuito de aprobación (eso vive en Designaciones).

## Roles

- **Decanato** y **Secretaría Académica**: crean tareas y son los únicos que crean y cierran Proyectos.
- **Administrativos**: usuarios principales del tablero; crean tareas, pero no Proyectos.
- **Coordinador de Carrera**, **Jefe de Cátedra** y **Docente**: reciben asignaciones.

La asignación de Responsable respeta una jerarquía (nunca se asigna hacia arriba), definida en la spec [tareas](../../../openspec/specs/tareas/spec.md).

## Bounded context

- **Pertenece**: tareas (con jerarquía padre/hijas y relaciones simples), proyectos, comentarios internos, historial y vencimientos.
- **No pertenece**: designaciones y reservas de aulas.

## Entidades principales

| Entidad    | Descripción                                                                                                  | Schema     |
| ---------- | ------------------------------------------------------------------------------------------------------------ | ---------- |
| `Tarea`    | Unidad de trabajo con Responsable, plazo, estado y % de avance; puede tener hijas y pertenecer a un Proyecto | `tareas.*` |
| `Proyecto` | Agrupa tareas; estado Abierto / Finalizado / Cancelado                                                       | `tareas.*` |

## Comportamiento

El comportamiento vigente está en las specs; este documento no lo repite:

- [tareas](../../../openspec/specs/tareas/spec.md): campos, jerarquía, relaciones y asignación.
- [flujo-estado-tareas](../../../openspec/specs/flujo-estado-tareas/spec.md): estados y quién puede moverlos.
- [tablero-tareas](../../../openspec/specs/tablero-tareas/spec.md): cuadros por Proyecto, filtros y semáforo.
- [proyectos](../../../openspec/specs/proyectos/spec.md): alta, edición y cierre de Proyectos.
- [tareas-api](../../../openspec/specs/tareas-api/spec.md): rutas, permisos, versionado y persistencia.

## Dependencias

- **Hacia adentro**: lectura de identity vía `IConsultasIdentity`.
- **Hacia afuera**: ninguna.
- **Externas**: ninguna.

## Decisiones registradas

- **Trello, no Jira**: el alcance es coordinación interna liviana; no se agregan flujos de aprobación dentro del módulo.
- **Sin rollup**: el estado y el % de avance de una tarea padre no se derivan de sus hijas; cada tarea es independiente.
- **Sin BR**: ninguna regla de Tareas proviene de normativa institucional ([business-rules/tareas.md](../../business-rules/tareas.md)).
