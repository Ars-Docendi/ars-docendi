## Why

El asistente está entero salvo una cosa: **no habla con ningún modelo**. `ModuleExtensions` sabe construir un solo proveedor, el simulado, y ese cliente devuelve a propósito un texto que dice que es simulado y que no debe presentarse como respuesta del sistema. Se puede levantar el sistema, entrar, escribir una pregunta — y eso es lo que llega.

Es la deuda TD-008, y no es de ingeniería: el pipeline no cambia. Lo que falta es elegir un proveedor, tener una clave y escribir el adaptador.

**El puerto ya es el seam agnóstico y no hace falta refactorizar nada para tener varios.** `IProveedorDeModelo` no menciona ningún proveedor —`PrefijoEstable`, `Mensaje`, `Temperatura`, `MaximoDeTokens` de ida; texto y conteo de tokens de vuelta— y el registro ya es un `switch` sobre `Asistente__Proveedor`, elegido por ambiente. Este change suma **un brazo**. Un modelo local o cualquier otro proveedor será otro brazo, sin tocar el pipeline.

Todavía no está decidido con qué se va a correr en producción. Por eso el objetivo acá no es «adoptar Anthropic», es **probar que el seam sostiene un adaptador real** y dejar el primero andando.

## What Changes

- **`ProveedorAnthropic`**, adaptador de `IProveedorDeModelo` contra el SDK oficial de C# (paquete `Anthropic`), seleccionable con `Asistente__Proveedor=anthropic`. El simulado sigue siendo el default de todos los ambientes.
- **El prefijo estable viaja como bloque `system` cacheable.** Es el bloque más grande del prompt y el diseño ya lo trata como estable; sin marcarlo, el ahorro que el diseño asume no ocurre.
- **El adaptador no reintenta.** El SDK reintenta por defecto; se apaga. El reintento con backoff y jitter ya vive en `ReintentoDeTransporte`, registrado en el cliente HTTP con nombre que este adaptador consume.
- **Toda falla del proveedor se traduce al vocabulario del módulo** antes de salir del adaptador. Ninguna excepción del SDK llega al pipeline.
- **Configuración nueva**: la clave, el modelo y el esfuerzo, por ambiente. La clave nunca se escribe al repositorio ni aparece en un log.
- **Corrección de documentación**: el comentario de `SolicitudAlModelo.Temperatura` deja de prometer un comportamiento que este adaptador no puede dar.

## Capabilities

### New Capabilities

- `asistente-proveedor-anthropic`: el primer adaptador real del puerto, con su selección por ambiente, su traducción de fallas y su disciplina de reintento y de caché.

## Impact

- `backend/src/Modules.Asistente/Infrastructure/ProveedorAnthropic.cs` — el adaptador.
- `backend/src/Modules.Asistente/ModuleExtensions.cs` — el brazo nuevo del `switch` y el cliente HTTP que le llega.
- `backend/src/Modules.Asistente/OpcionesAsistente.cs` — clave, modelo y esfuerzo.
- `backend/src/Modules.Asistente/Application/IProveedorDeModelo.cs` — solo el comentario de `Temperatura`.
- `backend/src/Modules.Asistente/Modules.Asistente.csproj` — paquete `Anthropic`.
- `backend/tests/ArsDocendi.IntegrationTests/Asistente/` — los tests del adaptador, contra un transporte falso y **sin clave real**.
- `docs/architecture/domains/asistente.md`, `docs/quality/tech-debt.md` (TD-008).
- **Grafo de dependencias**: sin edges nuevos. Es una dependencia de paquete, no un edge entre módulos.

## Rollback

Aditivo por completo. El default sigue siendo el simulado, así que un ambiente que no configure `Asistente__Proveedor=anthropic` se comporta exactamente como hoy. Quitar el adaptador es borrar una clase y un brazo del `switch`.
