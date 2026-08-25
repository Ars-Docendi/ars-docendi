# Diseño — Capa conversacional

## Contexto

`CarrilSql.ResponderAsync(actor, mensaje, preguntaInterpretada, ct)` ya acepta una pregunta autocontenida y devuelve `null` en `PreguntaInterpretada` cuando coincide con el mensaje. Ese parámetro se dejó puesto en el cambio del carril esperando esta capa: lo que se construye acá es quien lo calcula.

El orden del pipeline lo fija §4.2 del documento de definición y no se reordena por conveniencia — cada posición tiene un motivo.

```
resolver hilo
  └─ enrutador social/meta        ← se SALTEA si hay aclaración pendiente
       └─ reconocedor de aclaración
            └─ detector de cambio de tema
                 └─ reescritor    ← única llamada al modelo de esta capa; solo con historial
                      └─ detector de ambigüedad
                           └─ CARRIL SQL
```

## Decisiones

### D1 — El hilo guarda preguntas, nunca filas

El reescritor necesita saber qué se preguntó antes, no qué se respondió. Guardar las filas sería cómodo para dar más contexto y es exactamente lo que no hay que hacer: el cambio anterior sacó los datos personales del camino de salida hacia el proveedor, y guardarlos en el hilo los devolvería al prompt por la puerta del historial —además de contradecir «las filas nunca se persisten», que ya está verificado por test.

Lo que se guarda por turno es la pregunta interpretada y su marca de tiempo. Nada más.

### D2 — El ancla del recorte es el inicio del segmento, no el turno cero

Anclar para siempre el primer turno arrastra contexto muerto: una conversación larga que cambió de tema tres veces seguiría mandándole al reescritor el tema original.

El hilo lleva un **inicio de segmento** que el detector de cambio de tema mueve. El recorte toma los últimos turnos **desde ahí**, así que al pivotar el historial vigente queda vacío sin borrar nada.

### D3 — El pivote se fuerza en el llamador, no se le pide al modelo

Al marcar cambio de tema, al reescritor **no se le pasa historial**. No se le pasa historial y una instrucción que diga «ignoralo»: no se le pasa. La diferencia importa porque hay evidencia de modelos que detectan el pivote y arrastran contexto rancio igual la gran mayoría de las veces.

Consecuencia práctica: es imposible que el reescritor arrastre en un pivote, y el test que lo verifica no mira la salida del modelo sino qué se le mandó.

### D4 — La guarda anafórica antes que la de entidad

El detector marca pivote cuando el mensaje **no** contiene ningún marcador anafórico **y** menciona un término del índice que no está activo en el segmento.

El orden de las condiciones no es estético. «¿y en Sistemas?» menciona un término del catálogo que no está activo, así que la segunda condición se cumple: si la primera no estuviera, el caso canónico de seguimiento se rompería en el turno más común que existe. La guarda anafórica es lo único que lo evita, y por eso se prueba en memoria antes que nada.

### D5 — El enrutador social dispara por ausencia de contenido, no por presencia de saludo

Lo directo sería «si empieza con hola, es un saludo». Rompe con «hola, ¿cuántos docentes tiene Inglés Nivel IV?», que es una forma perfectamente normal de preguntar.

La regla es al revés: se quita la apertura social y se mira **qué queda**. Si no queda ningún token de contenido, era un saludo. Si queda algo, es una pregunta con una apertura cortés y sigue de largo.

Para la meta-pregunta la regla es distinta —tiene que nombrar al asistente: «qué podés hacer», «para qué servís»— y deliberadamente angosta, porque «¿qué carreras hay?» es una pregunta de dominio y confundirla con una meta-pregunta le devolvería al usuario un menú de capacidades en vez de la lista de carreras.

### D6 — Se saltea entero con una aclaración pendiente

Si hay un menú abierto y el usuario contesta «gracias», el enrutador social se lo quedaría y la aclaración quedaría colgada. Con aclaración pendiente el enrutador no corre: el turno va derecho al reconocedor.

### D7 — El reconocedor entrega la etiqueta canónica, no el texto del usuario

Corre **antes** del reescritor, y le entrega la etiqueta exacta del catálogo. Si le pasara el «2» que el usuario tipeó, el reescritor tendría que adivinar a qué se refiere — y adivinar es justo lo que esta capa evita.

Los tres pasos van en orden de especificidad: etiqueta completa, token distintivo, ordinal. Una respuesta que empata con dos opciones **no se resuelve al azar**: se vuelve a preguntar. Al agotarse los intentos la aclaración se abandona y el turno responde que no se pudo determinar a cuál se refería, en vez de quedar colgado para siempre.

### D8 — La ambigüedad se detecta con un `SELECT`, y solo con certeza

Dos clases de colisión en este dominio: nombres de materia que existen en varias carreras, y apellidos compartidos por varias personas. El índice se carga de la base y se cachea; no hay valores hardcodeados.

**No se extiende a la vaguedad**, y es una restricción, no una omisión. Preguntar tiene un costo medido: las aclaraciones de calidad baja y media son peores que no preguntar. El detector dispara solo ante una colisión verificada por consulta; llevarlo a «esta pregunta me parece vaga» lo devuelve al terreno del acierto parcial y le pega justo a la precisión, que es la métrica primaria.

### D9 — El reescritor decide campo por campo

Una regla que diga «conservá todas las restricciones vigentes; si no la menciona, arrastrala» produce **arrastre silencioso**: el filtro del turno anterior angosta el resultado del siguiente y el usuario no se entera.

La regla enumera los campos del dominio —carrera, materia, período, cargo, persona— y decide uno por uno. El prompt lleva un ejemplo que reescribe y **uno que descarta**, porque sin el segundo la única forma demostrada de resolver el prompt es arrastrar.

El ejemplo de descarte no replica ningún turno del dataset de capacidad, verificado por test: un ejemplo copiado del dataset estaría entrenando contra la métrica.

### D10 — El índice de entidades es uno solo

El detector de ambigüedad y el detector de cambio de tema necesitan lo mismo: qué términos del dominio existen y cuáles colisionan. El enrutador de dominio de la épica siguiente va a necesitar el mismo índice para resolver sus slots.

Se construye una vez, se cachea con la misma pereza que el prefijo del prompt —construirlo al arrancar exigiría base durante el arranque, y el invariante #3 pide que el `ping` responda con la base detenida— y lo comparten los tres.

## Qué NO hace este cambio

- **El enrutador de dominio y el carril API.** Son E6, y son los que agregan edges hacia `Modules.<X>.Contracts`.
- **El catálogo de capacidades** que responde la meta-pregunta con datos reales. Es E7: acá la meta-pregunta se resuelve con un texto fijo que no promete nada que el sistema no haga.
- **El endpoint HTTP y el campo `sugerencias`.** También E7.
- **La cuota por actor**, que en §4.2 va antes de resolver el hilo. Es E8.

## Criterios que este cambio no puede verificar

Tres criterios de aceptación de los tickets dependen del eje de evaluación conversacional (ARS-59, H4) **y** de que exista un proveedor de modelo real (TD-008):

| Ticket | Criterio                                                                |
| ------ | ----------------------------------------------------------------------- |
| ARS-42 | «La métrica de arrastre mejora respecto de la línea de base registrada» |
| ARS-43 | «La métrica de arrastre mejora»                                         |
| ARS-43 | «Los diálogos de seguimiento del dataset no se rompen»                  |

No hay línea de base porque no hay corrida, y no hay corrida porque el preflight rechaza al proveedor simulado a propósito. Lo que sí se verifica acá es todo lo demás, incluida la mitad de ARS-39 que sí es comprobable hoy: que el enrutador social no intercepte **ningún** ítem del dataset de capacidad, que existe.
