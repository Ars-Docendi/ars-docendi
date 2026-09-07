# pedidos-designacion

## Purpose

Gestionar por API el ciclo de vida de un pedido docente sobre una única materia, desde el borrador hasta su envío y eventual corrección, con autorización por rol y ámbito.

## Requirements

### Requirement: Persistencia remota

La creación, consulta, edición, eliminación y transición de pedidos SHALL usar la API de Designaciones. El backend MUST asignar el número, validar reglas y ámbito, y persistir cada cambio con su historial; el frontend MUST NOT mantener un store alternativo.

#### Scenario: Crear un borrador

- **GIVEN** un Jefe de Cátedra autorizado y un período activo
- **WHEN** crea un pedido válido para una materia a su cargo
- **THEN** la API devuelve un borrador con ID, número y evento `crear`

#### Scenario: Cliente envía una operación inválida

- **WHEN** cualquier cliente intenta una edición o transición no autorizada
- **THEN** el backend la rechaza sin modificar el pedido ni agregar historial

### Requirement: Catálogos canónicos

Personas, materias, cargos y períodos SHALL provenir de la API. Las mutaciones MUST enviar sus UUID canónicos. Los formularios SHALL ofrecer sólo materias y cargos activos dentro del ámbito del actor.

#### Scenario: Abrir formulario

- **WHEN** un actor abre el alta o edición
- **THEN** ve el período activo y los catálogos persistidos que puede utilizar

### Requirement: Un pedido por persona y período [BR-designaciones-001]

No SHALL existir más de un pedido no rechazado ni cancelado para una persona en el mismo período, aunque pertenezca a otra materia. La base MUST imponer la unicidad y la API MUST traducir el conflicto sin revelar el pedido bloqueante.

#### Scenario: Segundo pedido

- **GIVEN** una persona con un pedido vivo en el período
- **WHEN** se intenta crear otro
- **THEN** la API responde conflicto sin exponer materia, contenido ni autor del primero

### Requirement: Reglas por novedad

El backend MUST aplicar las reglas por novedad al crear y editar. `Alta` exige CV, DNI frente y DNI dorso [BR-designaciones-002]. `Baja` exige tipo, adjunto justificativo y persona con legajo [BR-designaciones-003, BR-designaciones-018]. `Cambio de cargo o dedicación` exige justificación y persona con legajo [BR-designaciones-004, BR-designaciones-018]. `Sin novedad` permanece como valor persistido admitido.

#### Scenario: Alta incompleta

- **WHEN** se guarda un Alta sin alguno de sus tres adjuntos
- **THEN** el backend rechaza el pedido

#### Scenario: Baja o Cambio sin legajo

- **WHEN** la persona no tiene legajo
- **THEN** el backend rechaza una Baja o Cambio, pero permite un Alta con documentación completa

### Requirement: Una materia por pedido

Cada pedido SHALL referir exactamente una materia y su carga horaria. La carrera se deriva de esa materia para determinar al Coordinador competente.

#### Scenario: Enrutamiento

- **GIVEN** un pedido de una materia perteneciente a una carrera
- **WHEN** se envía
- **THEN** queda en revisión del Coordinador de esa carrera

### Requirement: Edición y eliminación

Un borrador SHALL ser editable y eliminable sólo por un Jefe de Cátedra dentro de su ámbito. Un pedido devuelto SHALL ser editable por su propietario actual —Jefe, Coordinador o Secretaría— dentro de su ámbito, pero MUST NOT ser eliminable [BR-designaciones-008, BR-designaciones-009].

#### Scenario: Corregir una devolución

- **GIVEN** un pedido devuelto a Coordinación o Secretaría
- **WHEN** el propietario edita datos válidos y lo reenvía
- **THEN** la API acepta ambas operaciones y conserva el snapshot inicial

#### Scenario: Borrar un borrador

- **WHEN** su Jefe de Cátedra elimina un borrador dentro de su ámbito
- **THEN** el pedido deja de existir

### Requirement: Envío y reenvío idempotentes

Enviar y reenviar SHALL exigir `Idempotency-Key`. Repetir la misma solicitud con la misma clave MUST devolver la respuesta confirmada sin duplicar la transición. Reusar la clave con otra solicitud MUST responder conflicto.

#### Scenario: Reintento de red

- **WHEN** el cliente repite un envío confirmado con la misma clave
- **THEN** existe un único evento `enviar`

### Requirement: Snapshot histórico

Al enviar por primera vez, el backend SHALL congelar cargo, dedicación, materia y horas vigentes. El detalle SHALL mostrar ese snapshot aunque cambien o se desactiven los catálogos actuales. Editar y reenviar MUST NOT recalcularlo.

#### Scenario: Cambia la designación vigente

- **GIVEN** un pedido ya enviado
- **WHEN** cambian la designación o la materia del catálogo
- **THEN** su detalle conserva los datos fotografiados al enviar

### Requirement: Lista Mis pedidos

`/designaciones/mis-pedidos` SHALL listar los pedidos visibles del período, con número, persona, legajo, materia, novedad, estado y prioridad. La fila y la acción Ver SHALL abrir el detalle. Editar y Eliminar SHALL mostrarse sólo cuando la API incluya esas acciones.

#### Scenario: Filtros

- **WHEN** se filtra por docente, número, legajo, tipo o estado
- **THEN** la tabla conserva sólo las coincidencias sin distinguir mayúsculas ni acentos

#### Scenario: Estados remotos

- **WHEN** la consulta carga, falla, queda vacía o responde con datos
- **THEN** la pantalla representa explícitamente Loading, Error, Empty o Success

### Requirement: Validación anticipada y autoridad

El frontend SHALL anticipar las mismas reglas para dar feedback inmediato, pero el backend MUST ser la autoridad. Guardar, enviar y reenviar MUST validar antes de llamar a la API.

#### Scenario: Guardado o envío desde el formulario

- **WHEN** el usuario elige Guardar, Guardar y enviar o Guardar y reenviar
- **THEN** el frontend muestra errores de campos requeridos antes de ejecutar las mutaciones
