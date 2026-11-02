# designaciones-vigentes-propias Specification

## Purpose

Definir la consulta de designaciones vigentes del docente en Mi Portal.

## Requirements

### Requirement: Consultar mis designaciones vigentes

`GET /api/designaciones/vigentes/mias` SHALL devolver a la persona autenticada sus designaciones vigentes con materia, carrera, cargo, dedicación, horas de la materia, horas de investigación, horas externas y fecha de inicio. MUST NOT devolver designaciones de otras personas. Un usuario sin persona vinculada SHALL recibir una lista vacía.

#### Scenario: Sólo las propias

- **GIVEN** dos docentes con designaciones vigentes
- **WHEN** uno consulta sus designaciones
- **THEN** recibe sólo las suyas

### Requirement: Designaciones vigentes en Mi Portal

Mi Portal SHALL mostrar una sección de sólo lectura "Designaciones vigentes" con una fila por designación y el total de horas. Sin designaciones, la sección SHALL presentarse compacta con el texto "Sin designaciones vigentes".

#### Scenario: Con designaciones

- **GIVEN** un docente con dos designaciones vigentes
- **WHEN** abre `/portal`
- **THEN** ve ambas con su cargo, dedicación y horas, y el total de horas

#### Scenario: Sin designaciones

- **GIVEN** un docente sin designaciones vigentes
- **WHEN** abre `/portal`
- **THEN** la sección muestra "Sin designaciones vigentes"
