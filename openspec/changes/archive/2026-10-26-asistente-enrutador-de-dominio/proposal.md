## Why

El cambio anterior —[`asistente-catalogo-de-intenciones`](../asistente-catalogo-de-intenciones/proposal.md)— dejó construido el reconocimiento: dado un texto, qué intención del catálogo cerrado lo cubre y con qué slots. Lo que falta es **quién lo consulta y qué hace con la respuesta**.

Este cambio construye la decisión de carril. Corre **después del reescritor**, porque enrutar «¿y el de Pérez?» sin resolver la anáfora es imposible, y **antes del detector de ambigüedad**, que es donde la definición lo ubica.

**El default es SQL, nunca API, y no es una preferencia.** Enrutar mal hacia la API devuelve cero filas, y «cero filas» es indistinguible de «no hay» — exactamente la mentira que la política de abstención existe para prohibir. Fallar hacia el carril más caro es fallar hacia el carril que **puede** responder.

**Y el cambio trae la prueba que hace honesto al catálogo.** Un banco de preguntas negativas tomadas de los datasets de capacidad y de robustez: preguntas legítimas que el enrutador **no** debe capturar. Ya encontró una —«¿Cuántas solicitudes de baja se presentaron?» caía en la intención de novedad, porque «solicitudes» normaliza a «pedido» y «baja» es una novedad—, y esa es exactamente la clase de error que un catálogo sin banco negativo acumula en silencio.

## What Changes

- **Enrutador de dominio**: decide el carril de cada turno y devuelve la intención resuelta o la decisión de seguir a SQL. No ejecuta nada.
- **Términos excluidos en el catálogo**: una intención puede declarar términos que **no** deben aparecer. Es lo que separa «¿qué pedidos de Alta hay?» de «¿cuántas solicitudes de baja se presentaron?»: la segunda es una pregunta de conteo, con otra forma de respuesta, y capturarla con la intención de listado la respondería mal.
- **Banco de preguntas negativas** sobre los datasets de capacidad y robustez, ejecutado como test: si el enrutador captura una, falla nombrándola.
- **Cableado en la capa conversacional, en modo sombra**: la decisión se toma y se registra, y el turno sigue al carril SQL. No hay a dónde enrutar todavía —los adaptadores y los edges son el trabajo siguiente— y un carril a medio conectar sería peor que ninguno.

## Capabilities

### New Capabilities

- `asistente-enrutador-dominio`: decisión de carril determinista, con default seguro a SQL, banco de preguntas negativas y observación de lo que se habría enrutado.

### Modified Capabilities

- `asistente-catalogo-intenciones`: una intención puede declarar términos excluidos además de los exigidos.

## Out of Scope

- **Los adaptadores de respuesta** y cualquier llamada real a la API del sistema.
- **Los edges hacia los `Contracts`** (ARS-46), que requieren acuerdo del equipo.

## Por qué modo sombra y no un carril a medias

Enrutar de verdad exige los edges, y los edges exigen que el equipo apruebe el checklist de cinco pasos. Pedir esa aprobación sin datos es pedirla a ciegas.

En modo sombra la decisión se toma sobre tráfico real y se registra, el turno se resuelve como siempre por SQL, y el resultado es la evidencia con la que ese pedido se puede fundamentar: cuántas preguntas reales habría capturado el catálogo y cuántas veces se habría equivocado. Es también reversible sin costo, porque no cambia ninguna respuesta.
