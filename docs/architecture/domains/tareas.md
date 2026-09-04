# Domain: Tareas

## Propósito

**Seguimiento de tareas internas** del departamento. Tablero tipo Trello con **semáforo de vencimiento** (verde → amarillo → rojo según proximidad de la fecha límite). Pensado para coordinación administrativa, no para gestión de proyectos complejos.

## Roles que interactúan

- **Administrativos** — Usuarios principales del tablero.
- **Secretaría Académica** — Vista global, asignación de tareas.
- **Coordinador de Carrera** / **Jefe de Cátedra** — Pueden recibir asignaciones de tareas relacionadas.
- **Decanato** — Vista global.

## Bounded context

- **Pertenece**: Tareas, listas/columnas del tablero, asignados, vencimientos, estados, comentarios internos.
- **No pertenece**: Designaciones, reservas de aulas (cada uno con su propio flujo).

## Entidades principales

| Entidad | Descripción                                                                                                                                                                                                                    | Schema/Tabla |
| ------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------ |
| `Tarea` | Nro correlativo, Título, Descripción, Fecha Inicio, Fecha Fin, Prioridad (alta/media/baja), Estado, % de avance (0-100), Solución (al resolverse), Responsable, Autor (creador), comentarios internos, historial de auditoría. | `tareas.*`   |

_(frontend-first: hoy `Tarea` vive solo como mock en `frontend/src/features/tareas` — ver `openspec/specs/tareas/spec.md`; el schema `tareas.*` se crea cuando exista `Modules.Tareas` backend.)_

## API pública (contract)

| Interfaz                                                              | Métodos | Consumido por |
| --------------------------------------------------------------------- | ------- | ------------- |
| _(a definir — probablemente ninguna por ahora, módulo autocontenido)_ | ...     | ...           |

## Endpoints HTTP

| Método                    | Path               | Rol       | Descripción  |
| ------------------------- | ------------------ | --------- | ------------ |
| GET                       | `/api/tareas/ping` | (anónimo) | Health check |
| _(a documentar en specs)_ | ...                | ...       | ...          |

## Reglas de negocio

Ver [`docs/business-rules/tareas.md`](../../business-rules/tareas.md) (a crear).

## Dependencias

- **Hacia adentro**: `Modules.Portal.Contracts` (conocer al Responsable/Autor) — todavía no se consume: el change `sistema-tareas` es frontend-first con un catálogo mock propio (`features/tareas/api/personasSeed.ts`), sin backend real.
- **Hacia afuera**: por ahora ninguna.
- **Externas**: ninguna conocida.

## Decisiones registradas

- **Inspirado en Trello, no es Jira**: el alcance es coordinación interna ligera. Sin flujos complejos de aprobación dentro del módulo (eso está en `Designaciones`).
- **Pantalla única de listado**, la misma para todos los roles — no hay tablero Kanban por columnas; ver `openspec/specs/tablero-tareas/spec.md`.
- **Ciclo de estados**: Pendiente / En curso / Pausa / Resuelta / Cancelada. El Responsable mueve la tarea libremente entre los primeros cuatro; Cancelar (y editar Título/Descripción/fechas/Prioridad/Responsable) es exclusivo de la autoridad creadora (Secretaría, Decanato o Administración — únicos roles que además pueden crear tareas). Pasar a Pausa exige un comentario con el motivo; pasar a Resuelta exige completar el campo Solución. Ver `openspec/specs/flujo-estado-tareas/spec.md`.
- **Semáforo de vencimiento** como feature visual obligatoria, calculado por **% del plazo transcurrido** (no días fijos): verde por debajo del 50%, amarillo entre 50-80%, rojo desde el 80% (incluida vencida). Solo se muestra en estados no terminales. El umbral no es parametrizable todavía (fuera de alcance del primer change).
- **% de avance** (0-100), lo completa el Responsable, independiente del Estado (no se sincronizan automáticamente).
