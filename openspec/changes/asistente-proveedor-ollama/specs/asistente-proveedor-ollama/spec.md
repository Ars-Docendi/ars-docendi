## ADDED Requirements

### Requirement: Adaptador para el servicio de inferencia compartido

El sistema SHALL poder usar como proveedor del modelo el servicio de inferencia compartido (Ollama) por su API nativa, `POST {Inferencia:Url}/api/chat`, con el modelo de `Asistente:Modelo`. El prefijo estable SHALL viajar como mensaje `system` antes del mensaje `user` variable. Cada pedido MUST llevar las credenciales de la sección `Inferencia` del ambiente y MUST NOT llevarlas a ningún otro servicio. Sin `Inferencia:Url`, el proveedor MUST NOT construirse y el error MUST nombrar la opción faltante.

#### Scenario: El prefijo estable va primero y es idéntico entre turnos

- **GIVEN** el proveedor `ollama`
- **WHEN** se hacen dos llamadas con el mismo prefijo y distintas preguntas
- **THEN** el primer mensaje de las dos es el de sistema con el mismo contenido, y la pregunta viaja sólo en el mensaje de usuario

#### Scenario: Credenciales del ambiente

- **GIVEN** un ambiente con `Inferencia:Usuario`, `Inferencia:Contrasena` y Service Token de Cloudflare Access
- **WHEN** el proveedor `ollama` llama al modelo
- **THEN** el pedido lleva BasicAuth de ese usuario y las dos cabeceras `CF-Access-*`

#### Scenario: Sin servicio configurado

- **GIVEN** `Asistente:Proveedor` en `ollama` y `Inferencia:Url` vacía
- **WHEN** se resuelve el proveedor
- **THEN** falla nombrando `Inferencia:Url`

### Requirement: Razonamiento, salida estructurada y contexto

El adaptador `ollama` SHALL pedir `think: false` salvo con esfuerzo alto o máximo, y el texto devuelto MUST NOT incluir bloques de razonamiento. Cuando la llamada declara un esquema de salida, el pedido SHALL llevarlo en `format`. Cuando `Asistente:ContextoDelModelo` es mayor que cero, cada pedido SHALL llevar ese valor como `options.num_ctx`; en cero MUST NOT mandarlo. Todo pedido SHALL llevar `truncate: false`: un prompt que no entra en el contexto MUST llegar como falla y MUST NOT recortarse en silencio.

#### Scenario: El razonamiento se apaga con esfuerzo medio

- **GIVEN** una llamada con esfuerzo `medio`
- **WHEN** el adaptador arma el pedido
- **THEN** el pedido lleva `think: false`

#### Scenario: La generación pide salida con esquema

- **GIVEN** una solicitud con esquema de salida
- **WHEN** el adaptador arma el pedido
- **THEN** el pedido lleva ese esquema en `format`

#### Scenario: Contexto fijo por configuración

- **GIVEN** `Asistente:ContextoDelModelo` en `16384`
- **WHEN** el adaptador arma cualquier pedido
- **THEN** el pedido lleva `options.num_ctx` igual a `16384`

#### Scenario: El prompt no se recorta en silencio

- **GIVEN** el proveedor `ollama`
- **WHEN** el adaptador arma cualquier pedido
- **THEN** el pedido lleva `truncate: false`

### Requirement: Respuesta y fallas del servicio

El adaptador SHALL devolver el texto, los tokens de entrada (`prompt_eval_count`), los leídos de caché (`prompt_eval_cached_count`, nunca más que los de entrada) y los de salida (`eval_count`), y SHALL marcar el corte por techo cuando `done_reason` es `length`. Con quien escuche, SHALL pedir la respuesta por fragmentos NDJSON y entregar el texto visible a medida que llega. Todo estado no exitoso distinto de 429 y 503, un fragmento con `error`, un flujo cortado o sin cierre MUST llegar al pipeline como falla de transporte; 429 y 503 MUST llegar como saturación del proveedor. Los logs MUST NOT incluir el prompt, la URL con credenciales ni los secretos.

#### Scenario: Corte por techo

- **GIVEN** el servicio responde con `done_reason` igual a `length`
- **WHEN** el adaptador traduce la respuesta
- **THEN** la respuesta del puerto indica que se quedó sin tokens

#### Scenario: Tokens reutilizados de la caché de prefijo

- **GIVEN** el servicio responde con `prompt_eval_count` 12000 y `prompt_eval_cached_count` 11000
- **WHEN** el adaptador traduce la respuesta
- **THEN** la respuesta del puerto informa 12000 tokens de entrada y 11000 de caché

#### Scenario: Rechazo de Cloudflare Access o de BasicAuth

- **GIVEN** el servicio responde 401 o 403
- **WHEN** el adaptador traduce la respuesta
- **THEN** el pipeline recibe una falla de transporte con ese estado y el turno degrada sin un 500

#### Scenario: Servicio saturado

- **GIVEN** el servicio responde 429 (Traefik) o 503 (cola de Ollama llena)
- **WHEN** el adaptador traduce la respuesta
- **THEN** el pipeline recibe una saturación del proveedor, no una falla de transporte, y el log la identifica como saturación
- **AND** el corte al proveedor no la cuenta como fallo y el usuario lee que pruebe de nuevo en unos segundos

#### Scenario: Flujo cortado

- **GIVEN** una llamada con quien escuche y un flujo que termina sin el objeto con `done: true`
- **WHEN** el adaptador lo lee
- **THEN** el pipeline recibe una falla de transporte

### Requirement: El servicio compartido cuenta como hardware propio para la redacción

El proveedor `ollama` SHALL contar como hardware propio para `Asistente:RedaccionSinEnmascarar`, igual que `local`: con la opción en `true` y sin directorio de cassettes, el prompt de redacción SHALL llevar el valor real de las columnas `sensible-valor`. Con cassettes MUST seguir enmascarando. Las columnas `sensible-texto` MUST seguir suprimidas. El perfil de deploy del servicio compartido SHALL prender la opción.

#### Scenario: Redacción sin enmascarar con el servicio compartido

- **GIVEN** `Asistente:RedaccionSinEnmascarar` en `true`, el proveedor `ollama` y ningún directorio de cassettes
- **WHEN** arranca la aplicación
- **THEN** la redacción no enmascara los valores `sensible-valor` y el arranque lo registra

#### Scenario: Con cassettes se sigue enmascarando

- **GIVEN** la opción en `true`, el proveedor `ollama` y un directorio de cassettes
- **WHEN** arranca la aplicación
- **THEN** la redacción enmascara y el arranque advierte que la opción no rige

### Requirement: Despliegue con el servicio compartido

Con `ASISTENTE_PROVEEDOR=ollama`, el despliegue SHALL sumar el perfil `compose.asistente-ollama.yml` a toda llamada de compose, y MUST NOT exigir `ASISTENTE_CLAVE`. Si el ambiente no tiene `INFERENCIA_URL`, `INFERENCIA_USUARIO` o `INFERENCIA_CONTRASENA`, SHALL degradar a `simulado`. Un preview de PR con la label `asistente-ollama` SHALL desplegarse con el proveedor `ollama`.

#### Scenario: Preview con la label

- **GIVEN** un PR con las labels `deploy-preview` y `asistente-ollama` aprobado en el Environment `pr-preview`
- **WHEN** se despliega su ambiente
- **THEN** el backend arranca con el proveedor `ollama`, el perfil del servicio compartido y las credenciales `preview`

#### Scenario: Sin servicio de inferencia

- **GIVEN** `ASISTENTE_PROVEEDOR=ollama` sin `INFERENCIA_URL`
- **WHEN** corre el despliegue
- **THEN** el ambiente queda con el proveedor `simulado`
