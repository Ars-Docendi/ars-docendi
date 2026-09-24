# Domain: Tareas

## Propósito

**Seguimiento de tareas internas** del departamento. Tablero tipo Trello con **semáforo de vencimiento** (verde → amarillo → rojo según proximidad de la fecha límite). Pensado para coordinación administrativa, no para gestión de proyectos complejos.

## Roles que interactúan

- **Administrativos** — Usuarios principales del tablero. Pueden crear tareas, pero no Proyectos.
- **Secretaría Académica** — Vista global, asignación de tareas. Junto con Decanato, únicos roles que crean y gestionan Proyectos.
- **Coordinador de Carrera** / **Jefe de Cátedra** — Pueden recibir asignaciones de tareas.
- **Decanato** — Vista global. Máxima autoridad en la jerarquía de asignación (ver "Decisiones registradas").

Jerarquía de autoridad, de mayor a menor: Decanato → Secretaría Académica → Administrativos → Coordinador de Carrera → Jefe de Cátedra → Docente. Quien asigna un Responsable (siempre uno de los tres primeros roles, los únicos que crean tareas) solo puede elegir a alguien de su mismo nivel o de un nivel inferior, nunca superior.

## Bounded context

- **Pertenece**: Tareas, listas/columnas del tablero, asignados, vencimientos, estados, comentarios internos.
- **No pertenece**: Designaciones, reservas de aulas (cada uno con su propio flujo).

## Entidades principales

| Entidad    | Descripción                                                                                                                                                                                                                                                                                                                                                                                       | Schema/Tabla |
| ---------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------ |
| `Tarea`    | Nro correlativo, Título, Descripción, Fecha Inicio, Fecha Fin, Prioridad (alta/media/baja), Tipo (Extensión/Administrativa/Posgrado/Investigación/Académicas/Decanato), Estado, % de avance (0-100), Solución (al resolverse), Responsable, Autor (creador), comentarios internos, historial de auditoría, Proyecto asociado (opcional), tarea padre (si es una hija), tareas relacionadas (ids). | `tareas.*`   |
| `Proyecto` | Nro correlativo, Nombre, Descripción, Fecha Inicio, Fecha Fin, Estado (Abierto/Finalizado/Cancelado), Responsable (Secretaría Académica o Decanato). Agrupa un subconjunto de tareas.                                                                                                                                                                                                             | `tareas.*`   |

_(frontend-first: hoy `Tarea` y `Proyecto` viven solo como mock en `frontend/src/features/tareas` — ver `openspec/changes/sistema-tareas/specs/`; el schema `tareas.*` se crea cuando exista `Modules.Tareas` backend.)_

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

Este módulo todavía no tiene reglas provenientes de normativa institucional registradas.

## Dependencias

- **Hacia adentro**: `Modules.Portal.Contracts` (conocer al Responsable/Autor) — todavía no se consume: el change `sistema-tareas` es frontend-first con un catálogo mock propio (`features/tareas/api/personasSeed.ts`), sin backend real.
- **Hacia afuera**: por ahora ninguna.
- **Externas**: ninguna conocida.

## Decisiones registradas

- **Inspirado en Trello, no es Jira**: el alcance es coordinación interna ligera. Sin flujos complejos de aprobación dentro del módulo (eso está en `Designaciones`).
- **Pantalla inicial organizada en cuadros por Proyecto**: un cuadro fijo "Generales" (siempre primero, tareas sin Proyecto asociado) y uno por cada Proyecto en estado Abierto que tenga tareas, ordenados por Fecha de Fin del Proyecto más reciente. Cada cuadro tiene la misma tabla que antes (columnas, filtros por header, orden de 3 estados, semáforo). Ver `openspec/changes/sistema-tareas/specs/tablero-tareas/spec.md`.
- **Ciclo de estados**: Pendiente / En curso / Pausa / Resuelta / Cancelada. El Responsable mueve la tarea libremente entre los primeros cuatro; Cancelar (y editar Título/Descripción/fechas/Prioridad/Tipo/Responsable/Proyecto) es exclusivo de la autoridad creadora (Secretaría, Decanato o Administración — únicos roles que además pueden crear tareas). Pasar a Pausa exige un comentario con el motivo; pasar a Resuelta exige completar el campo Solución. Ver `openspec/changes/sistema-tareas/specs/flujo-estado-tareas/spec.md`.
- **Semáforo de vencimiento** como feature visual obligatoria, calculado por **% del plazo transcurrido** (no días fijos): verde por debajo del 50%, amarillo entre 50-80%, rojo desde el 80% (incluida vencida). Solo se muestra en estados no terminales. El umbral no es parametrizable todavía (fuera de alcance del primer change).
- **% de avance** (0-100), lo completa el Responsable, independiente del Estado (no se sincronizan automáticamente). Se mantiene así también para tareas con hijas: no hay rollup automático de avance/estado desde las hijas hacia el padre, es una decisión explícita.
- **Jerarquía de asignación de Responsable**: Decanato → Secretaría Académica → Administrativos → Coordinador de Carrera → Jefe de Cátedra → Docente. Quien asigna solo puede elegir a alguien de su mismo nivel o inferior, nunca superior — el buscador de Responsable filtra las opciones en consecuencia (no permite elegir y después rechaza). Aplica también al Responsable de Proyecto, acotado además a {Decanato, Secretaría Académica}.
- **Proyecto**: entidad separada de Tarea (mock store propio), creada y gestionada exclusivamente por Decanato y Secretaría Académica. Una tarea puede asociarse a un Proyecto opcionalmente; una tarea hija hereda el Proyecto de su padre de forma obligatoria (no editable aparte). Los Proyectos Finalizados/Cancelados no generan cuadro en la pantalla inicial — se acceden desde el listado completo de Proyectos (`/tareas/proyectos`).
- **Relación simple entre tareas**: vínculo bidireccional de acceso rápido entre dos tareas, sin jerarquía ni efecto en Estado/% de avance.
- **Tareas hijas (jerarquía padre/hijas)**: agrupamiento organizacional para descomponer una tarea compleja, multinivel (una hija puede tener sus propias hijas). Cada tarea (padre o hija) es independiente y completa: su propio Responsable, fechas, Estado y % de avance.
