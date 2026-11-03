## MODIFIED Requirements

### Requirement: Configuración de inferencia por ambiente

El backend SHALL conectarse al servicio de inferencia compartido con la URL y las credenciales de la configuración `Inferencia` del ambiente. El cliente vive en el módulo del asistente, su único consumidor, y lo usan tanto el estado del servicio como el proveedor `ollama`. Cada petición MUST incluir autenticación BasicAuth con el usuario y la contraseña configurados. Las cabeceras `CF-Access-Client-Id` y `CF-Access-Client-Secret` MUST enviarse sólo cuando ambas están configuradas, y MUST NOT enviarse a otro servicio. El código que consume el cliente MUST NOT depender del ambiente.

#### Scenario: Prod usa la ruta local sin Service Token

- **GIVEN** un backend configurado con URL `http://traefik:11435`, usuario `prod`, contraseña y sin Service Token
- **WHEN** realiza una petición al servicio de inferencia
- **THEN** la petición lleva BasicAuth de `prod` y no lleva cabeceras `CF-Access-*`

#### Scenario: Staging y pr-N usan Cloudflare Access

- **GIVEN** un backend configurado con URL `https://ia.<DOMINIO>`, usuario `staging` o `preview`, contraseña y Service Token
- **WHEN** realiza una petición al servicio de inferencia
- **THEN** la petición lleva BasicAuth del usuario configurado y ambas cabeceras `CF-Access-*`

#### Scenario: Las credenciales no viajan a otro proveedor

- **GIVEN** un backend con `Inferencia` configurada y el proveedor `anthropic`
- **WHEN** el asistente llama al modelo
- **THEN** el pedido a Anthropic no lleva BasicAuth ni cabeceras `CF-Access-*`
