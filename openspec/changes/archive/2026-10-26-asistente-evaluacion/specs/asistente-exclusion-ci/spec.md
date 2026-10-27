## ADDED Requirements

### Requirement: El runner queda fuera del archivo de solución

El proyecto que instancia un proveedor real de modelo MUST NOT figurar en el archivo de solución.

#### Scenario: El archivo de solución no lo nombra

- **WHEN** se lee el archivo de solución
- **THEN** no contiene ninguna referencia al proyecto del runner

#### Scenario: El núcleo sí está en la solución

- **WHEN** se lee el archivo de solución
- **THEN** contiene el proyecto del núcleo de evaluación, que no hace llamadas facturadas

### Requirement: Guard dentro de la solución

El sistema SHALL incluir, dentro de la solución, una verificación que falle si el proyecto del runner vuelve a entrar al archivo de solución.

#### Scenario: El guard falla si el runner entra

- **GIVEN** el archivo de solución con el proyecto del runner agregado
- **WHEN** corre la verificación
- **THEN** falla con un mensaje que nombra el proyecto

#### Scenario: El guard no es vacío

- **WHEN** corre la verificación sobre el archivo de solución vigente
- **THEN** el archivo se leyó y contiene proyectos, en lugar de pasar por no haber encontrado nada

### Requirement: El CI no hace llamadas facturadas

Ningún trabajo de integración continua MUST ejecutar una llamada real a un proveedor de modelo. El proveedor por omisión de todos los ambientes MUST ser el simulado.

#### Scenario: Ningún proyecto de la solución instancia un proveedor real

- **WHEN** se inspeccionan los proyectos del archivo de solución
- **THEN** ninguno referencia el proyecto del runner

#### Scenario: El default de configuración es el proveedor simulado

- **WHEN** se lee la configuración por omisión del módulo
- **THEN** el proveedor seleccionado es el simulado

### Requirement: Procedimiento manual documentado

El repositorio SHALL documentar cómo se corre la evaluación a mano.

#### Scenario: La documentación existe y nombra el comando

- **WHEN** se busca la documentación del eval
- **THEN** describe cómo ejecutarlo y qué se necesita para hacerlo
