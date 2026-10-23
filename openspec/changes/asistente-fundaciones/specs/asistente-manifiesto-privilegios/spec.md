## ADDED Requirements

### Requirement: Manifiesto versionado de privilegios

El sistema SHALL mantener un manifiesto versionado en el repositorio que enumere **toda** tabla de los schemas expuestos al asistente. Cada tabla MUST estar clasificada como `concedida`, con la lista explícita de columnas legibles y por cuál de los dos roles, o como `denegada-explicita`, con el motivo escrito.

El manifiesto MUST ser la única fuente de verdad de los privilegios del asistente: ningún `GRANT` puede existir en las migraciones sin su entrada correspondiente.

#### Scenario: Toda tabla expuesta está clasificada

- **GIVEN** el manifiesto versionado
- **WHEN** se lo compara con las tablas existentes en los schemas expuestos
- **THEN** cada tabla aparece exactamente una vez, como `concedida` o como `denegada-explicita`

#### Scenario: Las denegaciones llevan motivo

- **GIVEN** una entrada `denegada-explicita` del manifiesto
- **THEN** tiene un motivo no vacío que explica por qué la tabla no se concede

### Requirement: Verificación del manifiesto en las tres direcciones

El sistema SHALL incluir un test automatizado que compare el manifiesto contra los privilegios efectivos de la base. El test MUST fallar en cualquiera de estos tres casos: un privilegio efectivo que el manifiesto no declara; un privilegio declarado en el manifiesto que ya no existe en la base; una tabla presente en un schema expuesto que el manifiesto no clasifica.

#### Scenario: Privilegio concedido fuera del manifiesto

- **GIVEN** una base donde se concedió `SELECT` sobre una columna que el manifiesto no declara
- **WHEN** corre el test de manifiesto
- **THEN** el test falla e identifica la tabla y la columna concedidas de más

#### Scenario: Privilegio declarado que desapareció

- **GIVEN** un manifiesto que declara `SELECT` sobre una columna que ya no está concedida en la base
- **WHEN** corre el test de manifiesto
- **THEN** el test falla e identifica la declaración obsoleta

#### Scenario: Tabla nueva sin clasificar

- **GIVEN** una migración de cualquier módulo que agrega una tabla a un schema expuesto
- **AND** el manifiesto no fue actualizado
- **WHEN** corre el test de manifiesto
- **THEN** el test falla e identifica la tabla sin clasificar

#### Scenario: Manifiesto consistente

- **GIVEN** una base cuyos privilegios efectivos coinciden exactamente con el manifiesto
- **AND** toda tabla de los schemas expuestos está clasificada
- **WHEN** corre el test de manifiesto
- **THEN** el test pasa
