## ADDED Requirements

### Requirement: Documentación obligatoria para Alta nueva

Cuando el tipo de pedido sea "Alta nueva", el sistema SHALL exigir tres documentos en la sección Documentación: CV en PDF, foto del DNI (frente) y foto del DNI (dorso). Para los demás tipos de pedido la documentación SHALL ser opcional.

#### Scenario: Alta nueva exige los tres documentos

- **WHEN** el tipo de pedido es "Alta nueva"
- **THEN** la sección Documentación marca CV (PDF), DNI frente y DNI dorso como obligatorios

#### Scenario: Documentación opcional para otros tipos

- **WHEN** el tipo de pedido es Renovación, Cambio de cargo o Baja
- **THEN** la sección Documentación indica que adjuntar documentos es opcional para ese tipo

### Requirement: Bloqueo del envío hasta completar la documentación obligatoria

Cuando el tipo sea "Alta nueva" y falte alguno de los tres documentos obligatorios, el sistema SHALL deshabilitar la acción "Enviar a revisión" y SHALL informar en el footer cuántos documentos obligatorios faltan. Cuando los tres documentos estén cargados, el sistema SHALL habilitar "Enviar a revisión" e indicar en el footer que el pedido está listo para enviar.

#### Scenario: Envío bloqueado con documentos faltantes

- **WHEN** el tipo es "Alta nueva" y faltan documentos obligatorios
- **THEN** "Enviar a revisión" está deshabilitado y el footer muestra el mensaje de documentos faltantes (p. ej. "Faltan 3 documentos obligatorios para enviar")

#### Scenario: Envío habilitado con documentación completa

- **WHEN** el tipo es "Alta nueva" y los tres documentos obligatorios están cargados
- **THEN** "Enviar a revisión" se habilita y el footer indica que el pedido está listo para enviar

### Requirement: Feedback visual del requisito documental

Cuando el tipo sea "Alta nueva", el sistema SHALL mostrar un banner condicional explicando el requisito y SHALL teñir de advertencia (warning) el encabezado de la sección Documentación y los slots de los documentos obligatorios aún no cargados.

#### Scenario: Banner y tinte de advertencia en Alta nueva

- **WHEN** el tipo de pedido seleccionado es "Alta nueva"
- **THEN** se muestra el banner del requisito documental y la sección Documentación (header y slots faltantes) se renderiza con estilo de advertencia
