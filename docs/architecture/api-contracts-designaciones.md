# Contratos API — Designaciones

Complementa [api-contracts.md](./api-contracts.md). El backend deriva identidad, roles y ámbitos de la solicitud autenticada; ningún DTO acepta `ActorContexto` como autoridad.

## DTOs

```text
PeriodoDto       = { id, nombre, cargaDesde, cargaHasta, impactoDesde, impactoHasta, activo }
GuardarPeriodoDto= { nombre, cargaDesde, cargaHasta, impactoDesde, impactoHasta, activo }
PersonaPedidoDto = { id, nombre, apellido, documento, legajo? }
PedidoDto        = { id, numero, periodo{id,nombre}, persona, materia{id,codigo,nombre,carrera},
                     novedad, estado, prioritario, cargoSolicitado?, dedicacionSolicitada?, horas?,
                     horasInvestigacion?, horasExternas?, justificacion?, tipoBaja?,
                     tipoBajaDetalle?, etapaRetorno?, propietarioActual?, snapshot?,
                     adjuntos[], historial[], accionesPermitidas[] }
GuardarPedidoDto = { periodoId, personaId, materiaId, novedad, cargoSolicitadoId?,
                     dedicacionSolicitada?, horas?, horasInvestigacion?, horasExternas?,
                     justificacion?, tipoBaja?, tipoBajaDetalle?, adjuntos[] }
AccionPedidoDto  = { comentario? }
CatalogosDto     = { periodoActivo?, periodos[], personas[], materias[], cargos[],
                     dedicaciones[], tiposBaja[], novedades[] }
```

Los valores cerrados pueden viajar en `CatalogosDto` para un contrato uniforme, aunque permanezcan reglas de dominio y no filas configurables.

## Períodos y catálogos

| Método | Ruta                               | Permiso                                         | Entrada / salida                       |
| ------ | ---------------------------------- | ----------------------------------------------- | -------------------------------------- |
| GET    | `/api/designaciones/periodos`      | `periodos.administrar`                          | `PeriodoDto[]`                         |
| POST   | `/api/designaciones/periodos`      | `periodos.administrar`                          | `GuardarPeriodoDto` → `201 PeriodoDto` |
| PUT    | `/api/designaciones/periodos/{id}` | `periodos.administrar`                          | `GuardarPeriodoDto` → `PeriodoDto`     |
| DELETE | `/api/designaciones/periodos/{id}` | `periodos.administrar`                          | `204`                                  |
| GET    | `/api/designaciones/catalogos`     | `designaciones.ver` o `designaciones.gestionar` | `CatalogosDto` acotado al actor        |

## Pedidos

| Método | Ruta                                           | Permiso                   | Entrada / salida                     |
| ------ | ---------------------------------------------- | ------------------------- | ------------------------------------ | -------------------------------- |
| GET    | `/api/designaciones/pedidos?vista=propios      | revision`                 | `designaciones.ver`                  | `PedidoDto[]` filtrado por actor |
| GET    | `/api/designaciones/pedidos/{id}`              | `designaciones.ver`       | `PedidoDto` si es visible            |
| POST   | `/api/designaciones/pedidos`                   | `designaciones.gestionar` | `GuardarPedidoDto` → `201 PedidoDto` |
| PUT    | `/api/designaciones/pedidos/{id}`              | `designaciones.gestionar` | `GuardarPedidoDto` → `PedidoDto`     |
| DELETE | `/api/designaciones/pedidos/{id}`              | `designaciones.gestionar` | `204`, sólo borrador propio          |
| POST   | `/api/designaciones/pedidos/{id}/enviar`       | `designaciones.gestionar` | `PedidoDto`                          |
| POST   | `/api/designaciones/pedidos/{id}/reenviar`     | `designaciones.gestionar` | `PedidoDto`                          |
| POST   | `/api/designaciones/pedidos/{id}/aceptar`      | permiso de etapa          | `AccionPedidoDto` → `PedidoDto`      |
| POST   | `/api/designaciones/pedidos/{id}/rechazar`     | permiso de etapa          | comentario obligatorio → `PedidoDto` |
| POST   | `/api/designaciones/pedidos/{id}/devolver`     | permiso de etapa          | comentario obligatorio → `PedidoDto` |
| POST   | `/api/designaciones/pedidos/{id}/priorizar`    | permiso de etapa          | comentario obligatorio → `PedidoDto` |
| POST   | `/api/designaciones/pedidos/{id}/despriorizar` | permiso de etapa          | comentario opcional → `PedidoDto`    |

Permiso de etapa significa `designaciones.aprobar_coordinacion`, `designaciones.aprobar_secretaria` o `designaciones.aprobar_decanato`. Administración conserva los alcances excepcionales definidos por las reglas vigentes, no un permiso implícito de aceptar.

## Idempotencia

`Idempotency-Key: <uuid>` es obligatorio en enviar, reenviar, aceptar, rechazar, devolver, priorizar y despriorizar. La clave se identifica junto con actor, ruta y pedido durante 24 horas:

- repetir exactamente la solicitud retorna el mismo status y body;
- reutilizar la clave con otro payload o recurso retorna `409 idempotency-key-reused`;
- solicitudes concurrentes con la misma clave producen una sola transición e historial.

Crear, editar y eliminar usan constraints y control de concurrencia, pero no el replay de 24 horas.

## Códigos de error adicionales

| Código                      | HTTP | Uso                                   |
| --------------------------- | ---- | ------------------------------------- |
| `pedido-transition-invalid` | 422  | acción no admitida por estado o actor |
| `pedido-scope-forbidden`    | 403  | pedido fuera del ámbito persistido    |
| `pedido-duplicate-live`     | 409  | pedido vivo para persona/período      |
| `periodo-active-conflict`   | 409  | segundo período activo                |
| `periodo-in-use`            | 409  | eliminación con pedidos asociados     |
| `idempotency-key-required`  | 400  | falta header en una transición        |
| `idempotency-key-reused`    | 409  | clave reutilizada para otra operación |
