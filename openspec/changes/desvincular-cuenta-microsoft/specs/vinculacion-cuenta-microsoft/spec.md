## MODIFIED Requirements

### Requirement: Estado de vinculación visible para la administración

La API de administración de usuarios SHALL informar, para cada usuario, si tiene una cuenta Microsoft vinculada y la fecha de su último ingreso. MUST NOT exponer el identificador de objeto ni el tenant de la cuenta.

#### Scenario: Usuario que ya ingresó

- **GIVEN** un usuario que ingresó con Microsoft al menos una vez
- **WHEN** un operador con permiso de consulta de usuarios obtiene el listado o el detalle
- **THEN** el usuario figura con la cuenta vinculada y la fecha de su último ingreso

#### Scenario: Usuario que nunca ingresó

- **GIVEN** un usuario dado de alta que todavía no ingresó
- **WHEN** un operador obtiene el listado
- **THEN** el usuario figura como pendiente de primer ingreso, sin fecha de último ingreso

#### Scenario: Sin datos de la cuenta Microsoft

- **WHEN** se inspecciona la respuesta de la API de usuarios
- **THEN** no contiene el identificador de objeto ni el tenant de ninguna cuenta Microsoft

### Requirement: Desvinculación administrativa de la cuenta Microsoft

Un operador con permiso de administración de usuarios SHALL poder desvincular la cuenta Microsoft de un usuario vinculado, previa confirmación. La desvinculación SHALL vaciar el vínculo y conservar roles, datos personales, estado activo/inactivo y fecha de último ingreso. El próximo ingreso de la persona con un mail verificado que coincida con su UPN SHALL volver a vincularla. La operación MUST controlar concurrencia por versión y quedar registrada en la auditoría con el operador como actor.

#### Scenario: Desvincular una cuenta vinculada

- **GIVEN** un usuario con cuenta Microsoft vinculada y un operador con permiso de administración de usuarios
- **WHEN** el operador confirma la desvinculación
- **THEN** el usuario queda pendiente de primer ingreso, conserva roles, datos y estado, y `audit.change_log` registra el cambio con el operador como actor

#### Scenario: Volver a vincular con otra cuenta

- **GIVEN** un usuario desvinculado
- **WHEN** la persona ingresa con una cuenta Microsoft distinta de la anterior cuyo mail verificado coincide con su UPN
- **THEN** el ingreso es aceptado y la nueva cuenta queda vinculada

#### Scenario: Usuario sin cuenta vinculada

- **GIVEN** un usuario pendiente de primer ingreso
- **WHEN** se solicita desvincular su cuenta
- **THEN** el sistema rechaza la solicitud sin cambios e informa que no tiene una cuenta vinculada

#### Scenario: Versión desactualizada

- **GIVEN** un usuario modificado por otra sesión después de que el operador cargó el listado
- **WHEN** el operador confirma la desvinculación con la versión anterior
- **THEN** el sistema responde un conflicto sin aplicar cambios y la pantalla pide recargar

#### Scenario: Operador sin permiso de administración

- **GIVEN** un usuario autenticado sin permiso de administración de usuarios
- **WHEN** solicita desvincular una cuenta
- **THEN** recibe `403` y no se aplican cambios
