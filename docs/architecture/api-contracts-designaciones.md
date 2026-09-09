# Contratos API — Designaciones

Complementa [api-contracts.md](./api-contracts.md). El backend deriva identidad, roles y ámbitos de la solicitud autenticada; ningún DTO acepta `ActorContexto` como autoridad.

## DTOs

```text
PeriodoDto       = { id, nombre, cargaDesde, cargaHasta, impactoDesde, impactoHasta, activo }
GuardarPeriodoDto= { nombre, cargaDesde, cargaHasta, impactoDesde, impactoHasta, activo }
PersonaPedidoDto = { id, nombre, apellido, documento, legajo? }
GuardarPersonaPedidoDto = { documento, nombre, apellido }
PedidoDto        = { id, numero, periodo{id,nombre}, persona, materia{id,codigo,nombre,carrera},
                     novedad, estado, prioritario, cargoSolicitado?, dedicacionSolicitada?, horas?,
                     horasInvestigacion?, horasExternas?, justificacion?, tipoBaja?,
                     tipoBajaDetalle?, etapaRetorno?, propietarioActual?, snapshot?,
                     adjuntos[], historial[], accionesPermitidas[] }
GuardarPedidoDto = { periodoId, personaId?, materiaId, novedad, cargoSolicitadoId?,
                     dedicacionSolicitadaId?, horas?, horasInvestigacion?, horasExternas?,
                     justificacion?, tipoBaja?, tipoBajaDetalle?, adjuntos[], persona? }
AccionPedidoDto  = { comentario? }
CatalogosDto     = { periodoActivo?, periodos[], personas[], materias[], cargos[],
                     dedicaciones[], tiposBaja[], novedades[] }
```

Los valores cerrados pueden viajar en `CatalogosDto` para un contrato uniforme, aunque permanezcan reglas de dominio y no filas configurables.

En `Alta`, `persona` contiene `documento`, `nombre` y `apellido`; `personaId` se
omite y el backend crea la persona canónica sin crear una cuenta en `identity.users`.
También puede recibirse un `personaId` ya resuelto por compatibilidad. En `Baja` y
`Cambio de cargo o dedicación`, `personaId` es obligatorio y `persona` está prohibido.
`materiaId` es siempre explícito: no se resuelve por nombre ni se acepta una materia
fuera del ámbito persistido del actor. El primer login existente vincula la cuenta
por documento a la persona ya creada.

## Períodos y catálogos

| Método | Ruta                               | Permiso                                         | Entrada / salida                       |
| ------ | ---------------------------------- | ----------------------------------------------- | -------------------------------------- |
| GET    | `/api/designaciones/periodos`      | `periodos.administrar`                          | `PeriodoDto[]`                         |
| POST   | `/api/designaciones/periodos`      | `periodos.administrar`                          | `GuardarPeriodoDto` → `201 PeriodoDto` |
| PUT    | `/api/designaciones/periodos/{id}` | `periodos.administrar`                          | `GuardarPeriodoDto` → `PeriodoDto`     |
| DELETE | `/api/designaciones/periodos/{id}` | `periodos.administrar`                          | `204`                                  |
| GET    | `/api/designaciones/catalogos`     | `designaciones.ver` o `designaciones.gestionar` | `CatalogosDto` acotado al actor        |

## Exportación del lote

| Método | Ruta                                         | Permiso y ámbito                                                          | Salida |
| ------ | -------------------------------------------- | ------------------------------------------------------------------------- | ------ |
| GET    | `/api/designaciones/periodos/{id}/lote.xlsx` | `designaciones.ver` + Secretaría, Decanato o Administración departamental | XLSX   |

La ruta sólo acepta el período activo indicado por `{id}`. El archivo fijo
`lote-designaciones.xlsx` contiene `Pedidos finalizados` (pedidos `en_lote` del
período) y `Designaciones resultantes` (todas las designaciones vigentes,
incluidas las continuidades sin pedido aprobado). No recibe filtros: la
exportación siempre representa el lote completo del período. Devuelve `401` sin
autenticación, `403` fuera del ámbito departamental, `404` si el período no
existe y `409` si dejó de estar activo.

## Pedidos

| Método | Ruta                                           | Permiso                   | Entrada / salida                     |
| ------ | ---------------------------------------------- | ------------------------- | ------------------------------------ |
| GET    | `/api/designaciones/pedidos?periodoId={uuid}`  | `designaciones.ver`       | `PedidoDto[]` filtrado por actor     |
| GET    | `/api/designaciones/pedidos/{id}`              | `designaciones.ver`       | `PedidoDto` si es visible            |
| POST   | `/api/designaciones/pedidos`                   | `designaciones.gestionar` | `GuardarPedidoDto` → `201 PedidoDto` |
| PUT    | `/api/designaciones/pedidos/{id}`              | `designaciones.ver`       | `GuardarPedidoDto` → `PedidoDto`     |
| DELETE | `/api/designaciones/pedidos/{id}`              | `designaciones.gestionar` | `204`, sólo borrador propio          |
| POST   | `/api/designaciones/pedidos/{id}/enviar`       | `designaciones.gestionar` | `PedidoDto`                          |
| POST   | `/api/designaciones/pedidos/{id}/reenviar`     | `designaciones.ver`       | `PedidoDto`                          |
| POST   | `/api/designaciones/pedidos/{id}/aceptar`      | permiso de etapa          | `AccionPedidoDto` → `PedidoDto`      |
| POST   | `/api/designaciones/pedidos/{id}/rechazar`     | permiso de etapa          | comentario obligatorio → `PedidoDto` |
| POST   | `/api/designaciones/pedidos/{id}/devolver`     | permiso de etapa          | comentario obligatorio → `PedidoDto` |
| POST   | `/api/designaciones/pedidos/{id}/priorizar`    | `designaciones.ver`       | comentario obligatorio → `PedidoDto` |
| POST   | `/api/designaciones/pedidos/{id}/despriorizar` | `designaciones.ver`       | comentario opcional → `PedidoDto`    |

Permiso de etapa significa `designaciones.aprobar_coordinacion`, `designaciones.aprobar_secretaria` o `designaciones.aprobar_decanato`; la política también deja pasar a Administración para rechazar o devolver, y el dominio le impide aceptar. En edición, reenvío y prioridad, `designaciones.ver` abre el endpoint y la máquina de estados valida rol, estado y ámbito.

## Idempotencia

`Idempotency-Key: <uuid>` es obligatorio en enviar, reenviar, aceptar, rechazar, devolver, priorizar y despriorizar. La clave se identifica junto con actor, ruta y pedido durante 24 horas:

- repetir exactamente la solicitud retorna el mismo status y body;
- reutilizar la clave con otro payload o recurso retorna `409 idempotency-key-reused`;
- solicitudes concurrentes con la misma clave producen una sola transición e historial.

Crear, editar y eliminar usan constraints y control de concurrencia, pero no el replay de 24 horas.

## Códigos de error adicionales

| Código                       | HTTP | Uso                                   |
| ---------------------------- | ---- | ------------------------------------- |
| `pedido-transition-invalid`  | 422  | acción no admitida por estado o actor |
| `pedido-scope-forbidden`     | 403  | pedido fuera del ámbito persistido    |
| `pedido-duplicate-live`      | 409  | pedido vivo para persona/período      |
| `periodo-active-conflict`    | 409  | segundo período activo                |
| `periodo-in-use`             | 409  | eliminación con pedidos asociados     |
| `idempotency-key-required`   | 400  | falta header en una transición        |
| `idempotency-key-reused`     | 409  | clave reutilizada para otra operación |
| `identity-document-conflict` | 409  | documento ya asociado a otra persona  |

El catálogo `dedicaciones` devuelve `{ id, codigo, nombre, orden }` para las seis categorías activas 1–6. Las mutaciones envían `dedicacionSolicitadaId` (UUID); la lectura conserva `dedicacionSolicitada` como nombre histórico y agrega el ID opcional.
