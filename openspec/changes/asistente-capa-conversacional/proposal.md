## Why

El carril SQL responde preguntas sueltas. Cada turno empieza de cero: no hay hilo, no hay forma de preguntar «¿y en Sistemas?», y un «hola» atraviesa el pipeline completo —dos llamadas al modelo, casi tres segundos— para terminar diciéndole al usuario que su mensaje no contiene una consulta.

Este cambio construye la **capa conversacional**, que va **encima** del carril y no adentro. Esa separación es lo que deja intactos el prefijo cacheado, el validador y los datasets: `CarrilSql.ResponderAsync` ya recibe una `preguntaInterpretada`, y lo que esta capa hace es calcularla.

Casi todo es determinista y cuesta **cero tokens**. La única llamada al modelo que agrega es el reescritor, y solo cuando hay historial.

**Dos principios que vienen decididos y no se reabren:**

- **Clasificar la intención con el modelo está descartado con evidencia**: 60% de F1 en triage de cinco clases, 77,4% en nueve vías. Un clasificador que falla una de cada cuatro veces, cuesta una llamada y corta el flujo es peor que una tabla.
- **El cambio de tema se fuerza, no se pide.** Detectarlo no alcanza: hay evidencia de modelos que detectan el pivote y arrastran contexto rancio igual. Al marcar pivote, al reescritor se le pasa historial vacío **por construcción**, no por instrucción del prompt.

## What Changes

- **Hilo conversacional en memoria**, con expiración por inactividad y atado al actor que lo abrió. Un hilo ajeno se rechaza. Guarda **preguntas, nunca filas** — lo que el enmascaramiento sacó del camino de salida no vuelve a entrar por el del historial.
- **Enrutador de intención social y meta**, determinista y a costo cero. Su guarda de precisión es lo que lo hace viable: dispara solo si, quitada la apertura social, **no queda ningún token de contenido**. Así «hola» se captura y «hola, ¿cuántos docentes tiene Inglés Nivel IV?» no.
- **Reconocedor de la respuesta a una aclaración**, en tres pasos —etiqueta completa, token distintivo, ordinal— con una máquina de intentos que tiene tope y salida.
- **Detector de cambio de tema**, determinista, con la guarda del marcador anafórico que evita que «¿y en Sistemas?» se lea como pivote y rompa el caso canónico de seguimiento.
- **Reescritor de preguntas de seguimiento**: una llamada al modelo, solo con historial, con una regla que enumera los campos del dominio y decide **por campo** arrastrar o soltar. Una regla que dijera «conservá todo lo vigente» produce arrastre silencioso.
- **Índice de entidades y detector de ambigüedad**, que resuelven colisiones **con una consulta, no con el modelo**. Es el tercer estado del sistema: no es «no puedo responder», es «puedo en cuanto elijas».
- **Orquestador de la capa**, que compone las seis piezas en el orden que el diseño fija y delega en el carril SQL.

## Capabilities

### New Capabilities

- `asistente-hilo`: almacén de hilos en memoria con expiración, atadura al actor y recorte anclado al inicio del segmento vigente.
- `asistente-intencion-social`: carril sin datos para saludo, agradecimiento y meta-pregunta, resuelto a cero tokens y con guarda de precisión.
- `asistente-aclaracion`: detección de ambigüedad por consulta a la base y reconocimiento de la respuesta del usuario, ambos deterministas.
- `asistente-seguimiento`: reescritura a pregunta autocontenida y detección de cambio de tema que fuerza historial vacío.

### Modified Capabilities

- `asistente-abstencion`: se agrega el cuarto estado del turno, «necesita aclaración», que hasta ahora existía en el enum y no lo producía nadie.

## Impact

- `backend/src/Modules.Asistente/Application/` — el hilo, el enrutador social, la aclaración y su reconocedor, el detector de cambio de tema, el reescritor y el orquestador de la capa.
- `backend/src/Modules.Asistente/Infrastructure/` — el almacén de hilos en memoria y el índice de entidades cargado de la base.
- `backend/src/Modules.Asistente/Application/ResultadoDelTurno.cs` — las opciones de la aclaración y el identificador del hilo.
- `backend/src/Modules.Asistente/ModuleExtensions.cs` — registración de las piezas nuevas.
- `backend/tests/ArsDocendi.IntegrationTests/Asistente/` — las piezas puras se prueban en memoria; el índice y el detector de ambigüedad, contra una base real.
- `docs/architecture/domains/asistente.md` y `backend/src/Modules.Asistente/README.md`.
- **Grafo de dependencias**: sin edges nuevos. El enrutador de dominio, que sí agrega edges hacia `Modules.<X>.Contracts`, es de la épica E6.

## Rollback

Aditivo. El carril SQL no se modifica: la capa lo llama y le pasa la `preguntaInterpretada` que ese contrato ya aceptaba. Quitar la registración de la capa deja el carril funcionando exactamente como antes, con cada turno autocontenido.
