## MODIFIED Requirements

### Requirement: Ninguna columna `sensible-valor` llega al modelo con su valor real

El sistema SHALL reemplazar por un marcador todo valor de una columna clasificada `sensible-valor` antes de armar el prompt de redacción.

El prompt de redacción MUST NOT contener ningún valor real de esas columnas, salvo cuando rige la opción `Asistente__RedaccionSinEnmascarar` (`asistente-redaccion-sin-enmascarado-local`): sólo con proveedor en hardware propio (`local` u `ollama`). Ésa es la única excepción; en cualquier otra configuración el requisito rige sin matices.

#### Scenario: El documento no aparece en el prompt

- **GIVEN** un resultado con una columna `sensible-valor` cuyo valor real es conocido
- **WHEN** se arma el prompt de redacción
- **THEN** el prompt no contiene ese valor

#### Scenario: El alias tampoco lo deja pasar

- **GIVEN** un resultado donde la columna `sensible-valor` viene con un alias arbitrario
- **WHEN** se arma el prompt de redacción
- **THEN** el prompt no contiene el valor real

#### Scenario: Las columnas públicas viajan intactas

- **GIVEN** un resultado con columnas públicas
- **WHEN** se arma el prompt de redacción
- **THEN** sus valores aparecen tal cual
