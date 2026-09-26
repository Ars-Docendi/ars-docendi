## ADDED Requirements

### Requirement: Una intención puede declarar términos excluidos

El sistema SHALL permitir que una intención declare términos que no deben aparecer en la pregunta, y MUST NOT reconocerla cuando alguno esté presente.

#### Scenario: Un término excluido impide el reconocimiento

- **GIVEN** una intención que excluye un término
- **WHEN** la pregunta contiene ese término
- **THEN** la intención no se reconoce, aunque estén todos sus términos exigidos

#### Scenario: Los términos excluidos también se validan al cargar

- **GIVEN** una intención con un término excluido sin normalizar
- **WHEN** se carga el catálogo
- **THEN** la carga falla nombrando la intención y el término

#### Scenario: Un término no puede estar exigido y excluido a la vez

- **GIVEN** una intención que declara el mismo término como exigido y como excluido
- **WHEN** se carga el catálogo
- **THEN** la carga falla nombrando la intención, porque no podría reconocerse nunca
