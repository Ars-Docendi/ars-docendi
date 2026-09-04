## ADDED Requirements

### Requirement: Habilidades e intereses como listas separadas

El sistema SHALL ofrecer al docente **dos listas de tags distintas**: **Habilidades**, para lo que sabe hacer, e **Intereses**, para lo que le gustaría hacer. El sistema MUST NOT unificarlas en una sola lista: ante una vacante representan señales distintas —quién puede tomarla ya y quién la tomaría formándose—. Ambas listas MUST usar el mismo vocabulario de tags.

#### Scenario: Las dos listas se administran por separado

- **GIVEN** un docente en su Portal
- **WHEN** visualiza su perfil
- **THEN** ve Habilidades e Intereses como dos secciones distintas, cada una con sus propios tags

#### Scenario: Un mismo término en ambas listas

- **GIVEN** el vocabulario compartido entre ambas listas
- **WHEN** el docente agrega un término como habilidad y el mismo término como interés
- **THEN** el término queda registrado en ambas listas de forma independiente

### Requirement: Administración de tags desde el vocabulario

El sistema SHALL permitir al docente agregar y quitar tags en cada lista, seleccionándolos desde un vocabulario compartido. Los cambios se persisten en el store mock local.

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

### Requirement: Sugerir un término nuevo

El sistema SHALL ofrecer al docente la opción de **sugerir un término nuevo** cuando el que busca no existe en el vocabulario. El término sugerido MUST quedar agregado a la lista del docente, marcado como pendiente de incorporarse al vocabulario.

#### Scenario: Sugerir un término que no está en el vocabulario

- **GIVEN** el docente buscando un término que el vocabulario no tiene
- **WHEN** usa la opción de sugerir uno nuevo e ingresa su nombre
- **THEN** el término se agrega a su lista como tag pendiente de incorporarse al vocabulario

#### Scenario: El término sugerido se comporta como cualquier otro tag

- **GIVEN** un tag sugerido ya agregado
- **WHEN** el docente lo quita
- **THEN** el tag desaparece de su lista
