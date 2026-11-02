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

| Entidad                | Descripción                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 | Schema/Tabla                |
| ---------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------- |
| `SolicitudReservaAula` | Pedido de aula/laboratorio para una mesa de examen (día, horario, materia, comisión, cantidad aproximada de alumnos, estado, aula asignada del catálogo, motivo de rechazo). Circuito: `Pendiente` → `Aprobada`, `Pendiente` → `Rechazada` (con motivo obligatorio) o `Pendiente` → `Cancelada`; una `Aprobada` todavía puede pasar a `Cancelada` por un Administrativo, con motivo obligatorio y liberando el aula. También guarda `motivo_cancelacion` y `rol_solicitante` (rol activo de quien la creó). | `aulas.solicitudes_reserva` |
| `Aula`                 | Aula o laboratorio del catálogo: nombre único (sin distinguir mayúsculas), tipo (`aula` o `laboratorio`), capacidad, equipamiento y si está activa. Lo administra quien tiene `aulas.gestionar`. Un aula con reservas no se elimina, se desactiva.                                                                                                                                                                                                                                                          | `aulas.aulas`               |

`Materia` referencia `identity.materias` (catálogo transversal, no "el catálogo de Designaciones" —
Designaciones también lo referencia, pero vive en `identity`), acotada por el backend a las materias
que el docente solicitante tiene asignadas (ver `openspec/changes/archive/2026-11-02-reserva-aulas/design.md`, decisión
3). La reserva aprobada apunta a un aula del catálogo (`aula_id`) y la base impide dos reservas
aprobadas superpuestas en la misma aula (`EXCLUDE` `solicitudes_reserva_aula_sin_superposicion`).
`Comisión` sigue siendo texto libre: su catálogo depende de la integración con Guaraní.

## API pública (contract)

| Interfaz        | Métodos                                                 | Consumido por |
| --------------- | ------------------------------------------------------- | ------------- |
| `IAulasQueries` | _(placeholder — sin consumidores cross-module todavía)_ | —             |

## Endpoints HTTP

| Método | Path                                           | Permiso                             | Descripción                                                                                                                                          |
| ------ | ---------------------------------------------- | ----------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| GET    | `/api/aulas/ping`                              | (anónimo)                           | Health check                                                                                                                                         |
| POST   | `/api/aulas/solicitudes`                       | `aulas.solicitar`                   | Crear una solicitud propia                                                                                                                           |
| GET    | `/api/aulas/solicitudes/materias-propias`      | `aulas.solicitar`                   | Listar las materias asignadas al docente autenticado (desplegable de Materia)                                                                        |
| GET    | `/api/aulas/solicitudes/mias`                  | `aulas.solicitar`                   | Listar las solicitudes propias del Docente autenticado                                                                                               |
| POST   | `/api/aulas/solicitudes/{id}/cancelar`         | `aulas.solicitar`                   | Cancelar una solicitud propia en estado `Pendiente`                                                                                                  |
| GET    | `/api/aulas/solicitudes`                       | `aulas.aprobar`                     | Listar todas las solicitudes (todos los docentes)                                                                                                    |
| POST   | `/api/aulas/solicitudes/{id}/asignar`          | `aulas.aprobar`                     | Asignar un aula activa del catálogo (`aulaId`) a una `Pendiente` (→ `Aprobada`) o actualizarla en una `Aprobada`; 409 `aula-ocupada` si se superpone |
| POST   | `/api/aulas/solicitudes/{id}/rechazar`         | `aulas.aprobar`                     | Rechazar una `Pendiente` con motivo obligatorio (→ `Rechazada`)                                                                                      |
| GET    | `/api/aulas/solicitudes/{id}`                  | dueño o `aulas.aprobar`             | Detalle de una solicitud; 404 `solicitud-not-found` para cualquier otro (no revela que existe)                                                       |
| POST   | `/api/aulas/solicitudes/{id}/cancelar-reserva` | `aulas.aprobar`                     | Cancelar una `Aprobada` con motivo obligatorio (→ `Cancelada`, libera el aula)                                                                       |
| GET    | `/api/aulas/catalogo?activas=`                 | `aulas.gestionar` o `aulas.aprobar` | Listar el catálogo; `activas=true` acota a las asignables                                                                                            |
| POST   | `/api/aulas/catalogo`                          | `aulas.gestionar`                   | Crear un aula; 409 `aula-nombre-repetido`                                                                                                            |
| PUT    | `/api/aulas/catalogo/{id}`                     | `aulas.gestionar`                   | Editar un aula (con `version`)                                                                                                                       |
| POST   | `/api/aulas/catalogo/{id}/activar\|desactivar` | `aulas.gestionar`                   | Cambiar si el aula se puede asignar (con `version`)                                                                                                  |
| DELETE | `/api/aulas/catalogo/{id}`                     | `aulas.gestionar`                   | Eliminar un aula sin reservas; 409 `aula-en-uso` si tiene                                                                                            |

## Notificaciones

Después de confirmar cada cambio, `ServicioSolicitudesAula` avisa por `INotificador` (best effort; ver [notificaciones.md](./notificaciones.md)): al docente dueño cuando se le asigna aula ("Aula asignada"), cuando el aula de una reserva aprobada cambia a otra distinta ("Aula actualizada"; repetir la misma no avisa), cuando se rechaza su solicitud y cuando se cancela su reserva (con el motivo y el aula liberada, leída antes de borrarla); y a los usuarios `administrativo` y `secretaria` cuando el docente cancela una solicitud pendiente. El aviso al dueño se ve bajo `rol_solicitante`.

## Permisos

Catálogo completo (código C# en `ArsDocendi.Shared/Auth/Permisos.cs`; dato en
`database/identity/006_identity_catalogos.sql`, `20261102000906_PermisosAulasSolicitarIdentity.sql` y
`20261102181058_PermisosAulasYVocabularioIdentity.sql`):

| Permiso           | Descripción                                                  | Roles con el permiso                                                                          |
| ----------------- | ------------------------------------------------------------ | --------------------------------------------------------------------------------------------- |
| `aulas.ver`       | Consultar el calendario de reservas de aulas y laboratorios. | `docente`, `jefe_catedra`, `coordinador_carrera`, `administrativo`, `secretaria`, `sys_admin` |
| `aulas.solicitar` | Crear y cancelar solicitudes propias de reserva de aula.     | `docente`, `jefe_catedra`                                                                     |
| `aulas.gestionar` | Administrar el catálogo de aulas y laboratorios.             | `administrativo`, `secretaria`, `sys_admin`                                                   |
| `aulas.aprobar`   | Asignar aula a una solicitud pendiente / aprobarla.          | `administrativo`, `secretaria`, `sys_admin`                                                   |

## Reglas de negocio

Ver [docs/business-rules/aulas.md](../../business-rules/aulas.md) (`BR-aulas-001` a `007`).

## Dependencias

- **Hacia adentro**: ninguna cross-module — el docente solicitante se resuelve vía
  `ArsDocendi.Shared.Identity.IConsultasIdentity` (infraestructura transversal, no un módulo de
  negocio), no vía `Modules.Portal.Contracts`.
- **Hacia afuera**: ninguna por ahora.
- **Externas**: ninguna conocida.

## Specs activas

- `detalle-solicitud-reserva-aula` y `modal-formulario-app` — detalle de la solicitud, cancelación de una
  reserva aprobada y el diálogo/campos de formulario compartidos (`openspec/specs/`).
- `reserva-aulas` — ciclo de vida completo de la solicitud de reserva de aula (crear, listar propias,
  listar todas, cancelar, asignar aula, rechazar con motivo) (`openspec/specs/reserva-aulas/spec.md`).
- `catalogo-aulas` — catálogo de aulas y asignación sin superposición. Ver
  `openspec/changes/archive/2026-11-02-catalogo-aulas-y-vocabulario-portal/`.

## Decisiones registradas

- **Detalle por solicitud y cancelación de reservas**: cada solicitud tiene una página
  (`/aulas/solicitudes/:id`) y las dos tablas la abren con un click en cualquier fila. Un Administrativo
  puede cancelar una reserva aprobada con motivo. **Reemplaza la decisión 8 de
  `openspec/changes/archive/2026-11-02-reserva-aulas/design.md`** (doble click por estado). Ver
  `openspec/changes/archive/2026-11-02-reserva-aulas-detalle-y-cancelacion/`.

- **Catálogo de aulas**: la asignación elige un aula activa del catálogo; la superposición la impide la
  base y superar la capacidad sólo advierte. Ver `openspec/changes/archive/2026-11-02-catalogo-aulas-y-vocabulario-portal/`.

- **Pendientes fuera de alcance**: el catálogo de comisiones depende de la integración con Guaraní; no
  hay períodos de exámenes (se puede pedir aula para cualquier día).

- **Solo exámenes**: el alcance no incluye aulas para clases regulares — ese flujo existe institucionalmente por otra vía y no se va a duplicar.
