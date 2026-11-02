# Business rules: `designaciones`

## Contexto

- **Módulo / superficie:** `backend/src/Modules.Designaciones/` como autoridad y `frontend/src/features/designaciones/` como cliente de su API.
- **Owner / stakeholders:** Secretaría Académica del Departamento (define el circuito); Jefe de Cátedra (carga).
- **Specs OpenSpec relacionadas:** `openspec/specs/pedidos-designacion/` y `openspec/specs/aprobacion-pedidos-designacion/`.
- **Normativa de referencia:** Estatuto / régimen docente UNLaM (citas exactas **pendientes de confirmación con el cliente** para BR-001..004).

> **Alcance de este documento.** Reglas de la carga de pedidos por el Jefe de Cátedra que provienen de normativa institucional (BR-001..004, BR-018), cada una con su mapping a test.
>
> **Reglas de proceso.** Las reglas del circuito de aprobación (justificativo obligatorio, edición sólo en borrador/devuelto, ámbito por rol, rechazo terminal, revisor de la etapa, devolución de un nivel, Administración sin aprobar, prioridad) son decisiones de proceso, no normativa: viven en las specs [`aprobacion-pedidos-designacion`](../../openspec/specs/aprobacion-pedidos-designacion/spec.md) y [`pedidos-designacion`](../../openspec/specs/pedidos-designacion/spec.md). Los identificadores `BR-designaciones-005/008/009/011/013/014/015/017` se conservan como etiquetas estables en esas specs, en mensajes de error y en comentarios de código.

## Reglas

### BR-`designaciones`-001 Un pedido por docente por período

- **Statement:** No puede existir más de un pedido de designación para el mismo docente dentro de un mismo período abierto, **sin importar la cátedra**.
- **Rationale:** Evita designaciones duplicadas o contradictorias para un docente en un ciclo; el proyecto docente del período debe tener una sola entrada por docente.
- **Provenance:** `from_spec`
- **Fuente normativa:** Pendiente de confirmación con el cliente (estatuto / régimen docente UNLaM).
- **Ejemplos:** Si ya hay un pedido para el DNI 30.111.222 en el período abierto, crear otro para ese DNI se rechaza. Al **editar** el propio pedido, no se considera duplicado.
- **Roles afectados:** Jefe de Cátedra.
- **Implementación:** doble capa, con la base de datos como autoridad.

  ```sql
  -- database/designaciones/002_designaciones_pedidos.sql
  CREATE UNIQUE INDEX pedidos_uno_por_docente_periodo
      ON designaciones.pedidos (periodo_id, persona_id)
      WHERE estado <> ALL (ARRAY['rechazado', 'cancelado']);
  ```

  El índice es lo único que sobrevive a dos requests concurrentes. El backend valida antes para producir el mensaje del spec y traduce la violación del índice al mismo error. El conjunto de estados excluidos vive en C# en `EstadosPedido.NoOcupanCupo` y **debe** coincidir con el `WHERE` del índice: si se desalinean, backend y base discrepan sobre qué es un duplicado.

- **Consecuencia asumida:** combinada con "un pedido cubre exactamente una materia", esta regla implica que un docente recibe **a lo sumo un trámite por período en total**. Si dicta en dos cátedras, el primer Jefe de Cátedra que cargue bloquea al segundo. El mensaje de bloqueo **no debe exponer** la cátedra, el contenido ni el autor del pedido bloqueante — pertenece a una cátedra ajena al actor.

### BR-`designaciones`-002 Alta exige CV + DNI frente + DNI dorso

- **Statement:** Un pedido con novedad "Alta" debe adjuntar obligatoriamente CV, foto de DNI (frente) y foto de DNI (dorso) antes de poder guardarse.
- **Rationale:** El alta de un docente nuevo requiere la documentación mínima de identidad y antecedentes para la designación.
- **Provenance:** `from_spec`
- **Fuente normativa:** Pendiente de confirmación con el cliente (estatuto / régimen docente UNLaM).
- **Ejemplos:** Un "Alta" sin la foto de DNI dorso bloquea el guardado e indica el adjunto faltante. Con los tres adjuntos, no marca error de documentación.
- **Roles afectados:** Jefe de Cátedra.

### BR-`designaciones`-003 Baja exige justificativo

- **Statement:** Un pedido con novedad "Baja" debe indicar el tipo de baja y adjuntar obligatoriamente un justificativo antes de poder guardarse.
- **Rationale:** La baja de una designación debe quedar documentada con su justificación.
- **Provenance:** `from_spec`
- **Fuente normativa:** Pendiente de confirmación con el cliente (estatuto / régimen docente UNLaM).
- **Ejemplos:** Una "Baja" sin tipo o sin adjunto justificativo bloquea el guardado. Con ambos cargados, no marca error.
- **Roles afectados:** Jefe de Cátedra.

### BR-`designaciones`-004 Cambio de cargo o dedicación exige justificación

- **Statement:** Un pedido con novedad "Cambio de cargo o dedicación" debe incluir una justificación no vacía antes de poder guardarse.
- **Rationale:** Todo cambio de cargo/dedicación debe estar fundamentado para su evaluación por la cadena de aprobación.
- **Provenance:** `from_spec`
- **Fuente normativa:** Pendiente de confirmación con el cliente (estatuto / régimen docente UNLaM).
- **Ejemplos:** Un "Cambio" con justificación vacía (o solo espacios) bloquea el guardado. Con una justificación cargada, no marca ese error.
- **Roles afectados:** Jefe de Cátedra.

### BR-`designaciones`-018 Baja y Cambio exigen que el docente ya tenga legajo

- **Statement:** Un pedido con novedad "Baja" o "Cambio de cargo o dedicación" debe referenciar un docente con legajo asignado antes de poder guardarse. "Alta" no exige legajo: el docente todavía no existe en el sistema.
- **Rationale:** Baja y Cambio operan sobre una designación **ya existente** — el docente, por definición, ya está dado de alta en el sistema y por lo tanto ya tiene un legajo institucional asignado. Un Alta, en cambio, crea un docente nuevo cuyo legajo lo asigna el sistema/RRHH después, no el Jefe de Cátedra en el form.
- **Provenance:** `from_spec`
- **Fuente normativa:** Pendiente de confirmación con el cliente (estatuto / régimen docente UNLaM).
- **Ejemplos:** Un "Cambio" o una "Baja" sobre un docente sin legajo bloquea el guardado e indica que el legajo es obligatorio. Un "Alta" sin legajo no marca ese error.
- **Roles afectados:** Jefe de Cátedra.
- **Implementación:** `identity.personas.legajo` es **nullable** precisamente para admitir el caso del Alta — un docente que todavía no existe en el sistema y cuyo legajo asigna RRHH después. La obligatoriedad en Baja y Cambio la valida el backend, no la base: es una regla dependiente de la novedad del pedido, no del estado de la persona.

## Mapping a tests

| Rule ID              | Test file(s)                                                                                                                                                                                                                       | Tipo                          | Notas                                 |
| -------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------- | ------------------------------------- |
| BR-designaciones-001 | `frontend/src/features/designaciones/pedidoValidacion.test.ts`; `backend/tests/ArsDocendi.IntegrationTests/Designaciones/DesignacionesPersistenciaTests.cs` → rechazo entre cátedras, carrera concurrente y reintento tras rechazo | unit + integración PostgreSQL | Cita normativa pendiente con cliente. |
| BR-designaciones-002 | `frontend/src/features/designaciones/pedidoValidacion.test.ts`; `backend/tests/ArsDocendi.IntegrationTests/Designaciones/PedidosApiTests.cs`                                                                                       | unit + integración PostgreSQL | Cita normativa pendiente con cliente. |
| BR-designaciones-003 | `frontend/src/features/designaciones/pedidoValidacion.test.ts`; `backend/tests/ArsDocendi.IntegrationTests/Designaciones/PedidosApiTests.cs`                                                                                       | unit + integración PostgreSQL | Cita normativa pendiente con cliente. |
| BR-designaciones-004 | `frontend/src/features/designaciones/pedidoValidacion.test.ts`; `backend/tests/ArsDocendi.IntegrationTests/Designaciones/PedidosApiTests.cs`                                                                                       | unit + integración PostgreSQL | Cita normativa pendiente con cliente. |
| BR-designaciones-018 | `frontend/src/features/designaciones/pedidoValidacion.test.ts`; `backend/tests/ArsDocendi.IntegrationTests/Designaciones/PedidosApiTests.cs`                                                                                       | unit + integración PostgreSQL | Cita normativa pendiente con cliente. |

Todo BR-\* debe tener al menos un test verificando la regla.

### Cobertura técnica post-auditoría (no nuevas BR normativas)

La política intradía y los guards de concurrencia/lectura son decisiones técnicas
aprobadas del change `corregir-integridad-y-rendimiento-post-auditoria`; no agregan
normativa, plazos o autoridad de aprobación. Conservan las fuentes pendientes
anteriores y refuerzan su ejecución:

| Cobertura                                                                                    | Tests persistentes                                                                   | Relación con reglas existentes                                                          |
| -------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------- |
| Administración vs envío/aprobación, relectura protegida y conflicto de observación obsoleta  | `CoordinacionAdministracionTests.cs`, `ConcurrenciaVigenciasTests.cs`                | circuito de aprobación; estado, snapshot y eventos no parciales                         |
| Actor desactivado tras prevalidación, nueva persona inexistente y transacción local/ambiente | `DestinosLogicosDesignacionesTests.cs`, `DestinosPedidosTests.cs`                    | ámbito del rol; referencia canónica/elegibilidad actual, sin reemplazar autor histórico |
| Intradía: mismo ID/inicio y tres cargas/orígenes; rollback y snapshot inmutable              | `VigenciasPedidosTests.cs`, `VigenciasPedidosCasosTests.cs`                          | BR-004/018 y edición del pedido; no inventa cargo/horas/legajo                          |
| Identidad sin cuenta y exportación sin mutación                                              | `PedidosApiTests.cs`, `ExportacionLoteTests.cs`, `Identity/IngresoMicrosoftTests.cs` | BR-002 y ámbito del rol; login no provisiona usuarios                                   |

Los nombres sin prefijo están bajo
`backend/tests/ArsDocendi.IntegrationTests/Designaciones/`; `Identity/IngresoMicrosoftTests.cs`
parte de `backend/tests/ArsDocendi.IntegrationTests/`.
Resultados y límites: [evidencia 3.6–3.8](../../openspec/changes/archive/2026-10-20-corregir-integridad-y-rendimiento-post-auditoria/evidence/designaciones-3.6-3.8.md).

### Integridad del circuito (reglas de seguridad, no normativa)

`BR-designaciones-019` y `BR-designaciones-020` provienen del change
`integridad-sellada-pedidos`, no de normativa institucional. Su comportamiento vive en las specs
[`integridad-circuito-pedidos`](../../openspec/changes/integridad-sellada-pedidos/specs/integridad-circuito-pedidos/spec.md)
y [`exportacion-lote-designaciones`](../../openspec/specs/exportacion-lote-designaciones/spec.md).

| Regla                                                                                                                    | Tests persistentes                                                | Tipo                   |
| ------------------------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------- | ---------------------- |
| BR-designaciones-019: cada evento fuera de borrador queda sellado y firmado, en una cadena por período de sólo inserción | `IntegridadPedidosTests.cs`                                       | integración PostgreSQL |
| BR-designaciones-020: el lote se exporta sólo con la cadena íntegra e imprime la cabeza anclada                          | `IntegridadPedidosTests.cs` (`Lote_imprime_la_cabeza_anclada...`) | integración PostgreSQL |

## Assumptions (a confirmar)

- Las citas normativas de BR-001..004 y BR-018 provienen del estatuto / régimen docente UNLaM; el texto exacto (artículo/sección) se confirma con el cliente. La validación se implementa igual; el test queda en el lane `business` con cita pendiente.
- BR-018 se acota a Baja y Cambio (lo que pidió el cliente explícitamente). "Sin novedad" permanece admitida, pero no materializa cambios y no exige legajo.

## Open Questions

- Confirmar con el cliente la documentación exacta exigida por Alta/Baja (¿algún adjunto adicional?) y la fuente normativa de la regla "un pedido por docente por período".
- **Catálogo de cargos.** `designaciones.cargos` es la fuente única y la API sirve hoy seis cargos. La nomenclatura definitiva viene del convenio colectivo y del estatuto UNLaM: **la define el cliente, no el equipo**. Corregirla es un `INSERT`/`UPDATE`, no una migración.
- **Alcance de BR-001.** Se implementó la versión literal (por docente y período, sin importar la cátedra), que bloquea al segundo Jefe de Cátedra cuando un docente dicta en dos. Si el cliente confirma que debía ser **por cátedra**, es agregar `materia_id` al índice único.
- **Reintento tras rechazo.** El índice excluye `rechazado` y `cancelado`, o sea que hoy se puede volver a presentar tras un rechazo dentro del mismo período. Si un rechazo debe cerrar el período para ese docente, se saca `'rechazado'` del `WHERE` (y de `EstadosPedido.NoOcupanCupo`).
- **Matriz inicial de permisos.** `identity.rol_permisos` se siembra con una matriz derivada de las responsabilidades institucionales de cada rol, **no** del mock del frontend (que le asignaba "Aprobar designaciones — Decanato" al rol Docente). Es provisional y se ajusta desde `/roles` sin migración, pero conviene que Secretaría la valide.

## Aprobación

- **Aprobado por:** (pendiente — Secretaría Académica)
- **Fecha:**
- **Versión de la normativa vigente al aprobar:** (pendiente)
