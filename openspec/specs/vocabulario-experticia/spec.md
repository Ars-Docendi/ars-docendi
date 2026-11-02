# vocabulario-experticia Specification

## Purpose

Definir el vocabulario compartido de habilidades e intereses y su curaduría.

## Requirements

### Requirement: Vocabulario compartido desde la base

`GET /api/portal/vocabulario` SHALL devolver a cualquier usuario autenticado los términos incorporados (no sugeridos) y no fusionados, ordenados alfabéticamente. Sin autenticación MUST responder 401.

#### Scenario: Términos ofrecidos

- **GIVEN** un término incorporado, uno sugerido y uno fusionado
- **WHEN** un docente pide el vocabulario
- **THEN** recibe sólo el incorporado

### Requirement: Curar el vocabulario

Un actor con `portal.vocabulario.administrar` SHALL poder listar todos los términos con su estado (incorporado, sugerido o fusionado), la cantidad de docentes que lo usan y su canónico, y SHALL poder incorporar un sugerido, renombrar un término y fusionar un término en otro. Sin el permiso la API MUST responder 403. Renombrar a un término normalizado existente MUST responder 409 [BR-portal-002]. Fusionar A en B MUST mover los usos de A a B sin duplicados, registrar B como canónico de A y forzar 409 en los editores abiertos de los perfiles afectados [BR-portal-004]. No MUST fusionarse un término consigo mismo, en un término fusionado ni un término que ya es canónico de otros.

#### Scenario: Incorporar un sugerido [BR-portal-003]

- **GIVEN** el término sugerido "Robótica"
- **WHEN** la Secretaría lo incorpora
- **THEN** deja de figurar como sugerido y se ofrece en el vocabulario

#### Scenario: Renombrar en conflicto [BR-portal-002]

- **GIVEN** los términos "Bases de datos" y "BD"
- **WHEN** se renombra "BD" a "bases de datos"
- **THEN** la API responde 409 y sugiere fusionar

#### Scenario: Fusionar variantes [BR-portal-004]

- **GIVEN** un docente con "BD" y "Bases de datos" como habilidades y otro con "BD"
- **WHEN** se fusiona "BD" en "Bases de datos"
- **THEN** ambos docentes quedan con "Bases de datos" una sola vez y "BD" no se ofrece más

#### Scenario: Sin permiso

- **GIVEN** un Jefe de Cátedra con `portal.administrar`
- **WHEN** llama a la curaduría
- **THEN** recibe 403
