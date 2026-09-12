## Why

El asistente responde, se acota por permisos, conversa y se degrada sin caerse. Lo que no tiene es **puerta**: `CapaConversacional.ResponderAsync` es un método que nadie puede llamar desde afuera. El único endpoint del módulo es el ping.

Y le falta la otra mitad del producto. Una caja de texto libre sin descubrimiento es una falsa promesa: el usuario no sabe qué preguntar, y averiguarlo le cuesta un turno que termina en rechazo. Hoy, un rechazo dice «no puedo» y se acaba ahí.

Tres huecos:

1. **El contrato de respuesta está a medias.** Los cuatro estados existen, pero el rechazo cooperativo no: falta el campo de **sugerencias**, distinto del de opciones. Y falta la consulta generada detrás de permiso, que es la única forma de que alguien pueda auditar qué hizo el asistente sin leer los logs del servidor.
2. **No hay endpoint.** Sin `Idempotency-Key`, además, un doble clic cuesta dos o tres llamadas al modelo pagadas dos veces.
3. **No hay catálogo de capacidades.** Es la respuesta a «¿qué podés hacer?», y hoy el enrutador social contesta esa meta-pregunta con un texto fijo escrito a mano.

## What Changes

- **Sugerencias en el contrato de respuesta**, campo separado de las opciones. No es un detalle de nombres: las opciones **bloquean** el turno esperando una elección y las sugerencias son próximos pasos que no bloquean nada. Un solo campo para las dos cosas borra el tercer estado.
- **La consulta generada detrás de un permiso nuevo**, `asistente.ver_consulta`, sembrado y **concedido a ningún rol por defecto**. Se otorga desde la administración de membresías, sin desplegar.
- **`POST /api/asistente/consultas`**, protegido por el permiso del asistente, con `Idempotency-Key` obligatoria resuelta **en memoria con expiración corta** y acotada por actor.
- **`GET /api/asistente/capacidades`**, derivado de los **GRANT efectivos** del rol del actor y nunca del payload del prompt. Con qué cubre y sus conteos, ejemplos ejecutables verificados contra los privilegios del actor por el propio motor, y qué **no** puede responder.
- **La meta-pregunta deja de responderse con un texto fijo**: el enrutador social pasa a devolver el catálogo real.

## Capabilities

### New Capabilities

- `asistente-contrato-de-respuesta`: los cuatro estados con opciones y sugerencias separadas, la pregunta interpretada condicional y la consulta detrás de permiso.
- `asistente-endpoint-consultas`: la puerta del turno, con permiso e idempotencia en memoria.
- `asistente-capacidades`: el catálogo por actor derivado de los privilegios efectivos.

### Modified Capabilities

- `asistente-abstencion`: todo rechazo pasa a traer al menos una sugerencia accionable, tomada del catálogo de ejemplos verificados.
- `asistente-intencion-social`: la meta-pregunta se responde con el catálogo real en lugar de un texto fijo.

## Impact

- `backend/src/Modules.Asistente/Application/` — el contrato de respuesta, las sugerencias y el catálogo de capacidades.
- `backend/src/Modules.Asistente/Api/` — el endpoint del turno, el de capacidades y sus modelos.
- `backend/src/Modules.Asistente/Infrastructure/` — el catálogo derivado del motor y la caché de idempotencia.
- `backend/src/ArsDocendi.Shared/Auth/Permisos.cs` y `database/identity/` — el permiso nuevo y su siembra.
- `backend/tests/ArsDocendi.IntegrationTests/Asistente/` — el contrato y las capacidades contra base real; la idempotencia en memoria.
- `docs/architecture/api-contracts.md`, `docs/architecture/domains/asistente.md` y el README del módulo.
- **Grafo de dependencias**: sin edges nuevos.

## Rollback

Aditivo salvo el permiso, que se revierte con el `Down` de su migración. Quitar los dos endpoints deja el módulo exactamente como estaba: la capa conversacional sigue siendo invocable desde los tests, y ninguna otra pieza depende de la superficie HTTP.
