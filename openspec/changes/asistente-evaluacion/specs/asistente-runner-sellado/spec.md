## ADDED Requirements

### Requirement: Preflight obligatorio

El runner SHALL verificar, antes de ejecutar ningún ítem, que el proveedor responde de verdad.

El preflight MUST rechazar una respuesta simulada y MUST rechazar una respuesta con conteos de tokens en cero.

#### Scenario: Sin proveedor, el runner aborta

- **GIVEN** un proveedor que falla
- **WHEN** se ejecuta el runner
- **THEN** termina con código distinto de cero

#### Scenario: Un proveedor simulado se rechaza

- **GIVEN** el proveedor simulado
- **WHEN** se ejecuta el runner
- **THEN** termina con código distinto de cero

#### Scenario: Una respuesta con métricas en cero se rechaza

- **GIVEN** un proveedor que responde con entrada y salida en cero tokens
- **WHEN** se ejecuta el runner
- **THEN** termina con código distinto de cero

#### Scenario: El preflight no consume el presupuesto de la corrida

- **WHEN** el preflight resulta favorable
- **THEN** su llamada no se cuenta como ítem del dataset

### Requirement: Ningún reporte sobre una corrida inválida

Cuando el preflight falla, el runner MUST NOT escribir ningún reporte en disco.

#### Scenario: El preflight fallido no deja archivo

- **GIVEN** un directorio de reportes vacío y un proveedor que falla
- **WHEN** se ejecuta el runner
- **THEN** el directorio sigue vacío

#### Scenario: Un reporte anterior no se sobrescribe

- **GIVEN** un reporte de una corrida válida anterior y un proveedor que falla
- **WHEN** se ejecuta el runner
- **THEN** el reporte anterior queda intacto

### Requirement: Reportes sellados con tres hashes

Cada reporte SHALL estampar en su encabezado el hash del prefijo del prompt, el del dataset y el del fixture.

Los reportes MUST ser generados y MUST NOT editarse a mano.

#### Scenario: El encabezado trae los tres hashes

- **WHEN** se genera un reporte
- **THEN** su encabezado contiene los tres hashes

#### Scenario: Un dataset distinto produce un sello distinto

- **GIVEN** un reporte de una corrida
- **WHEN** se cambia el dataset y se vuelve a correr
- **THEN** el hash del dataset del reporte nuevo es distinto

### Requirement: Los conteos del reporte salen del dataset leído

Los conteos del reporte MUST derivarse del dataset efectivamente cargado.

#### Scenario: Los conteos coinciden con el archivo

- **WHEN** se genera un reporte
- **THEN** el total de ítems del reporte es igual al número de ítems del archivo cargado

#### Scenario: Los conteos por categoría suman el total

- **WHEN** se generan los conteos por categoría
- **THEN** su suma es igual al total de ítems

### Requirement: Corridas estables

Dos corridas consecutivas sobre el mismo dataset, el mismo fixture y el mismo prefijo MUST producir reportes comparables, y su variación MUST quedar registrada.

#### Scenario: Dos corridas con un proveedor determinista dan lo mismo

- **GIVEN** un proveedor determinista
- **WHEN** se corre dos veces
- **THEN** los dos reportes tienen los mismos conteos y los mismos puntajes
