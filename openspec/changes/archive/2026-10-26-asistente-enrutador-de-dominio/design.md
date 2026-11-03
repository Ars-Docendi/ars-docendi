## Contexto

La capa conversacional tiene seis pasos numerados. La definición ubica el enrutador de dominio entre el reescritor y el detector de ambigüedad, y las dos vecindades tienen motivo:

- **Después del reescritor**, porque «¿y el de Pérez?» no tiene slot que resolver hasta que se resuelve la anáfora.
- **Antes del detector de ambigüedad**, porque una pregunta que el catálogo cubre con todos sus slots únicos no es ambigua, y hacerla pasar por el menú sería preguntar por algo que ya está decidido.

## Decisiones

### D1 — El enrutador decide y no ejecuta

Devuelve la intención resuelta o nada. No llama a ninguna API, no arma ninguna respuesta.

Es lo que permite construirlo hoy. Los adaptadores necesitan los edges hacia los `Contracts`, los edges necesitan que el equipo apruebe el checklist de cinco pasos, y esa aprobación conviene pedirla con datos. Separar la decisión de la ejecución es lo que hace que la decisión se pueda medir antes de que exista la ejecución.

### D2 — Modo sombra: la decisión se toma sobre tráfico real, y el turno sigue a SQL

La capa conversacional consulta al enrutador y **registra** lo que habría hecho. Después sigue de largo al carril de siempre.

**La alternativa era no cablearlo hasta tener los adaptadores.** Se descartó porque deja la pregunta importante sin responder: nadie sabe qué proporción del tráfico real captura un catálogo de cinco intenciones, ni cuántas veces se equivoca fuera del banco de pruebas. El pedido de aprobación de los edges se fundamenta con ese número o no se fundamenta.

**El costo aceptado es una rama que decide algo que todavía no cambia nada**, y hay que decirlo en el código o el próximo lector la va a «terminar» conectándola. A cambio: no cambia ninguna respuesta, así que no puede romper nada, y se saca borrando una línea.

### D3 — Términos excluidos, y por qué el banco negativo los hizo necesarios

Una intención puede declarar términos que **no** deben aparecer.

Salió de un falso positivo real: «¿Cuántas solicitudes de baja se presentaron?» caía en `pedidos-de-una-novedad`, porque «solicitudes» normaliza a «pedido» y «baja» es una novedad válida. La intención existe para **listar** los pedidos de una novedad; la pregunta pide **contarlos**. Son dos formas de respuesta distintas, y responder la segunda con la primera es responder mal.

La alternativa —agregar un término positivo que distinga— no existe: «¿qué pedidos de Alta hay?» no tiene ninguna palabra de contenido que «¿cuántos pedidos de Alta hay?» no tenga. Lo único que las separa es justamente la presencia de «cuántos». Un mecanismo declarativo de exclusión es la forma directa de decir eso, y queda en el archivo como todo el resto.

### D4 — El banco negativo sale de los datasets, no de una lista inventada

Las preguntas negativas se leen de `capacidad.json` y `robustez.json`.

**Escribir a mano una lista de preguntas que no hay que capturar sería escribir las que ya sabemos que fallan.** Los datasets los escribió otra tarea con otro objetivo —medir traducción a SQL y tolerancia al fraseo—, así que son preguntas legítimas y ajenas al catálogo. Cuando el catálogo crezca, el banco crece con él sin que nadie lo mantenga, y es el único test que puede fallar por agregar una intención demasiado laxa.

**Costo aceptado**: acopla el módulo a los datasets de evaluación por ruta de archivo. Es un test, no código productivo, y el proyecto de evaluación ya está excluido del CI por otra vía; si los datasets se mueven, el test falla ruidosamente y se arregla la ruta.

### D5 — No captura ≠ error

Una pregunta que no matchea ninguna intención, o que matchea con un slot sin resolver, devuelve «nada» y el turno sigue. No es una excepción ni un estado de error.

Es la misma forma que ya tienen el enrutador social y el detector de ambigüedad, y el motivo es el mismo: el caso normal es no capturar. Un catálogo de cinco intenciones no cubre la mayoría de las preguntas y no pretende hacerlo.

## Riesgos

- **La rama en sombra invita a conectarse antes de tiempo.** Mitigado con el comentario en el punto exacto y con el hecho de que el destino sigue siendo una cadena: conectarla exige agregar los edges, que es un cambio visible en el `.csproj` y en los tests de arquitectura.
- **El banco negativo puede volverse una excusa para no agregar intenciones.** Es lo contrario de lo buscado: el banco no prohíbe crecer, prohíbe crecer con laxitud. Una intención nueva que lo hace fallar está mal declarada, no de más.
