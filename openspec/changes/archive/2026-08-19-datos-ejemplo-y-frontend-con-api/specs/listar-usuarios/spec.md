## ADDED Requirements

### Requirement: Listado desde la identidad persistida

La página de usuarios MUST obtener el listado desde la API de administración y MUST mostrar el estado canónico persistido, sin recurrir a un listado local ante respuestas vacías o fallidas.

#### Scenario: Consulta exitosa

- **GIVEN** usuarios persistidos en `identity`
- **WHEN** un operador autorizado abre `/usuarios`
- **THEN** la tabla muestra los usuarios devueltos por la API con sus personas, roles, ámbitos y estado

#### Scenario: Cambios de otra sesión

- **GIVEN** un usuario modificado por otra sesión
- **WHEN** el operador refresca el listado
- **THEN** la tabla refleja el nuevo estado persistido
