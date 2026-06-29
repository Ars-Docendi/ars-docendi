# Business rules: `designaciones`

## Contexto

- **Módulo / superficie:** `frontend/src/features/designaciones/` (prototipo SCRUM-7, mock; el backend `backend/src/Modules.Designaciones/` se implementa después).
- **Owner / stakeholders:** Secretaría Académica del Departamento (define el circuito); Jefe de Cátedra (carga).
- **Change/Spec OpenSpec relacionado:** `openspec/changes/proyecto-docente-pedidos/` (capability `pedidos-designacion`, SCRUM-7) y `openspec/changes/flujo-aprobacion-designaciones/` (capability `aprobacion-pedidos-designacion`, SCRUM-8).
- **Normativa de referencia:** Estatuto / régimen docente UNLaM (citas exactas **pendientes de confirmación con el cliente** para BR-001..004).

> **Alcance de este documento.** Registra las reglas que implementan **SCRUM-7** (carga de pedidos por el Jefe de Cátedra: BR-001..004, BR-008) y **SCRUM-8** (circuito de aprobación, change `flujo-aprobacion-designaciones`: BR-005, BR-009, BR-011, BR-013, BR-014, BR-015, BR-017), cada una con su mapping a test.

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

### BR-`designaciones`-005 Rechazo exige justificativo; devolución exige comentario

- **Statement:** Una acción de rechazo MUST incluir un justificativo no vacío y una acción de devolución MUST incluir un comentario no vacío; sin ese texto, la acción se rechaza.
- **Rationale:** El Jefe de Cátedra (y la auditoría) deben saber por qué un pedido fue rechazado o qué corregir tras una devolución; un rechazo/devolución sin fundamento no es trazable.
- **Provenance:** `from_spec`
- **Fuente normativa:** Decisión de proceso (a validar con Secretaría Académica).
- **Ejemplos:** Rechazar con justificativo vacío o solo espacios se deniega. Devolver sin comentario se deniega. El modal de la UI adelanta la validación; el dominio es la autoridad.
- **Roles afectados:** Coordinador, Secretaría, Decanato, Administración.

### BR-`designaciones`-009 Acción y visibilidad acotadas al ámbito del rol

- **Statement:** El Coordinador solo ve y actúa sobre pedidos de su carrera; Secretaría, Decanato y Administración tienen alcance de todo el departamento. Una acción sobre un pedido fuera del ámbito del actor MUST denegarse.
- **Rationale:** El circuito refleja la estructura institucional: un Coordinador de una carrera no decide sobre designaciones de otra.
- **Provenance:** `from_spec`
- **Fuente normativa:** Decisión de proceso (a validar con Secretaría Académica).
- **Ejemplos:** Un Coordinador de "Ingeniería en Informática" no ve ni puede actuar sobre un pedido de "Ingeniería Industrial". La Secretaría ve ambos.
- **Roles afectados:** Coordinador (acotado a su carrera); Secretaría, Decanato, Administración (depto-wide).

### BR-`designaciones`-011 El rechazo es terminal

- **Statement:** Un pedido en estado `rechazado` MUST no admitir ninguna acción posterior (aceptar, devolver, reenviar, etc.): es un estado terminal.
- **Rationale:** Un rechazo cierra el circuito para ese pedido; reabrirlo desvirtuaría la trazabilidad y la decisión tomada.
- **Provenance:** `from_spec`
- **Fuente normativa:** Decisión de proceso (a validar con Secretaría Académica).
- **Ejemplos:** Intentar aceptar o devolver un pedido ya rechazado se deniega (idempotencia terminal).
- **Roles afectados:** Todos los revisores.

### BR-`designaciones`-013 Solo el revisor de la etapa actual puede actuar

- **Statement:** Aceptar, rechazar o devolver un pedido MUST estar permitido únicamente al revisor cuyo rol corresponde a la etapa actual del pedido (o a Administración como revisor depto-wide, salvo aceptar). Un rol que no es el de la etapa actual se deniega.
- **Rationale:** La cadena es secuencial (Coordinador → Secretaría → Decanato); cada etapa la resuelve su responsable, no uno anterior o posterior.
- **Provenance:** `from_spec`
- **Fuente normativa:** Decisión de proceso (a validar con Secretaría Académica).
- **Ejemplos:** Con el pedido en `en_revision_secretaria`, el Coordinador (etapa anterior) no puede aceptarlo/rechazarlo/devolverlo.
- **Roles afectados:** Coordinador, Secretaría, Decanato (cada uno en su etapa); Administración (cualquier etapa, salvo aceptar).

### BR-`designaciones`-014 Devolución retrocede un nivel; reenvío retoma la etapa

- **Statement:** Una devolución lleva el pedido a `devuelto` fijando `etapaRetorno` (la etapa desde la que se devolvió) y `propietarioActual` (quién corrige), retrocediendo un nivel: desde Coordinador → Jefe de Cátedra; desde Secretaría → Coordinador; desde Decanato → Secretaría. El propietario puede reenviar, y el reenvío retoma la `etapaRetorno`.
- **Rationale:** La corrección la hace el actor inmediatamente anterior, y al reenviar el pedido vuelve exactamente a la etapa que lo devolvió (no reinicia toda la cadena).
- **Provenance:** `from_spec`
- **Fuente normativa:** Decisión de proceso (a validar con Secretaría Académica).
- **Ejemplos:** La Secretaría devuelve un pedido en `en_revision_secretaria` → queda `devuelto`, `propietarioActual = Coordinador`, `etapaRetorno = en_revision_secretaria`. Al reenviarlo el Coordinador, vuelve a `en_revision_secretaria`. Solo el propietario puede reenviar.
- **Roles afectados:** Todos los revisores (devuelven); el propietario anterior (reenvía).

### BR-`designaciones`-015 Administración revisa pero no aprueba

- **Statement:** El rol Administración puede rechazar y devolver pedidos dentro del departamento, pero MUST no poder aceptar (avanzar la cadena) en ninguna etapa.
- **Rationale:** Administración cumple un rol de control/gestión sobre el circuito, no de aprobación académica; la decisión de avanzar la designación recae en la cadena Coordinador → Secretaría → Decanato.
- **Provenance:** `from_spec`
- **Fuente normativa:** Decisión de proceso (a validar con Secretaría Académica).
- **Ejemplos:** Administración intenta aceptar un pedido en cualquier etapa `en_revision_*` → se deniega. Rechazar o devolver con su comentario sí está permitido.
- **Roles afectados:** Administración.

### BR-`designaciones`-017 Cualquier actor marca prioritario con justificativo

- **Statement:** Cualquier actor dentro de su ámbito puede marcar un pedido no terminal como prioritario (`prioritario = true`) sin cambiar su estado; el justificativo (motivo) MUST ser obligatorio.
- **Rationale:** La prioridad es una señal de gestión transversal (no una transición de estado); su motivo debe quedar registrado para la trazabilidad.
- **Provenance:** `from_spec`
- **Fuente normativa:** Decisión de proceso (a validar con Secretaría Académica).
- **Ejemplos:** Marcar prioritario sin justificativo se deniega. Con motivo, el pedido queda `prioritario = true` y conserva su estado (p. ej. sigue en `en_revision_coordinador`).
- **Roles afectados:** Todos los actores dentro de su ámbito.

## Mapping a tests

| Rule ID              | Test file(s)                                                                                                                                                                                   | Tipo                          | Notas                                 |
| -------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------- | ------------------------------------- |
| BR-designaciones-001 | `frontend/src/features/designaciones/pedidoValidacion.test.ts` → `unPedidoPorDocentePorPeriodo`                                                                                                | unit (business)               | Cita normativa pendiente con cliente. |
| BR-designaciones-002 | `frontend/src/features/designaciones/pedidoValidacion.test.ts` → `altaExigeCvYDniFrenteYDorso`                                                                                                 | unit (business)               | Cita normativa pendiente con cliente. |
| BR-designaciones-003 | `frontend/src/features/designaciones/pedidoValidacion.test.ts` → `bajaExigeJustificativo`                                                                                                      | unit (business)               | Cita normativa pendiente con cliente. |
| BR-designaciones-004 | `frontend/src/features/designaciones/pedidoValidacion.test.ts` → `cambioExigeJustificacion`                                                                                                    | unit (business)               | Cita normativa pendiente con cliente. |
| BR-designaciones-008 | `frontend/src/features/designaciones/api/maquinaEstados.test.ts` → `editarSoloBorradorODevueltoDelPropietario`, `no edita tras enviar a revisión`                                              | unit (business)               | Decisión de proceso.                  |
| BR-designaciones-005 | `frontend/src/features/designaciones/api/maquinaEstados.test.ts` → `rechazoSinJustificativoFalla`, `devolucionSinComentarioFalla`; `components/ModalAccionRevision.test.tsx` (UI)              | unit + ui (business)          | Decisión de proceso.                  |
| BR-designaciones-009 | `frontend/src/features/designaciones/api/maquinaEstados.test.ts` → `coordinadorFueraDeCarreraDenegado`; `api/pedidosApi.test.ts` → `listarPedidosPorAmbito acota a la carrera del Coordinador` | unit (business)               | Decisión de proceso.                  |
| BR-designaciones-011 | `frontend/src/features/designaciones/api/maquinaEstados.test.ts` → `rechazoEsTerminal`                                                                                                         | unit (business)               | Decisión de proceso.                  |
| BR-designaciones-013 | `frontend/src/features/designaciones/api/maquinaEstados.test.ts` → `rolEtapaIncorrectaDenegado`                                                                                                | unit (business)               | Decisión de proceso.                  |
| BR-designaciones-014 | `frontend/src/features/designaciones/api/maquinaEstados.test.ts` → `devolucionRetrocedeUnNivel`, `reenvioRetomaEtapaDelRevisor`; `pages/flujoAprobacion.test.tsx` (devolución → reenvío)       | unit + integración (business) | Decisión de proceso.                  |
| BR-designaciones-015 | `frontend/src/features/designaciones/api/maquinaEstados.test.ts` → `administracionNoPuedeAceptar`                                                                                              | unit (business)               | Decisión de proceso.                  |
| BR-designaciones-017 | `frontend/src/features/designaciones/api/maquinaEstados.test.ts` → `prioritarioExigeJustificativo`, `prioridadNoCambiaEstado`                                                                  | unit (business)               | Decisión de proceso.                  |

Todo BR-\* debe tener al menos un test verificando la regla.

## Pendientes (SCRUM-8)

✅ **Completado.** Las reglas del circuito de aprobación (BR-005, BR-009, BR-011, BR-013, BR-014, BR-015, BR-017) quedaron registradas arriba **con sus tests** en el change `flujo-aprobacion-designaciones`. No quedan BR pendientes de SCRUM-8.

## Assumptions (a confirmar)

- Las citas normativas de BR-001..004 provienen del estatuto / régimen docente UNLaM; el texto exacto (artículo/sección) se confirma con el cliente. La validación se implementa igual; el test queda en el lane `business` con cita pendiente.

## Open Questions

- Confirmar con el cliente la documentación exacta exigida por Alta/Baja (¿algún adjunto adicional?) y la fuente normativa de la regla "un pedido por docente por período".

## Aprobación

- **Aprobado por:** (pendiente — Secretaría Académica)
- **Fecha:**
- **Versión de la normativa vigente al aprobar:** (pendiente)
