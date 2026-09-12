# Diseño — La superficie de usuario del asistente

## Contexto

`CapaConversacional.ResponderAsync(actor, hilo, mensaje, ct)` devuelve un `ResultadoDelTurno` con los cuatro estados, las opciones de la aclaración y el identificador del hilo. Lo que falta es exponerlo y completarlo.

## Decisiones

### D1 — Opciones y sugerencias son dos campos, y colapsarlos rompe el tercer estado

Las **opciones** bloquean: el turno terminó en «necesita aclaración» y espera una elección. Las **sugerencias** no bloquean: son próximos pasos, y el turno ya terminó.

Con un solo campo, la interfaz no puede distinguir «elegí una de estas y sigo» de «probá con alguna de estas otras preguntas», así que tiene que adivinar por el estado — y el día que un turno respondido quiera sugerir algo, la distinción se pierde del todo.

### D2 — Las sugerencias salen del catálogo de ejemplos verificados, no del modelo

Pedirle sugerencias al modelo costaría una llamada más justo en el camino donde el sistema ya decidió que no puede responder, y produciría preguntas que **no** se sabe si funcionan. El catálogo de ejemplos ya tiene pares pregunta-consulta verificados: sus preguntas son, por construcción, cosas que el asistente sabe hacer.

Se eligen por parecido léxico con la pregunta que falló, con el mismo selector que arma el prompt. Cuando ninguna se parece lo suficiente, se devuelven las de arranque del catálogo: una sugerencia genérica pero ejecutable es mejor que ninguna.

### D3 — La consulta generada va detrás de un permiso propio, y nadie lo tiene por defecto

`asistente.ver_consulta` se siembra y **no se concede a ningún rol**. Se otorga desde la administración de membresías, sin desplegar, igual que `asistente.consultar`.

Ninguno por defecto y no «Secretaría» por defecto: la consulta generada es superficie de diagnóstico, su `WHERE` puede llevar un documento, y quién necesita verla es una decisión del Departamento y no del que escribe la migración. Un permiso concedido de arranque es difícil de quitar; uno vacío se concede en treinta segundos cuando alguien lo pide.

Reusar `asistente.consultar` estaba descartado por el mismo argumento con que se descartó reusar `designaciones.ver`: dos decisiones distintas necesitan dos interruptores distintos.

### D4 — La idempotencia vive en memoria y no reusa la tabla de designaciones

`designaciones.idempotencia_comandos` guarda el `response_body` completo, que es exactamente lo que este módulo decidió no persistir. Copiar esa tabla acá metería las filas devueltas en una tabla sin política de retención, por la puerta de atrás.

La caché en memoria con expiración corta alcanza para lo que el requisito realmente pide —el doble clic—, no sobrevive al redespliegue, y es coherente con no persistir el hilo.

**La clave se acota por actor.** Sin eso, la `Idempotency-Key` de un usuario le devolvería a otro una respuesta calculada con el alcance del primero: un canal de fuga trivial de disparar y difícil de notar.

### D5 — El catálogo se deriva de los GRANT efectivos, y ésa es la restricción entera

> El esquema se inyecta **entero** en el prompt, columnas personales incluidas.

Un catálogo derivado de ese payload ofrecería preguntas sobre columnas que el rol del usuario no puede leer. La consulta terminaría en `permission denied`, pero el daño ya está hecho: el catálogo le habría dicho al usuario que esos datos existen y que el asistente los tiene.

Se deriva de `LectorDeCatalogo`, que le pregunta a la base «¿qué puedo leer **yo**?» con `has_column_privilege` contra `current_user`. Los dos roles obtienen catálogos distintos sin que este código sepa nada de ellos.

### D6 — Los ejemplos los valida el motor, no una lista

Cada ejemplo candidato se pasa por `EXPLAIN` con la conexión del actor. `EXPLAIN` sin `ANALYZE` arranca el ejecutor y por lo tanto **chequea privilegios**, pero no lee ninguna fila: si el ejemplo toca una columna que el rol no puede leer, el motor lo rechaza con `42501` y el ejemplo se descarta.

Es más caro que mirar una lista y es lo correcto: la lista se desincroniza del `GRANT` en silencio, y el modo de falla de esa desincronización es ofrecerle al usuario una pregunta que no puede hacer.

### D7 — El alcance se dice aparte de las capacidades

El ámbito del actor —global, carrera, materia— cambia **qué filas ve**, no **qué puede preguntar**. Meterlo en los conteos los haría mentir en las dos direcciones.

Va como una línea propia, para que quien lea el catálogo entienda por qué su compañero ve otros números en la misma pregunta.

### D8 — La meta-pregunta deja de tener texto fijo

El enrutador social contesta «¿qué podés hacer?» con un texto escrito a mano. Ese texto es, literalmente, una promesa sobre capacidades que nadie verifica.

Pasa a devolver el catálogo real, con sus conteos y sus ejemplos ejecutables. Sigue costando **cero tokens**: el catálogo sale de la base y del catálogo de ejemplos, no del modelo.

## Alternativas descartadas

| Alternativa                                           | Por qué no                                                                                       |
| ----------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| Sugerencias generadas por el modelo                   | Una llamada más en el camino del rechazo, y preguntas que no se sabe si funcionan.               |
| Un solo campo para opciones y sugerencias             | Borra la diferencia entre bloquear el turno y no bloquearlo.                                     |
| Reusar `designaciones.idempotencia_comandos`          | Persiste el cuerpo completo de la respuesta: las filas que el enmascaramiento acaba de proteger. |
| Idempotencia persistida en tabla propia               | Misma objeción, más una retención propia que administrar, para resolver un doble clic.           |
| Catálogo derivado del prefijo del prompt              | El prefijo trae el esquema entero, columnas personales incluidas.                                |
| Catálogo con una lista de ejemplos marcados «seguros» | La marca se desincroniza del `GRANT` en silencio.                                                |
