## 1. Configuración y selección (TD-008)

- [ ] 1.1 `OpcionesAsistente` — `ClaveDelProveedor`, `Modelo` y `Esfuerzo`, con default
- [ ] 1.2 `ProveedorAnthropic.Clave` = `anthropic`, brazo nuevo del `switch` de `ModuleExtensions`
- [ ] 1.3 La clave se valida dentro de la fábrica, con un mensaje que nombra el valor faltante
- [ ] 1.4 El adaptador recibe el cliente HTTP con nombre `asistente-proveedor`, que ya trae el reintento
- [ ] 1.5 Test: sin configurar nada, el proveedor resuelto es el simulado
- [ ] 1.6 Test: configurado `anthropic` con clave, el proveedor resuelto no es simulado
- [ ] 1.7 Test: sin clave, pedir el proveedor falla con un mensaje que la nombra
- [ ] 1.8 Test: sin clave, el Host arranca igual y el ping responde
- [ ] 1.9 Test: un proveedor desconocido no impide arrancar

## 2. El adaptador (TD-008)

- [ ] 2.1 `Infrastructure/ProveedorAnthropic.cs` — implementa `IProveedorDeModelo`
- [ ] 2.2 `MaxRetries = 0` en el cliente del SDK, con el motivo escrito al lado
- [ ] 2.3 El prefijo estable va como bloque `system` con `CacheControlEphemeral`
- [ ] 2.4 El mensaje del turno va como único mensaje de usuario
- [ ] 2.5 `MaximoDeTokens` → `MaxTokens`; `Esfuerzo` → `OutputConfig.Effort`
- [ ] 2.6 La temperatura no se envía; el comentario de `SolicitudAlModelo.Temperatura` se corrige
- [ ] 2.7 Los conteos salen de `Usage`, sin estimar
- [ ] 2.8 Test: el prefijo viaja marcado para cachear
- [ ] 2.9 Test: dos turnos con la misma base comparten bloque de sistema idéntico
- [ ] 2.10 Test: el request no lleva temperatura
- [ ] 2.11 Test: los conteos son los que informó el proveedor
- [ ] 2.12 Verificar en rojo: mandar el prefijo como texto común hace fallar 2.8

## 3. Traducción de fallas (TD-008)

- [ ] 3.1 Error de servidor y límite de tasa → `HttpRequestException`
- [ ] 3.2 Credencial rechazada → log `Error` que nombra la causa, después `HttpRequestException`
- [ ] 3.3 Request mal armado → log `Error`, después `HttpRequestException`
- [ ] 3.4 La cancelación del token propio del breaker se deja pasar sin atrapar
- [ ] 3.5 Respuesta sin bloque de texto → texto vacío, nunca excepción
- [ ] 3.6 El rehúso se registra en `Warning` con su categoría
- [ ] 3.7 Test: un error de servidor llega como falla de transporte del módulo
- [ ] 3.8 Test: un límite de tasa llega como falla de transporte del módulo
- [ ] 3.9 Test: fallas repetidas abren el corte y el turno degrada
- [ ] 3.10 Test: el timeout de la llamada sigue llegando como `TimeoutDelProveedor`
- [ ] 3.11 Test: con la credencial rechazada el turno degrada y no rompe
- [ ] 3.12 Test: la credencial rechazada queda registrada en `Error`
- [ ] 3.13 Test: una respuesta sin texto no cuenta como fallo del corte
- [ ] 3.14 Test: el adaptador hace un solo intento ante una falla de transporte
- [ ] 3.15 Verificar en rojo: dejar escapar la excepción del SDK hace fallar 3.9

## 4. Higiene de la credencial

- [ ] 4.1 La clave no entra en ningún `appsettings.json` versionado
- [ ] 4.2 Test: ningún archivo de configuración versionado contiene la clave
- [ ] 4.3 Test: el registro operativo guarda el nombre del proveedor y ninguna credencial

## 5. Documentación

- [ ] 5.1 `docs/architecture/domains/asistente.md` — el adaptador y cómo se elige por ambiente
- [ ] 5.2 `docs/quality/tech-debt.md` — TD-008 pasa a la mitad que queda: elegir y pagar
- [ ] 5.3 README del módulo — cómo correrlo con clave real en desarrollo
- [ ] 5.4 Las cuatro variables de ambiente nuevas, documentadas
