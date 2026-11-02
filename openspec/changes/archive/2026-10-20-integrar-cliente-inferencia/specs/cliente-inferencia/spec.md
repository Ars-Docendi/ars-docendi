## ADDED Requirements

### Requirement: Configuración de inferencia por ambiente

El backend SHALL conectarse al servicio de inferencia compartido mediante un único cliente HTTP cuya URL y credenciales provienen de la configuración `Inferencia` del ambiente. Cada petición MUST incluir autenticación BasicAuth con el usuario y la contraseña configurados. Las cabeceras `CF-Access-Client-Id` y `CF-Access-Client-Secret` MUST enviarse sólo cuando ambas están configuradas. El código que consume el cliente MUST NOT depender del ambiente.

#### Scenario: Prod usa la ruta local sin Service Token

- **GIVEN** un backend configurado con URL `http://traefik:11435`, usuario `prod`, contraseña y sin Service Token
- **WHEN** realiza una petición al servicio de inferencia
- **THEN** la petición lleva BasicAuth de `prod` y no lleva cabeceras `CF-Access-*`

#### Scenario: Staging y pr-N usan Cloudflare Access

- **GIVEN** un backend configurado con URL `https://ia.<DOMINIO>`, usuario `staging` o `preview`, contraseña y Service Token
- **WHEN** realiza una petición al servicio de inferencia
- **THEN** la petición lleva BasicAuth del usuario configurado y ambas cabeceras `CF-Access-*`

### Requirement: Inferencia deshabilitada sin URL

Cuando `Inferencia:Url` está vacía, la inferencia SHALL considerarse deshabilitada: el backend MUST arrancar normalmente y MUST NOT realizar peticiones al servicio. Fuera del ambiente Development, una URL configurada sin usuario o sin contraseña, o con sólo una de las dos cabeceras de Service Token, MUST impedir el arranque con un error que no incluya valores secretos.

#### Scenario: Ambiente sin inferencia configurada

- **GIVEN** un backend sin `Inferencia:Url`
- **WHEN** arranca y se consulta el estado de inferencia
- **THEN** el backend responde normalmente, informa `deshabilitado` y no se realiza ninguna petición saliente

#### Scenario: Configuración incompleta fuera de Development

- **GIVEN** un backend en ambiente Production con `Inferencia:Url` configurada y sin contraseña
- **WHEN** arranca
- **THEN** el arranque falla indicando qué opción falta, sin mostrar valores de configuración

### Requirement: Estado seguro de la inferencia

El sistema SHALL ofrecer `GET /api/inferencia/estado` a usuarios con el permiso `sistema.estado.ver`. La respuesta MUST contener sólo `estado` (`deshabilitado`, `disponible` o `no_disponible`), `comprobadoEn` y `duracionMs`. La comprobación MUST consultar la versión del servicio con un tiempo límite de 3 segundos. Una falla de red, autenticación, tiempo límite o respuesta no exitosa MUST informarse como `no_disponible` sin exponer URL, usuario, credenciales, cabeceras ni detalles de la excepción.

#### Scenario: Servicio disponible

- **GIVEN** un usuario con `sistema.estado.ver` y el servicio de inferencia respondiendo con éxito
- **WHEN** consulta el estado de inferencia
- **THEN** recibe `disponible`, la hora de comprobación y la duración

#### Scenario: Servicio rechaza o no responde

- **GIVEN** un usuario con `sistema.estado.ver` y el servicio respondiendo 401, 403, 5xx o excediendo el tiempo límite
- **WHEN** consulta el estado de inferencia
- **THEN** recibe `no_disponible` y la respuesta no contiene la URL, el usuario ni mensajes internos

#### Scenario: Consulta sin permiso

- **GIVEN** un usuario autenticado sin `sistema.estado.ver`
- **WHEN** consulta el estado de inferencia
- **THEN** el backend responde 403 sin contactar el servicio

### Requirement: Credenciales de inferencia por GitHub Environment

Los despliegues SHALL entregar las opciones de inferencia desde la configuración del GitHub Environment de cada ambiente: `prod` con su contraseña y sin Service Token; `staging` y `pr-preview` con su contraseña propia y el Service Token. Los valores secretos MUST NOT versionarse, imprimirse en logs de CI ni escribirse en archivos del repositorio.

#### Scenario: Deploy de prod

- **GIVEN** el workflow de deploy de prod con los secrets del Environment `prod`
- **WHEN** levanta el backend
- **THEN** el backend recibe URL local, usuario `prod` y su contraseña, y ninguna credencial de Cloudflare Access

#### Scenario: Deploy de un preview

- **GIVEN** el workflow de pr-N aprobado en el Environment `pr-preview`
- **WHEN** levanta el backend
- **THEN** el backend recibe URL `https://ia.<DOMINIO>`, usuario `preview`, su contraseña y el Service Token
