## ADDED Requirements

### Requirement: Corrección del perfil por terceros autorizados

Además de su dueño, el perfil de Mi Portal de una persona SHALL poder ser leído por quien pueda ver a esa persona (`personas.ver`, o `personas.ver_ambito` dentro de su ámbito) y corregido por quien tenga `portal.administrar` dentro de su ámbito, con las mismas reglas de validación, versión y archivos que la edición propia. Los archivos que sube el operador MUST quedar asociados al perfil de la persona y no al del operador. Cada corrección MUST quedar auditada con el operador como autor. La edición propia desde Mi Portal no cambia.

#### Scenario: Secretaría corrige un CV

- **GIVEN** un operador con `portal.administrar` y una persona sin CV
- **WHEN** sube un PDF desde la administración de personas
- **THEN** el CV queda en el perfil de la persona y la persona lo ve en su Mi Portal

#### Scenario: Corrección fuera del ámbito

- **GIVEN** un Jefe de Cátedra y una persona sin designación en sus materias
- **WHEN** intenta corregir su perfil por la API
- **THEN** la API responde 404 y el perfil no cambia
