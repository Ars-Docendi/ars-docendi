## ADDED Requirements

### Requirement: Referencias inmutables de acciones de CI

Todos los workflows de GitHub Actions SHALL referenciar cada acción externa mediante un SHA completo de 40 caracteres. Ningún tag o rama mutable SHALL quedar como referencia ejecutable en CI, deploy o teardown.

#### Scenario: Workflow con acciones fijadas

- **GIVEN** un workflow versionado en el repositorio
- **WHEN** se revisan sus pasos `uses`
- **THEN** cada referencia termina en un SHA completo de 40 caracteres y no en un tag o rama

#### Scenario: Actualización de una acción

- **GIVEN** que se decide actualizar una acción
- **WHEN** se modifica su referencia
- **THEN** el cambio conserva una referencia completa a un commit verificable y no reintroduce un tag mutable

### Requirement: Política de instalación de dependencias del pipeline

La configuración de pnpm del workspace SHALL bloquear subdependencias exóticas, esperar al menos siete días antes de aceptar una versión recién publicada y rechazar downgrades de la política de confianza. Los valores SHALL ser `blockExoticSubdeps: true`, `minimumReleaseAge: 10080` y `trustPolicy: no-downgrade`.

#### Scenario: Configuración de supply chain presente

- **WHEN** el pipeline instala dependencias del workspace
- **THEN** la configuración efectiva contiene las tres políticas con esos valores

#### Scenario: Dependencia recientemente publicada

- **GIVEN** una versión publicada hace menos de 10080 minutos
- **WHEN** pnpm resuelve la instalación
- **THEN** la versión no se acepta por la política de antigüedad mínima
