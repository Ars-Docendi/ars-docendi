## ADDED Requirements

### Requirement: Permiso persistido para consultar el asistente

El sistema SHALL agregar al catálogo `identity.permisos` el código `asistente.consultar`, con nombre y descripción en español. El permiso MUST declararse como constante en `Permisos` y MUST estar incluido en `Permisos.Todos` para que la política de autorización quede registrada al arrancar el Host.

La membresía del permiso MUST ser administrable desde la superficie de administración de roles sin requerir una migración.

#### Scenario: El permiso existe en el catálogo

- **WHEN** se consulta `identity.permisos` después de aplicar las migraciones
- **THEN** existe exactamente una fila con `code = 'asistente.consultar'`

#### Scenario: La política queda registrada al arrancar

- **GIVEN** el Host compuesto
- **WHEN** un endpoint declara `[Authorize(Policy = Permisos.AsistenteConsultar)]`
- **AND** llega un request autenticado
- **THEN** la autorización se evalúa sin lanzar excepción por política inexistente

#### Scenario: Secretaría concede el permiso sin migración

- **GIVEN** un rol que no tiene `asistente.consultar`
- **WHEN** un usuario con `roles.gestionar_membresia` le concede el permiso desde la superficie de administración
- **THEN** los usuarios de ese rol pasan a estar autorizados sin desplegar ni migrar

#### Scenario: Secretaría revoca el permiso sin migración

- **GIVEN** un rol que tiene `asistente.consultar`
- **WHEN** un usuario con `roles.gestionar_membresia` se lo revoca
- **THEN** los usuarios de ese rol dejan de estar autorizados sin desplegar ni migrar

### Requirement: Siembra explícita para los siete roles de sistema

La migración que agrega el permiso SHALL sembrar su membresía de forma explícita para cada uno de los siete roles de sistema. La migración MUST NOT depender de que un rol herede el permiso por haberse sembrado con el catálogo completo en una migración anterior.

El valor inicial SHALL conceder el permiso a los seis roles no `docente` y NO concederlo a `docente`, alineado con la matriz vigente donde `docente` carece de `designaciones.ver`.

#### Scenario: sys_admin recibe el permiso explícitamente

- **WHEN** se consulta la membresía del rol `sys_admin` después de la migración
- **THEN** incluye `asistente.consultar`

#### Scenario: Los roles de gestión reciben el permiso

- **WHEN** se consulta la membresía de `jefe_catedra`, `coordinador_carrera`, `secretaria`, `decanato` y `administrativo`
- **THEN** cada uno incluye `asistente.consultar`

#### Scenario: El rol docente no recibe el permiso

- **WHEN** se consulta la membresía del rol `docente`
- **THEN** no incluye `asistente.consultar`

#### Scenario: La migración es idempotente

- **WHEN** la migración se aplica sobre una base que ya la tiene aplicada
- **THEN** termina sin error y no duplica filas en `identity.rol_permisos`
