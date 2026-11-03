## 1. Configuración y selección (TD-008)

- [x] 1.1 `OpcionesAsistente` — `ClaveDelProveedor`, `Modelo` y `Esfuerzo`, con default
- [x] 1.2 `ProveedorAnthropic.Clave` = `anthropic`, brazo nuevo del `switch` de `ModuleExtensions`
- [x] 1.3 La clave se valida dentro de la fábrica, con un mensaje que nombra el valor faltante
- [x] 1.4 El adaptador recibe el cliente HTTP con nombre `asistente-proveedor`, que ya trae el reintento
- [x] 1.5 Test: sin configurar nada, el proveedor resuelto es el simulado
- [x] 1.6 Test: configurado `anthropic` con clave, el proveedor resuelto no es simulado
- [x] 1.7 Test: sin clave, pedir el proveedor falla con un mensaje que la nombra
- [x] 1.8 Test: sin clave, el Host arranca igual y el ping responde
- [x] 1.9 Test: un proveedor desconocido no impide arrancar

## 2. El adaptador (TD-008)

- [x] 2.1 `Infrastructure/ProveedorAnthropic.cs` — implementa `IProveedorDeModelo`
- [x] 2.2 `MaxRetries = 0` en el cliente del SDK, con el motivo escrito al lado
- [x] 2.3 El prefijo estable va como bloque `system` con `CacheControlEphemeral`
- [x] 2.4 El mensaje del turno va como único mensaje de usuario
- [x] 2.5 `MaximoDeTokens` → `MaxTokens`; `Esfuerzo` → `OutputConfig.Effort`
- [x] 2.6 La temperatura no se envía; el comentario de `SolicitudAlModelo.Temperatura` se corrige
- [x] 2.7 Los conteos salen de `Usage`, sin estimar
- [x] 2.8 Test: el prefijo viaja marcado para cachear
- [x] 2.9 Test: dos turnos con la misma base comparten bloque de sistema idéntico
- [x] 2.10 Test: el request no lleva temperatura
- [x] 2.11 Test: los conteos son los que informó el proveedor
- [x] 2.12 Verificar en rojo: mandar el prefijo como texto común hace fallar 2.8

## 3. Traducción de fallas (TD-008)

- [x] 3.1 Error de servidor y límite de tasa → `HttpRequestException`
- [x] 3.2 Credencial rechazada → log `Error` que nombra la causa, después `HttpRequestException`
- [x] 3.3 Request mal armado → log `Error`, después `HttpRequestException`
- [x] 3.4 La cancelación del token propio del breaker se deja pasar sin atrapar
- [x] 3.5 Respuesta sin bloque de texto → texto vacío, nunca excepción
- [x] 3.6 El rehúso se registra en `Warning` con su categoría
- [x] 3.7 Test: un error de servidor llega como falla de transporte del módulo
- [x] 3.8 Test: un límite de tasa llega como falla de transporte del módulo
- [x] 3.9 Test: fallas repetidas abren el corte y el turno degrada
- [x] 3.10 Test: el timeout de la llamada sigue llegando como `TimeoutDelProveedor`
- [x] 3.11 Test: con la credencial rechazada el turno degrada y no rompe
- [x] 3.12 Test: la credencial rechazada queda registrada en `Error`
- [x] 3.13 Test: una respuesta sin texto no cuenta como fallo del corte
- [x] 3.14 Test: el adaptador hace un solo intento ante una falla de transporte
- [x] 3.15 Verificar en rojo: dejar escapar la excepción del SDK hace fallar 3.9

## 4. Higiene de la credencial

- [x] 4.1 La clave no entra en ningún `appsettings.json` versionado
- [x] 4.2 Test: ningún archivo de configuración versionado contiene la clave
- [x] 4.3 Test: el registro operativo guarda el nombre del proveedor y ninguna credencial
- [x] 4.4 `registro_operativo.proveedor` — la columna que lo hace posible, sin `ALTER`
- [x] 4.5 Test: el nombre del proveedor NO viaja al registro analítico

## 5. Documentación

- [x] 5.1 `docs/architecture/domains/asistente.md` — el adaptador y cómo se elige por ambiente
- [x] 5.2 `docs/quality/tech-debt.md` — TD-008 pasa a la mitad que queda: elegir y pagar
- [x] 5.3 README del módulo — cómo correrlo con clave real en desarrollo
- [x] 5.4 Las cuatro variables de ambiente nuevas, documentadas
