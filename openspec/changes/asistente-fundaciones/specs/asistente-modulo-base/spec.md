## ADDED Requirements

### Requirement: Módulo Asistente registrado por el Host

El sistema SHALL incorporar los proyectos `Modules.Asistente` y `Modules.Asistente.Contracts`, y el Host SHALL registrarlo mediante un método de extensión propio, siguiendo la convención de los módulos existentes.

`Modules.Asistente` MUST depender únicamente de `ArsDocendi.Shared` en este cambio. `Modules.Asistente.Contracts` MUST contener solo DTOs, interfaces o tokens, sin lógica.

#### Scenario: El Host arranca con el módulo registrado

- **WHEN** se levanta el Host con el módulo registrado
- **THEN** la aplicación inicia sin errores de composición

#### Scenario: El módulo no referencia internals ajenos

- **WHEN** corren los tests de arquitectura
- **THEN** ninguna referencia de `Modules.Asistente` apunta a un proyecto `Modules.<Otro>` interno

#### Scenario: El módulo se puede desregistrar

- **WHEN** se quita el registro del módulo de la composición del Host
- **THEN** la aplicación compila y arranca, y ningún otro módulo deja de funcionar

### Requirement: Endpoint de smoke test

El módulo SHALL exponer `GET /api/asistente/ping` con `[AllowAnonymous]`, devolviendo `200`. El endpoint MUST NOT consultar la base de datos ni ningún servicio externo.

#### Scenario: Ping responde sin dependencias

- **GIVEN** el Host levantado con la base de datos detenida
- **WHEN** se hace `GET /api/asistente/ping`
- **THEN** la respuesta es `200`

#### Scenario: Ping no requiere autenticación

- **WHEN** se hace `GET /api/asistente/ping` sin credenciales
- **THEN** la respuesta es `200`

### Requirement: Verificación automatizada de las fronteras del módulo

El sistema SHALL extender los tests de arquitectura para cubrir el módulo nuevo. Los tests MUST verificar que el módulo no escribe en `identity` y que respeta la separación de capas vigente en el resto del sistema.

#### Scenario: El módulo no escribe identidad

- **WHEN** corren los tests de arquitectura
- **THEN** se verifica que `Modules.Asistente` no ejecuta escrituras sobre `identity.personas`, `identity.roles`, `identity.permisos` ni `identity.rol_permisos`

#### Scenario: El invariante nuevo está documentado

- **WHEN** se revisa la documentación de arquitectura del repositorio
- **THEN** el invariante de frontera de motor para consulta generada está escrito, con sus cuatro condiciones verificables y el alcance limitado a este módulo
