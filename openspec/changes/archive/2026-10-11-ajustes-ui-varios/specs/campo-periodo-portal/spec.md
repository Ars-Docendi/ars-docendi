## ADDED Requirements

### Requirement: Mes y año del período se eligen en cualquier orden

Los campos de período del Portal (desde/hasta en Experiencia, Educación y Proyectos) SHALL permitir elegir el mes y el año de forma independiente y en cualquier orden. El mes elegido MUST conservarse mientras el año todavía no esté cargado. El valor resultante MUST seguir siendo `"YYYY"` (sin mes) o `"YYYY-MM"` (con mes), sin cambios en el formato persistido.

#### Scenario: Elegir el mes antes que el año

- **GIVEN** un docente con el diálogo de Experiencia abierto y el campo "Desde" vacío
- **WHEN** elige el mes "mar" y después escribe el año "2014"
- **THEN** el selector de mes MUST seguir mostrando "mar" después de elegirlo
- **AND** el valor del campo MUST ser `"2014-03"`

#### Scenario: Elegir el año antes que el mes

- **GIVEN** un docente con el campo "Desde" vacío
- **WHEN** escribe el año "2014" y después elige el mes "mar"
- **THEN** el valor del campo MUST ser `"2014-03"`

#### Scenario: Año sin mes

- **GIVEN** un docente con el campo "Desde" vacío
- **WHEN** escribe el año "2014" sin elegir mes
- **THEN** el valor del campo MUST ser `"2014"`

#### Scenario: Mes sin año impide guardar

- **GIVEN** un docente que eligió un mes pero no cargó el año de un campo de período
- **WHEN** intenta guardar el ítem
- **THEN** el sistema MUST impedir el guardado
- **AND** MUST mostrar el error inline "Completá el año" en ese campo

#### Scenario: Mismo diseño visual

- **WHEN** se renderiza un campo de período del Portal
- **THEN** el sistema SHALL mostrar un selector de mes opcional (con la opción vacía "Mes" y los meses "ene" a "dic") junto a un campo de año de 4 dígitos, cada uno con su etiqueta accesible
