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
                     tipoBajaDetalle?, etapaRetorno?, propietarioActual?, snapshot?, version,
                     adjuntos[], historial[], accionesPermitidas[], dedicacionSolicitadaId?,
                     motivoDevolucion?, enviadoEn? }
GuardarPedidoDto = { periodoId, personaId?, materiaId, carreraId, novedad, cargoSolicitadoId?,
                     dedicacionSolicitadaId?, horas?, horasInvestigacion?, horasExternas?,
                     justificacion?, tipoBaja?, tipoBajaDetalle?, adjuntos[], persona?, version? }
AccionPedidoDto  = { comentario? }
CatalogosDto     = { periodoActivo?, periodos[], personas[], materias[], cargos[],
                     dedicaciones[], tiposBaja[], novedades[] }
```

Los valores cerrados pueden viajar en `CatalogosDto` para un contrato uniforme, aunque permanezcan reglas de dominio y no filas configurables.

`PedidoDto.version` representa el `xmin` observado. La edición exige ese token en
`GuardarPedidoDto.version`; la creación no exige una versión de una fila inexistente.

En `Alta`, `persona` contiene `documento`, `nombre` y `apellido`; `personaId` se
omite y el backend crea la persona canónica sin crear una cuenta en `identity.users`.
También puede recibirse un `personaId` ya resuelto por compatibilidad. En `Baja` y
`Cambio de cargo o dedicación`, `personaId` es obligatorio y `persona` está prohibido.
`materiaId` y `carreraId` son siempre explícitos: no se resuelven por nombre ni se acepta una
materia o carrera fuera del ámbito persistido del actor. Una materia dictada en más de una
carrera exige elegir una. El primer login sólo vincula una cuenta Microsoft a un usuario
previamente creado por administración según la política vigente; nunca crea una cuenta
por haber registrado la persona en un Alta.

## Períodos y catálogos

| Método | Ruta                                          | Permiso                                         | Entrada / salida                                                                                     |
| ------ | --------------------------------------------- | ----------------------------------------------- | ---------------------------------------------------------------------------------------------------- |
| GET    | `/api/designaciones/periodos`                 | `periodos.administrar`                          | `PeriodoDto[]`                                                                                       |
| GET    | `/api/designaciones/periodos/{id}`            | `periodos.administrar`                          | `PeriodoDto`                                                                                         |
| POST   | `/api/designaciones/periodos`                 | `periodos.administrar`                          | `GuardarPeriodoDto` → `201 PeriodoDto`                                                               |
| PUT    | `/api/designaciones/periodos/{id}`            | `periodos.administrar`                          | `GuardarPeriodoDto` → `PeriodoDto`                                                                   |
| POST   | `/api/designaciones/periodos/{id}/activar`    | `periodos.administrar`                          | `PeriodoDto`                                                                                         |
| POST   | `/api/designaciones/periodos/{id}/desactivar` | `periodos.administrar`                          | `PeriodoDto`                                                                                         |
| DELETE | `/api/designaciones/periodos/{id}`            | `periodos.administrar`                          | `204`                                                                                                |
| GET    | `/api/designaciones/catalogos`                | `designaciones.ver` o `designaciones.gestionar` | `CatalogosDto` acotado al actor; `?pedidoId=` (edición) mantiene elegible a la persona de ese pedido |

## Exportación del lote

| Método | Ruta                                         | Permiso y ámbito                                                          | Salida |
| ------ | -------------------------------------------- | ------------------------------------------------------------------------- | ------ |
| GET    | `/api/designaciones/periodos/{id}/lote.xlsx` | `designaciones.ver` + Secretaría, Decanato o Administración departamental | XLSX   |

La ruta sólo acepta el período activo indicado por `{id}`. El archivo fijo
`lote-designaciones.xlsx` contiene exactamente tres hojas institucionales:

- `PROPUESTA COMPLETA`: una fila por persona y materia vigente, baja aprobada o
  Alta aprobada aún no materializada del período. Incluye CUIL,
  cargo/dedicación anterior y propuesta, observación, horas semanales y
  asignatura; departamento, dedicación externa y horas destinadas al ingreso
  quedan vacíos porque no tienen fuente persistida.
- `ALTAS`: sólo pedidos `en_lote` de novedad Alta del período activo, con CUIL,
  cargo/dedicación, fecha de nacimiento, teléfono y correo de Portal cuando
  exista.
- `BAJAS`: sólo pedidos `en_lote` de novedad Baja del período activo, con CUIL,
  cargo/dedicación anterior y el motivo compuesto por tipo, detalle y
  justificación disponibles.

Los encabezados usan el nombre del período activo y, en la primera hoja, el
período anterior por `ImpactoDesde`; si no existe, la referencia anterior queda
vacía. La exportación no recibe filtros: siempre representa el lote completo y
las continuidades vigentes del período. Devuelve `401` sin autenticación, `403`
fuera del ámbito departamental, `404` si el período no existe y `409` si dejó de
estar activo. Identity aporta persona, CUIL, fecha de nacimiento y teléfono;
`IPortalQueries` aporta únicamente `Contacto.Mail` para las altas.

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

### Seguimiento del pedido

Sin `designaciones.ver_seguimiento` (Jefe de Cátedra) la API arma una vista simplificada:
`estado` es `borrador`, `en_proceso` (cualquier etapa de revisión o devuelto a otro actor),
`devuelto` (sólo si volvió al rol del Jefe), `aprobado` (`en_lote`), `rechazado` o
`cancelado`; `historial` va vacío y `etapaRetorno` en `null`. `motivoDevolucion` trae el
comentario de la última devolución cuando el pedido está devuelto a quien consulta (con
seguimiento, siempre que esté devuelto). `accionesPermitidas` se calcula sobre el estado real. `enviadoEn` (fecha del primer envío) se
entrega a todos, con o sin seguimiento.

### Lectura de grilla versus detalle

`GET /pedidos` entrega todas las filas visibles sin paginación. `adjuntos` está vacío;
`historial` es un resumen de hasta cuatro eventos reales: primero, creación, primer
envío y último, sin duplicados, ordenados por fecha e ID y con `comentario = null`.
No es una historia completa ni la fuente para mostrar el detalle. Preserva nombres
de actores históricos, fecha de envío/creación, última actualización y acciones.
`GET /pedidos/{id}` y las respuestas de mutaciones conservan el historial completo,
snapshot y adjuntos autorizados. La selección de personas/actores se hace por IDs
visibles en lote, no mediante un directorio global.

Enviar obtiene snapshot bajo el mismo guard persona–materia que administración y
aprobación. Reenviar/aprobar no lo recalculan. Una observación administrativa de
vigencia obsoleta produce `409 concurrency-conflict`; el circuito puede esperar y
revalidar explícitamente el estado vigente antes de confirmar. No se repite una
intención obsoleta en automático.

## Idempotencia

`Idempotency-Key: <uuid>` es obligatorio en enviar, reenviar, aceptar, rechazar, devolver, priorizar y despriorizar. La clave se identifica junto con actor, ruta y pedido durante 24 horas:

- repetir exactamente la solicitud retorna el mismo status y body;
- reutilizar la clave con otro payload o recurso retorna `409 idempotency-key-reused`;
- solicitudes concurrentes con la misma clave producen una sola transición e historial.

Crear, editar y eliminar usan constraints y control de concurrencia, pero no el replay de 24 horas.

## Códigos de error adicionales

| Código                          | HTTP | Uso                                                                  |
| ------------------------------- | ---- | -------------------------------------------------------------------- |
| `pedido-transition-invalid`     | 422  | acción no admitida por estado o actor                                |
| `pedido-scope-forbidden`        | 403  | pedido fuera del ámbito persistido                                   |
| `pedido-duplicate-live`         | 409  | pedido vivo para persona/período                                     |
| `periodo-active-conflict`       | 409  | segundo período activo                                               |
| `periodo-in-use`                | 409  | eliminación con pedidos asociados                                    |
| `idempotency-key-required`      | 400  | falta header en una transición                                       |
| `idempotency-key-reused`        | 409  | clave reutilizada para otra operación                                |
| `identity-document-conflict`    | 409  | documento ya asociado a otra persona                                 |
| `identity-destination-conflict` | 409  | nuevo destino inexistente o actor desactivado durante la mutación    |
| `concurrency-conflict`          | 409  | pedido/vigencia cambió respecto de la observación usada para guardar |

El catálogo `dedicaciones` devuelve `{ id, codigo, nombre, orden }` para las seis categorías activas 1–6. Las mutaciones envían `dedicacionSolicitadaId` (UUID); la lectura conserva `dedicacionSolicitada` como nombre histórico y agrega el ID opcional.
