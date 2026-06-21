# Business rules: `designaciones`

## Contexto

- **Módulo / superficie:** `frontend/src/features/designaciones/` (prototipo SCRUM-7, mock; el backend `backend/src/Modules.Designaciones/` se implementa después).
- **Owner / stakeholders:** Secretaría Académica del Departamento (define el circuito); Jefe de Cátedra (carga).
- **Change/Spec OpenSpec relacionado:** `openspec/changes/proyecto-docente-pedidos/` (capability `pedidos-designacion`).
- **Normativa de referencia:** Estatuto / régimen docente UNLaM (citas exactas **pendientes de confirmación con el cliente** para BR-001..004).

> **Alcance de este documento.** Registra las reglas que implementa **SCRUM-7** (carga de pedidos por el Jefe de Cátedra). Las reglas del circuito de aprobación (BR-005, BR-009, BR-011, BR-013, BR-014, BR-015, BR-017) las agrega **SCRUM-8** (`flujo-aprobacion-designaciones`) junto con sus tests; se listan en [Pendientes (SCRUM-8)](#pendientes-scrum-8) para no registrarlas sin test.

## Reglas

### BR-`designaciones`-001 Un pedido por docente por período

- **Statement:** No puede existir más de un pedido de designación para el mismo docente dentro de un mismo período abierto.
- **Rationale:** Evita designaciones duplicadas o contradictorias para un docente en un ciclo; el proyecto docente del período debe tener una sola entrada por docente.
- **Provenance:** `from_spec`
- **Fuente normativa:** Pendiente de confirmación con el cliente (estatuto / régimen docente UNLaM).
- **Ejemplos:** Si ya hay un pedido para el DNI 30.111.222 en el período abierto, crear otro para ese DNI se rechaza. Al **editar** el propio pedido, no se considera duplicado.
- **Roles afectados:** Jefe de Cátedra.

### BR-`designaciones`-002 Alta exige CV + DNI frente + DNI dorso

- **Statement:** Un pedido con novedad "Alta" debe adjuntar obligatoriamente CV, foto de DNI (frente) y foto de DNI (dorso) antes de poder guardarse.
- **Rationale:** El alta de un docente nuevo requiere la documentación mínima de identidad y antecedentes para la designación.
- **Provenance:** `from_spec`
- **Fuente normativa:** Pendiente de confirmación con el cliente (estatuto / régimen docente UNLaM).
- **Ejemplos:** Un "Alta" sin la foto de DNI dorso bloquea el guardado e indica el adjunto faltante. Con los tres adjuntos, no marca error de documentación.
- **Roles afectados:** Jefe de Cátedra.

### BR-`designaciones`-003 Baja exige justificativo

- **Statement:** Un pedido con novedad "Baja" debe adjuntar obligatoriamente un justificativo antes de poder guardarse.
- **Rationale:** La baja de una designación debe quedar documentada con su justificación.
- **Provenance:** `from_spec`
- **Fuente normativa:** Pendiente de confirmación con el cliente (estatuto / régimen docente UNLaM).
- **Ejemplos:** Una "Baja" sin adjunto justificativo bloquea el guardado. Con el justificativo cargado, no marca error.
- **Roles afectados:** Jefe de Cátedra.

### BR-`designaciones`-004 Cambio de cargo o dedicación exige justificación

- **Statement:** Un pedido con novedad "Cambio de cargo o dedicación" debe incluir una justificación no vacía antes de poder guardarse.
- **Rationale:** Todo cambio de cargo/dedicación debe estar fundamentado para su evaluación por la cadena de aprobación.
- **Provenance:** `from_spec`
- **Fuente normativa:** Pendiente de confirmación con el cliente (estatuto / régimen docente UNLaM).
- **Ejemplos:** Un "Cambio" con justificación vacía (o solo espacios) bloquea el guardado. Con una justificación cargada, no marca ese error.
- **Roles afectados:** Jefe de Cátedra.

### BR-`designaciones`-008 Tras enviar, el Jefe de Cátedra no edita salvo devolución

- **Statement:** Un pedido solo es editable en estado `borrador`, o en estado `devuelto` cuando el actor es su propietario actual. Tras enviarlo a revisión, queda de solo lectura para el Jefe de Cátedra hasta que sea devuelto.
- **Rationale:** Preserva la integridad de lo que está bajo revisión: el revisor evalúa lo que recibió, y el JC solo puede corregir si el pedido vuelve a sus manos.
- **Provenance:** `from_spec`
- **Fuente normativa:** Decisión de proceso (a validar con Secretaría Académica).
- **Ejemplos:** Un pedido en `en_revision_coordinador` rechaza la edición. Un pedido `devuelto` con `propietarioActual = "Jefe de Cátedra"` se puede editar; la edición no cambia el estado.
- **Roles afectados:** Jefe de Cátedra (y, para la devolución, los revisores definidos en SCRUM-8).

## Mapping a tests

| Rule ID              | Test file(s)                                                                                                                                      | Tipo            | Notas                                 |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- | --------------- | ------------------------------------- |
| BR-designaciones-001 | `frontend/src/features/designaciones/pedidoValidacion.test.ts` → `unPedidoPorDocentePorPeriodo`                                                   | unit (business) | Cita normativa pendiente con cliente. |
| BR-designaciones-002 | `frontend/src/features/designaciones/pedidoValidacion.test.ts` → `altaExigeCvYDniFrenteYDorso`                                                    | unit (business) | Cita normativa pendiente con cliente. |
| BR-designaciones-003 | `frontend/src/features/designaciones/pedidoValidacion.test.ts` → `bajaExigeJustificativo`                                                         | unit (business) | Cita normativa pendiente con cliente. |
| BR-designaciones-004 | `frontend/src/features/designaciones/pedidoValidacion.test.ts` → `cambioExigeJustificacion`                                                       | unit (business) | Cita normativa pendiente con cliente. |
| BR-designaciones-008 | `frontend/src/features/designaciones/api/maquinaEstados.test.ts` → `editarSoloBorradorODevueltoDelPropietario`, `no edita tras enviar a revisión` | unit (business) | Decisión de proceso.                  |

Todo BR-\* debe tener al menos un test verificando la regla.

## Pendientes (SCRUM-8)

Reglas del circuito de aprobación, a registrar **con sus tests** en el change `flujo-aprobacion-designaciones`:

- **BR-designaciones-005** — Rechazo exige justificativo; devolución exige comentario.
- **BR-designaciones-009** — Acción/visibilidad acotada al ámbito del rol (Coordinador → su carrera).
- **BR-designaciones-011** — El rechazo es terminal.
- **BR-designaciones-013** — Solo el revisor de la etapa actual puede actuar.
- **BR-designaciones-014** — Devolución retrocede un nivel; reenvío retoma la etapa.
- **BR-designaciones-015** — Administración revisa pero no aprueba.
- **BR-designaciones-017** — Cualquier actor marca prioritario con justificativo.

## Assumptions (a confirmar)

- Las citas normativas de BR-001..004 provienen del estatuto / régimen docente UNLaM; el texto exacto (artículo/sección) se confirma con el cliente. La validación se implementa igual; el test queda en el lane `business` con cita pendiente.

## Open Questions

- Confirmar con el cliente la documentación exacta exigida por Alta/Baja (¿algún adjunto adicional?) y la fuente normativa de la regla "un pedido por docente por período".

## Aprobación

- **Aprobado por:** (pendiente — Secretaría Académica)
- **Fecha:**
- **Versión de la normativa vigente al aprobar:** (pendiente)
