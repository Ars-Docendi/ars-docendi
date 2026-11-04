# Domain: Notificaciones

## Propósito

Avisar dentro del sistema (in-app) a la persona que tiene que actuar o enterarse de algo que le pasó a un trámite suyo: un aula asignada, un pedido devuelto, una tarea reasignada. El correo es otro trabajo y está fuera de este módulo. Además del panel, el aviso llega como notificación push al celular o al navegador (Web Push) si la persona lo activó.

## Roles que interactúan

Todos los usuarios autenticados, sobre sus propias notificaciones y bajo el rol con que operan. No hay una vista administrativa ni permisos propios: la autorización es "es tuya y es de tu perfil activo".

## Bounded context

- **Pertenece**: la notificación (una fila por usuario destinatario), su estado de lectura, la resolución de destinatarios, el contrato `INotificador`, las suscripciones Web Push por dispositivo, las preferencias de push por módulo y el envío.
- **No pertenece**: el hecho que la origina (vive en Aulas, Designaciones o Tareas), el texto de cada aviso (lo arma el módulo emisor con su propio vocabulario y queda congelado), la identidad y los roles (se leen de `identity`).

## Entidades principales

| Entidad           | Descripción                                                                                                                               | Schema/Tabla                        |
| ----------------- | ----------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------- |
| `Notificacion`    | Aviso para un usuario: perfil de destino opcional, evento, entidad de origen, título y mensaje inmutables, contexto JSON, lectura y alta. | `notificaciones.notificaciones`     |
| `SuscripcionPush` | Dispositivo (endpoint + claves) de un usuario para Web Push; el endpoint es único y pasa al usuario que lo suscribe.                      | `notificaciones.suscripciones_push` |
| `PreferenciaPush` | Interruptor del push por módulo (Aulas, Designaciones, Tareas) y usuario (el aviso in-app no se silencia).                                | `notificaciones.preferencias_push`  |

## API pública (contract)

| Interfaz              | Métodos                                                                    | Consumido por                                              |
| --------------------- | -------------------------------------------------------------------------- | ---------------------------------------------------------- |
| `INotificador`        | `NotificarAsync(AvisoNuevo, IReadOnlyCollection<DestinoNotificacion>, ct)` | `Modules.Aulas`, `Modules.Designaciones`, `Modules.Tareas` |
| `DestinoNotificacion` | `Usuario`, `Persona`, `RolEnAmbito(rol, materiaId?, carreraId?)`           | los emisores, para decir _a quién_                         |

`INotificador` es _best effort_ por contrato: nunca lanza hacia el flujo de negocio, se invoca **después del commit** y no reutiliza el `DbContext` ni la transacción de quien lo llama (abre su propio scope). Excluye al actor, deduplica, descarta usuarios inactivos y omite (registrando) a las personas sin usuario.

## Endpoints HTTP

Detalle en [api-contracts.md](../api-contracts.md#notificaciones-apinotificaciones).

| Método        | Path                                     | Rol       | Descripción                                         |
| ------------- | ---------------------------------------- | --------- | --------------------------------------------------- |
| GET           | `/api/notificaciones/ping`               | (anónimo) | Health check                                        |
| GET           | `/api/notificaciones/no-leidas/cantidad` | sesión    | Cantidad de no leídas del usuario y perfil activo   |
| GET           | `/api/notificaciones`                    | sesión    | Lista paginada (`NO_LEIDAS` / `LEIDAS`)             |
| PATCH         | `/api/notificaciones/{id}/leida`         | sesión    | Marca una como leída (idempotente)                  |
| PATCH         | `/api/notificaciones/leidas`             | sesión    | Marca varias (`ids` o `todas` + `hasta`)            |
| GET           | `/api/notificaciones/push/clave-publica` | sesión    | Clave VAPID pública (404 si push está apagado)      |
| POST / DELETE | `/api/notificaciones/push/suscripciones` | sesión    | Alta (upsert que reasigna) y baja de un dispositivo |
| GET / PUT     | `/api/notificaciones/push/preferencias`  | sesión    | Interruptores de push por módulo                    |

## Quién avisa a quién

Todos los avisos salen después del commit de la acción; el actor nunca recibe el suyo.

| Evento                                                                    | Emisor        | Destinatarios                                                                                                             | `rol_destino`              |
| ------------------------------------------------------------------------- | ------------- | ------------------------------------------------------------------------------------------------------------------------- | -------------------------- |
| Aula asignada (RF-022: lleva el número)                                   | Aulas         | Docente dueño                                                                                                             | `rol_solicitante` (o NULL) |
| Aula actualizada (sólo si cambió)                                         | Aulas         | Docente dueño                                                                                                             | `rol_solicitante` (o NULL) |
| Solicitud rechazada (con motivo)                                          | Aulas         | Docente dueño                                                                                                             | `rol_solicitante` (o NULL) |
| Reserva cancelada (con motivo y aula liberada)                            | Aulas         | Docente dueño                                                                                                             | `rol_solicitante` (o NULL) |
| Solicitud cancelada por el docente                                        | Aulas         | Usuarios `administrativo` y usuarios `secretaria`                                                                         | el rol del destinatario    |
| Pedido para revisar (aceptar → Secretaría o Decanato; `en_lote` no avisa) | Designaciones | Usuarios con el rol de la etapa a la que pasó                                                                             | ese rol                    |
| Pedido rechazado                                                          | Designaciones | Jefes de Cátedra de la materia del pedido                                                                                 | `jefe_catedra`             |
| Pedido devuelto                                                           | Designaciones | Rol propietario de la etapa de origen en el ámbito del pedido: Jefe (materia), Coordinador (carrera), Secretaría (global) | ese rol                    |
| Tarea asignada / reasignada                                               | Tareas        | Nuevo responsable                                                                                                         | NULL                       |
| Cambio de estado / comentario en una tarea                                | Tareas        | Responsable y creador                                                                                                     | NULL                       |

Un aviso con `rol_destino` sólo se ve bajo ese rol; con NULL se ve bajo cualquiera (las tareas pertenecen al usuario, no a uno de sus perfiles).

## Reglas de negocio (BR-\*)

Sin reglas de normativa institucional. Es comportamiento de producto, especificado en `openspec/specs/notificaciones-internas` y `openspec/specs/campana-y-panel-notificaciones` una vez archivado el change `notificaciones-in-app`.

## Dependencias

- **Hacia adentro**: `ArsDocendi.Shared` (`IConsultasIdentity`, `ICurrentUser`, `Permisos` no se usa), `ArsDocendi.Migraciones`. Ningún otro módulo.
- **Hacia afuera**: Aulas, Designaciones y Tareas referencian `Modules.Notificaciones.Contracts`. El Host referencia `Modules.Notificaciones`.
- **Externas**: servicios de Web Push de Google, Mozilla, Apple y Microsoft (sólo hosts de la lista permitida), a través de `IEnviadorPush` y el paquete `WebPush`. Configuración VAPID en [operations/web-push.md](../../operations/web-push.md).

## Decisiones registradas

- **Push por cola en memoria, sin outbox** — `Notificador` sólo encola; `ProcesadorPush` (`BackgroundService`) envía con timeout por envío, borra suscripciones 404/410 y registra únicamente código y motivo del servicio. Se aceptan trabajos perdidos si el proceso cae: el aviso in-app es el registro durable. El texto viaja cifrado (RFC 8291) y jamás se loguea.
- **Sin outbox** — el equipo aceptó que un aviso fallido se pierda (se registra sin datos personales) a cambio de no acoplar la acción de negocio a la entrega.
- **Fan-out al emitir, una fila por usuario** — varios Jefes de una materia leen y marcan por separado; la consulta de lectura es un índice por usuario, sin joins con roles.
- **Texto congelado en el emisor** — Notificaciones no lee datos ajenos y el aviso no cambia si el pedido cambia después.
- **Sin `audit.attach`** — ver [data-model.md](../data-model.md#notificaciones-schema-notificaciones).
- **Optimismo al marcar como leída** — la regla "sin optimismo para transiciones" protege hechos de negocio que otros usan; leer un aviso sólo cambia el estado de quien lo lee y se revierte con un toast si falla (ver `openspec/changes/notificaciones-in-app/design.md`, decisión 7).
- **El número de tarea se muestra como `N° {numero}`**, igual que el detalle de la tarea.
