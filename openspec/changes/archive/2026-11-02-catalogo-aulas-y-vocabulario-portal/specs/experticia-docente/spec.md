## MODIFIED Requirements

### Requirement: Administración de tags desde el vocabulario

El sistema SHALL permitir al docente agregar y quitar tags en cada lista, seleccionándolos desde el vocabulario compartido que devuelve la API del Portal. Los cambios MUST persistirse mediante la API del Portal. Un término guardado que fue fusionado MUST registrarse como su término canónico [BR-portal-004], y una lista MUST NOT contener dos veces el mismo término normalizado [BR-portal-002].

#### Scenario: Agregar un tag desde el vocabulario

- **GIVEN** el docente en la sección Habilidades
- **WHEN** selecciona un término del vocabulario
- **THEN** el término se agrega como tag a esa lista

#### Scenario: Quitar un tag

- **GIVEN** un tag ya agregado a una lista
- **WHEN** el docente lo quita
- **THEN** el tag desaparece de esa lista sin afectar a la otra

#### Scenario: Sección de tags vacía

- **GIVEN** un docente sin habilidades cargadas
- **WHEN** abre `/portal`
- **THEN** la sección Habilidades se presenta como fila compacta con su control de alta

#### Scenario: Guardar una variante fusionada [BR-portal-004]

- **GIVEN** "BD" fusionado en "Bases de datos"
- **WHEN** un perfil guarda "BD"
- **THEN** el perfil queda con "Bases de datos"
