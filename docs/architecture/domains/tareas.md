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

| Entidad                                  | Descripción | Schema/Tabla |
| ---------------------------------------- | ----------- | ------------ |
| _(a definir en spec inicial del módulo)_ | ...         | `tareas.*`   |

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

- **Hacia adentro**: `Modules.Portal.Contracts` (conocer al docente asignado).
- **Hacia afuera**: por ahora ninguna.
- **Externas**: ninguna conocida.

## Decisiones registradas

- **Inspirado en Trello, no es Jira**: el alcance es coordinación interna ligera. Sin flujos complejos de aprobación dentro del módulo (eso está en `Designaciones`).
- **Semáforo de vencimiento** como feature visual obligatoria: verde si vence en > N días, amarillo si vence en < N días, rojo si está vencida. N parametrizable por Secretaría.
