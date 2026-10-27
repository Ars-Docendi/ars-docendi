## ADDED Requirements

### Requirement: El servicio degradado se decide antes del pipeline

El sistema SHALL determinar la disponibilidad del modelo antes de iniciar el turno, y SHALL seguir ejecutando los pasos que no necesitan proveedor aunque el modelo no esté disponible.

El sistema MUST NOT abortar el turno completo por falta de modelo.

#### Scenario: El veredicto se toma una sola vez por turno

- **GIVEN** un turno que empieza con el modelo disponible
- **WHEN** el proveedor se cae a mitad del turno
- **THEN** el turno resuelve como servicio degradado y no reintenta indefinidamente

#### Scenario: Los pasos deterministas corren igual

- **GIVEN** un turno que empieza sin modelo disponible
- **WHEN** el mensaje se resuelve por un paso que no necesita proveedor
- **THEN** el turno responde normalmente y no como servicio degradado

#### Scenario: El estado degradado se distingue del no contestable

- **GIVEN** un turno resuelto como servicio degradado
- **WHEN** el cliente lee el estado
- **THEN** es distinguible de «no contestable», que significa que la pregunta no se puede responder nunca
