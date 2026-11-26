# Design

## Context

Ver proposal.md (Why). Estado actual relevante:

- `MaquinaEstadosPedido`: `en_revision_decanato` → `en_lote` al aceptar. `en_lote` está en `EstadosPedido.Terminales`.
- `ServicioPedidos` llama a `MaterializadorDesignaciones.MaterializarAsync` dentro de la transacción de la aceptación de Decanato. Alta abre, Baja cierra y Cambio cierra y abre, o actualiza intradía. La fecha efectiva es `FechaOperativa.Hoy`.
- La persona de un Alta nueva se crea al **cargar el pedido**, en `identity.personas` (documento, nombre y apellido), mediante `IAdministracionIdentity.CrearPersonaSinCuentaAsync`. `pedidos.persona_id` la referencia desde el borrador.
- Un rol se asigna en `identity.user_roles`, que cuelga de `identity.users`. `users.upn` (el mail) es NOT NULL, así que **una persona sin mail no puede tener roles**. El rol `docente` exige `materia_id` y `carrera_id` (trigger `enforce_role_scope`).
- La administración unificada de personas (`ArsDocendi.Host/Administracion/ServicioAdministracionPersonas`) guarda en una sola transacción la persona, la cuenta, las membresías y las designaciones vigentes. Lo hace con `IUnidadDeTrabajoAdministracion` (un `TransactionScope` ambiente) y lee y escribe designaciones por `Modules.Designaciones.Contracts.Administracion`.
- BR-designaciones-001: un pedido por docente y período. En la práctica, en la conciliación cada docente tiene **un solo** movimiento. La agrupación por docente da contexto (sus otras materias), no junta varios pedidos.
- Integridad: cada evento fuera de borrador se sella. `CanonicoSellos.HashEstado` fotografía estado, cargo, dedicación, horas y snapshot del pedido.
- El lote (`ServicioLoteDesignaciones`) lee los pedidos `en_lote` del período y las designaciones vigentes. Para un Cambio asume que ya está materializado.

## Goals / Non-Goals

**Goals:**

- Sacar la materialización de la aceptación de Decanato y hacerla en la sincronización, junto con el rol Docente de la persona.
- Que el Excel del lote siga produciendo lo mismo antes y después de sincronizar.
- Permisos de catálogo, sin códigos de rol en el código nuevo.

**Non-Goals:**

- No cambian la edición de borradores y devueltos (BR-designaciones-008), la exportación ni sus roles habilitados.
- No hay carga del resultado de la rectificación por archivo: la conciliación es manual.
- No se notifica al Jefe de Cátedra. Ver Open Questions.
- No se deshace una sincronización.

## Decisions

### D1. Estados: `en_lote` pendiente, `aprobado` y `no_aprobado`

```
en_revision_decanato ──aceptar──► en_lote ──sincronizar──► aprobado   (terminal)
                                    │  ▲
                          no_aprobar│  │editar (designaciones.editar)
                                    ▼  │
                                 no_aprobado
```

- `Terminales` pasa a `{aprobado, rechazado, cancelado}`.
- `en_lote` y `no_aprobado` sólo admiten las acciones de la conciliación (`sincronizar`, `no_aprobar`) y la edición con permiso.
- `NoOcupanCupo` queda igual (`rechazado`, `cancelado`), así que `no_aprobado` sigue ocupando el cupo, y el índice `pedidos_uno_por_docente_periodo` no cambia.
- En la UI, `no_aprobado` se muestra como "Rechazado".
- **Alternativa descartada:** reusar `rechazado`. Es terminal [BR-designaciones-011] y saldría del cupo; además mezclaría un rechazo del circuito con uno de la rectificación.

### D2. La materialización se mueve a la sincronización

- `ServicioPedidos` ya no llama al materializador cuando el resultado es `en_lote`.
- Un servicio nuevo, `ServicioConciliacion`, sincroniza una lista `[{pedidoId, version}]` en **una** transacción:
  1. Bloquea por persona y materia (`BloquearAsync`, como el circuito).
  2. Valida estado `en_lote`, versión (`xmin`), ámbito y permiso.
  3. Materializa con el mismo `MaterializadorDesignaciones`.
  4. Asigna o retira el rol (D4).
  5. Pasa el pedido a `aprobado`, registra el evento `sincronizar` y lo sella.
- Si cualquier pedido falla, no se aplica ninguno. El error dice el número de pedido.
- La operación usa la tabla de idempotencia existente (`idempotencia_comandos`) con la cabecera `Idempotency-Key`, como las acciones del circuito.
- La fecha efectiva es la de la sincronización (`FechaOperativa.Hoy`).
- **Alternativa descartada:** sincronización parcial. Con muchos docentes, el operador no sabría qué quedó aplicado.

### D3. Pedidos `en_lote` heredados: columna `materializado_en`

- Se agrega `pedidos.materializado_en timestamptz NULL`. Una migración lo completa con `now()` en los pedidos `en_lote` que ya existen, que se materializaron al aceptar.
- La sincronización:
  - Si `materializado_en` es NULL, materializa y la completa.
  - Si no, sólo transiciona.
- `no_aprobar` sobre un heredado se deniega: revertir una designación ya escrita sale del alcance.
- La columna **no** entra en `CanonicoSellos.HashEstado`. Es una marca operativa, y así los sellos existentes se siguen verificando sin versionar la forma canónica.
- **Alternativa descartada:** detectar lo materializado por `designaciones.origen_pedido_id`. No sirve para Baja, porque cerrar una vigencia no deja el pedido en la fila.

### D4. Persona, rol Docente y designación al sincronizar

Qué escribe cada novedad:

| Novedad | `designaciones.designaciones`                               | `identity.user_roles` (si la persona tiene cuenta)                |
| ------- | ----------------------------------------------------------- | ----------------------------------------------------------------- |
| Alta    | abre con cargo, dedicación, horas, investigación y externas | inserta `docente` (materia, carrera) si no existe vigente         |
| Cambio  | cierra y abre, o actualiza intradía                         | sin cambios: la materia y la carrera no cambian                   |
| Baja    | cierra `vigente_hasta`                                      | soft-delete (`deleted_at`) del `docente` de esa materia y carrera |

- **La persona no se crea en la sincronización: ya existe desde que se cargó el pedido.** La sincronización no escribe `identity.personas` ni `identity.users`.
- `IAdministracionIdentity` (Shared, la frontera de escritura de identidad para módulos) suma:
  - `AsignarRolDocenteAsync(personaId, materiaId, carreraId, otorgadoPor)`
  - `RetirarRolDocenteAsync(personaId, materiaId, carreraId)`
- Si la persona no tiene `users`, los dos métodos no hacen nada.
- La sincronización corre dentro de `IUnidadDeTrabajoAdministracion`, para que `DesignacionesDbContext` e `IdentityDbContext` confirmen juntos. Es el mismo patrón que la administración de personas.
- **Persona sin mail:** el rol se completa cuando se crea la cuenta.
  - El editor de Personas, al abrir una persona sin cuenta con designaciones vigentes, **precarga** una membresía `docente` por cada una (materia, carrera), con la leyenda "Se asigna al crear la cuenta, según sus designaciones".
  - El operador puede quitarla antes de guardar, y `ServicioAdministracionPersonas.GuardarAsync` **respeta la lista enviada**: no agrega roles por su cuenta.
  - La precarga no cambia la API: el editor deriva las membresías sugeridas de las designaciones vigentes que la consulta de la persona en administración ya devuelve.
  - La fuente de verdad es la designación vigente, así que no hace falta una tabla de asignaciones pendientes.
  - **Alternativa descartada:** que el backend agregue el rol faltante al crear la cuenta. Sería más robusto, pero le impediría al operador quitarlo, y se decidió dejarlo en sus manos.
- **Alternativas descartadas:**
  - Una tabla `identity.asignaciones_pendientes`: duplica la designación y puede quedar desfasada si después se da de baja.
  - Permitir `users.upn` NULL: rompe la vinculación del primer ingreso por UPN y la unicidad.
- Es una excepción documentada a "sólo la administración escribe roles". Pasa por la misma frontera que ya crea personas sin cuenta, y se registra en `data-model.md`.

### D5. Rechazo de conciliación y edición con permiso

- `POST /pedidos/{id}/no-aprobar {motivo, version}` exige `designaciones.conciliar`.
  - Pasa de `en_lote` a `no_aprobado` y registra el evento `no_aprobar` con el motivo en `comentario`, sellado.
  - El motivo vive en el historial, no en una columna: así no cambia la forma canónica del sello.
- Hoy `ActorContexto` sólo lleva roles y ámbitos. Suma `Permisos`, el conjunto efectivo que `ResolutorActor` lee de `IConsultasIdentity` con la identidad persistida, y `TienePermiso(code)`. El ámbito sigue resolviéndose como hoy (`AlcanzaAmbito`).
- `MaquinaEstadosPedido.PuedeEditar` suma: estado `en_lote` o `no_aprobado`, `actor.TienePermiso(designaciones.editar)` y ámbito.
  - Se usan las mismas validaciones de `ValidarDatosAsync` y el snapshot no se recalcula.
  - Editar un `no_aprobado` lo pasa a `en_lote` en la misma transacción (evento `editar`).
- Un Cambio o una Baja editados siguen exigiendo una designación vigente en la materia (`ValidarDesignacionVigenteAsync`). Como la materialización se difiere, esa vigente es la de antes del pedido.

### D6. Consulta de conciliación

`GET /api/designaciones/conciliacion?periodoId=` exige `designaciones.conciliar` y filtra por ámbito [BR-designaciones-009]. Devuelve, por persona:

- documento, CUIL y si tiene cuenta;
- sus designaciones vigentes de **todas** sus materias (materia, carrera, cargo, dedicación y tres horas);
- sus pedidos `en_lote`, `no_aprobado` y `aprobado` del período, con valores solicitados, justificación, tipo de baja, versión, el último motivo de `no_aprobar` y fecha y autor de la sincronización.

La vista previa se calcula en el cliente con esos datos. El backend vuelve a validar todo al sincronizar.

### D7. Lote

`ServicioLoteDesignaciones`:

- Los pedidos del lote son `estado IN (en_lote, aprobado)`.
- En `PROPUESTA COMPLETA`, una vigente con un Cambio `en_lote` de la misma persona y materia proyecta los valores del pedido. Una Baja `en_lote` excluye la vigente de las continuidades, como ya hace con `clavesBaja`.
- Alta `en_lote` sin designación ya se maneja (`altasSinDesignacion`). Un Alta `aprobado` tiene designación con origen.
- La lectura sigue siendo consistente con una sincronización concurrente: un pedido se lee con un solo estado.

### D8. Frontend

- Página `ConciliacionPage` en `features/designaciones`, ruta `conciliacion`, con `RequirePermission permission="designaciones.conciliar"` y un ítem en la navegación por permiso.
- Usa `@ars-docendi/ui` (`Tabs`, `Table`, `Checkbox`, `Modal`, `InlineAlert`, `Badge`) como el prototipo validado. La edición reutiliza `PedidoFormPage` (`/designaciones/pedidos/:id/editar`). La selección de docentes aceptados es estado local.
- `EstadoPedido`, los badges, el tablero (`tableroRevisionModelo`) y el detalle suman `aprobado` y `no_aprobado`. La vista simplificada cambia `en_lote` de "aprobado" a "en proceso".

### D9. Base de datos (migraciones incrementales con `scaffold.mjs`)

- `designaciones`:
  - Reemplaza los CHECKs `pedidos_estado_valido`, `pedido_historial_accion_valida` (+ `sincronizar`, `no_aprobar`) y `pedido_historial_etapa_valida` (+ `aprobado`, `no_aprobado`).
  - Agrega `pedidos.materializado_en` y su backfill.
  - Revisa la regla de completitud de `20261013173721_CompletitudCircuitoDesignaciones`, que enumera estados, para incluir `no_aprobado` y `aprobado`.
  - Actualiza los comentarios de columna del asistente sobre estados.
- `identity`: siembra `designaciones.conciliar` y `designaciones.editar`, y los concede a los roles `secretaria` y `administrativo` (`ON CONFLICT DO NOTHING`).
- No se toca ninguna migración publicada.

## Risks / Trade-offs

- [Riesgo] Cambiar la máquina de estados rompe expectativas de tests y del front que asumen `en_lote` terminal y materializado. → Mitigación: actualizar los tests de aprobación y persistencia en el mismo cambio. La suite de lote cubre `en_lote` y `aprobado`.
- [Riesgo] Una persona con un Alta `no_aprobado` que nunca se corrige queda en `identity.personas` sin designación. → Se acepta: ya pasa hoy con un Alta rechazada en el circuito, y la persona puede reutilizarse.
- [Riesgo] La fecha de vigencia pasa a ser la de la sincronización y no la de aceptación de Decanato. → Es el comportamiento buscado: la designación existe recién cuando se aprueba.
- [Riesgo] La transacción ambiente que cruza dos contextos, con muchos pedidos, puede ser larga. → Se procesa por persona en el orden de bloqueo estable del circuito, con timeout de 30 s como la administración. Para lotes grandes, el cliente sincroniza lo aceptado y el operador puede partirlo por filtros.
- [Riesgo] Un rol personalizado con `designaciones.conciliar` pero sin un ámbito que `AlcanzaAmbito` reconozca no ve ningún pedido. → Se documenta en el artículo de ayuda de roles: el permiso habilita la acción y el ámbito sigue viniendo de la asignación.
- [Trade-off] La aceptación por docente es sólo de UI: la API recibe pedidos. Con BR-001 equivale a uno por docente.

## Migration Plan

1. Desplegar las migraciones (CHECKs, columna con backfill y permisos) y el backend en el mismo release. Las migraciones sólo amplían.
2. Verificar en staging: un `en_lote` heredado se sincroniza sin duplicar designaciones, y la integridad del período sigue verificando.
3. **Rollback:** antes de volver a la versión anterior, resolver los pedidos `aprobado` y `no_aprobado` con una migración incremental explícita, porque la versión anterior no los conoce. Los CHECKs ampliados y la columna pueden quedar.

## Open Questions

- ¿Avisar al Jefe de Cátedra (notificación in-app) cuando su pedido queda aprobado o rechazado en la conciliación? No cambia el modelo: es un consumidor más de los eventos.
