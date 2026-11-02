# Domain: Aulas

## Propósito

Gestión de **pedidos y asignación de aulas y laboratorios** para mesas de examen. No cubre el uso académico regular, que se coordina por otra vía institucional y no se va a duplicar.

## Roles que interactúan

- **Docente / Jefe de Cátedra** — Solicita aula para examen.
- **Administrativos** — Gestionan las reservas, asignan aulas, resuelven conflictos.
- **Secretaría Académica** — Configurables del módulo (qué aulas existen, capacidades, equipamiento).

## Bounded context

- **Pertenece**: Aulas, laboratorios, pedidos de reserva, asignaciones, períodos de exámenes.
- **No pertenece**: Usuarios del aula (vienen del Portal), información académica de las materias.

## Entidades principales

| Entidad                | Descripción                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    | Schema/Tabla                |
| ---------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------- |
| `SolicitudReservaAula` | Pedido de aula/laboratorio para una mesa de examen (día, horario, materia, comisión, cantidad aproximada de alumnos, estado, aula asignada, motivo de rechazo). Circuito: `Pendiente` → `Aprobada`, `Pendiente` → `Rechazada` (con motivo obligatorio) o `Pendiente` → `Cancelada`; una `Aprobada` todavía puede pasar a `Cancelada` por un Administrativo, con motivo obligatorio y liberando el aula. También guarda `motivo_cancelacion` y `rol_solicitante` (rol activo de quien la creó). | `aulas.solicitudes_reserva` |

`Materia` referencia `identity.materias` (catálogo transversal, no "el catálogo de Designaciones" —
Designaciones también lo referencia, pero vive en `identity`), acotada por el backend a las materias
que el docente solicitante tiene asignadas (ver `openspec/changes/reserva-aulas/design.md`, decisión
3). `Comisión` y `AulaAsignada` siguen siendo texto libre: no hay catálogo de comisiones ni de
aulas/laboratorios todavía.

## API pública (contract)

| Interfaz        | Métodos                                                 | Consumido por |
| --------------- | ------------------------------------------------------- | ------------- |
| `IAulasQueries` | _(placeholder — sin consumidores cross-module todavía)_ | —             |

## Endpoints HTTP

| Método | Path                                           | Permiso                 | Descripción                                                                                                     |
| ------ | ---------------------------------------------- | ----------------------- | --------------------------------------------------------------------------------------------------------------- |
| GET    | `/api/aulas/ping`                              | (anónimo)               | Health check                                                                                                    |
| POST   | `/api/aulas/solicitudes`                       | `aulas.solicitar`       | Crear una solicitud propia                                                                                      |
| GET    | `/api/aulas/solicitudes/materias-propias`      | `aulas.solicitar`       | Listar las materias asignadas al docente autenticado (desplegable de Materia)                                   |
| GET    | `/api/aulas/solicitudes/mias`                  | `aulas.solicitar`       | Listar las solicitudes propias del Docente autenticado                                                          |
| POST   | `/api/aulas/solicitudes/{id}/cancelar`         | `aulas.solicitar`       | Cancelar una solicitud propia en estado `Pendiente`                                                             |
| GET    | `/api/aulas/solicitudes`                       | `aulas.aprobar`         | Listar todas las solicitudes (todos los docentes)                                                               |
| POST   | `/api/aulas/solicitudes/{id}/asignar`          | `aulas.aprobar`         | Asignar aula a una `Pendiente` (→ `Aprobada`) o actualizar el aula de una ya `Aprobada` (sin cambiar el estado) |
| POST   | `/api/aulas/solicitudes/{id}/rechazar`         | `aulas.aprobar`         | Rechazar una `Pendiente` con motivo obligatorio (→ `Rechazada`)                                                 |
| GET    | `/api/aulas/solicitudes/{id}`                  | dueño o `aulas.aprobar` | Detalle de una solicitud; 404 `solicitud-not-found` para cualquier otro (no revela que existe)                  |
| POST   | `/api/aulas/solicitudes/{id}/cancelar-reserva` | `aulas.aprobar`         | Cancelar una `Aprobada` con motivo obligatorio (→ `Cancelada`, libera el aula)                                  |

## Permisos

Catálogo completo (código C# en `ArsDocendi.Shared/Auth/Permisos.cs`; dato en
`database/identity/006_identity_catalogos.sql` + `20261102000906_PermisosAulasSolicitarIdentity.sql`):

| Permiso           | Descripción                                                    | Roles con el permiso                                      |
| ----------------- | -------------------------------------------------------------- | --------------------------------------------------------- |
| `aulas.ver`       | Consultar el calendario de reservas de aulas y laboratorios.   | `docente`, `jefe_catedra`, `administrativo`, `secretaria` |
| `aulas.solicitar` | Crear y cancelar solicitudes propias de reserva de aula.       | `docente`, `jefe_catedra`                                 |
| `aulas.gestionar` | Solicitar y asignar aulas o laboratorios (reservado a futuro). | `administrativo`, `secretaria`                            |
| `aulas.aprobar`   | Asignar aula a una solicitud pendiente / aprobarla.            | `administrativo`, `secretaria`                            |

## Reglas de negocio

Este módulo todavía no tiene reglas provenientes de normativa institucional registradas.

## Dependencias

- **Hacia adentro**: ninguna cross-module — el docente solicitante se resuelve vía
  `ArsDocendi.Shared.Identity.IConsultasIdentity` (infraestructura transversal, no un módulo de
  negocio), no vía `Modules.Portal.Contracts`.
- **Hacia afuera**: ninguna por ahora.
- **Externas**: ninguna conocida.

## Specs activas

- `detalle-solicitud-reserva-aula` y `modal-formulario-app` — detalle de la solicitud, cancelación de una
  reserva aprobada y el diálogo/campos de formulario compartidos. Ver
  `openspec/changes/reserva-aulas-detalle-y-cancelacion/` (o `openspec/specs/` una vez archivado).

- `reserva-aulas` — ciclo de vida completo de la solicitud de reserva de aula (crear, listar propias,
  listar todas, cancelar, asignar aula, rechazar con motivo), con la tabla ordenable/filtrable que las
  expone. Ver `openspec/changes/reserva-aulas/` (o `openspec/specs/reserva-aulas/spec.md` una vez
  archivado).

## Decisiones registradas

- **Detalle por solicitud y cancelación de reservas**: cada solicitud tiene una página
  (`/aulas/solicitudes/:id`) y las dos tablas la abren con un click en cualquier fila. Un Administrativo
  puede cancelar una reserva aprobada con motivo. **Reemplaza la decisión 8 de
  `openspec/changes/reserva-aulas/design.md`** (doble click por estado). Ver
  `openspec/changes/reserva-aulas-detalle-y-cancelacion/`.

- **Solo exámenes**: el alcance no incluye aulas para clases regulares — ese flujo existe institucionalmente por otra vía y no se va a duplicar.
