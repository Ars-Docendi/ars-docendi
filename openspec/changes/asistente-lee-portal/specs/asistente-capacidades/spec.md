## ADDED Requirements

### Requirement: La presentación anuncia lo que el actor puede ejercer

El sistema SHALL derivar de los permisos vigentes del actor qué capacidades del portal anuncia su presentación.

El sistema MUST NOT anunciar la búsqueda de docentes por perfil profesional a un actor que no tiene `portal.ver_trayectoria_ajena`.

La presentación MUST NOT ofrecer el contacto personal ni el archivo del CV.

#### Scenario: Sin el permiso se ofrece el perfil propio

- **GIVEN** un actor sin `portal.ver_trayectoria_ajena`
- **WHEN** pide el catálogo de capacidades
- **THEN** la presentación ofrece consultar su propio perfil y no la búsqueda de docentes

#### Scenario: Con el permiso se ofrece la búsqueda

- **GIVEN** un actor con `portal.ver_trayectoria_ajena`
- **WHEN** pide el catálogo de capacidades
- **THEN** la presentación ofrece buscar docentes por formación, certificaciones o habilidades

#### Scenario: Nunca se promete el contacto ni el CV

- **GIVEN** cualquier actor
- **WHEN** se lee su presentación
- **THEN** no menciona teléfono, mail ni currículum
