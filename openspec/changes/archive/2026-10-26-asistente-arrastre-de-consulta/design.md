# Diseño — el hilo arrastra la consulta, no las filas

## Decisiones

### D1 — Se arrastra el SQL y no los resultados

Las dos formas de resolver «los profesores de esa materia» son guardar el valor que salió en la respuesta, o guardar la consulta que lo produjo. Se elige la segunda, y no por comodidad.

**Guardar los valores mueve el dato personal, no lo evita.** Un allowlist por clase —materia y carrera sí, persona no— parece acotado, pero se apoya en clasificar bien cada columna del resultado, y `BR-asistente-002` ya registra que ahí no se puede confiar: el enmascarador identifica la columna por `(OID, attnum)`, y **toda expresión reporta OID 0 y se trata como pública**. Un `SELECT upper(p.apellido)` produce una columna que el sistema considera no sensible. Con esa vía, ese apellido entra al hilo y vuelve al prompt en el turno siguiente, que es exactamente el agujero que D1 de la capa conversacional existe para tapar.

**El SQL no tiene ese problema.** Sus literales salen de la pregunta del usuario, que el hilo ya guarda; ninguna fila leída de la base aparece en él. El hilo no gana ninguna clase de dato que no tuviera.

Y hay un argumento de capacidad además del de seguridad: el conjunto es más útil que el valor. «Los profesores de esa materia» sobre una consulta que devolvió tres materias se responde bien anidando; con el valor de una sola fila habría que elegir cuál.

### D2 — Se arrastra la consulta que respondió, no la que se generó

El carril puede generar dos consultas en un turno: si la primera vuelve vacía y el actor alcanza todo, se reintenta. Se anota **la que produjo la respuesta que el usuario vio**.

Anotar la primera haría que el seguimiento editara una consulta que el usuario nunca vio contestada, y el arrastre pasaría a corregir hacia atrás en vez de continuar hacia adelante. Un turno que terminó en abstención o sin filas **no anota consulta**: no hay nada que continuar, y ofrecerle al modelo una consulta que no encontró nada lo invita a repetirla.

### D3 — La consulta va en el mensaje de usuario, nunca en el prefijo

El prefijo estable se cachea y su huella sella cada reporte de evaluación. Meter ahí algo que cambia por turno lo invalidaría en cada llamada y pagaría escritura de caché a 1,25× sobre el bloque más grande del prompt, en vez de lectura a 0,1×.

Va donde ya van los ejemplos y la fecha: en la parte variable. La **regla** que le dice al modelo qué hacer con ella sí va en el prefijo, porque no cambia por turno — y es lo que hace que este change exija regrabar el corpus.

### D4 — Se arrastra el segmento vigente, no el hilo entero

El mismo recorte que ya rige para las preguntas: `InicioDeSegmento` y `TopeDeTurnosDelHistorial`. Un pivote de tema suelta las consultas igual que suelta las preguntas, y sin código nuevo — es la propiedad que D2 de `asistente-capa-conversacional` ya compró.

Arrastrar el hilo entero le daría al modelo consultas de un tema que el usuario abandonó, y editar una de ésas produce una respuesta correcta a una pregunta que nadie hizo.

### D5 — La enmienda a D1 de la capa conversacional se escribe, no se interpreta

El texto vigente dice: «lo que se guarda por turno es la pregunta interpretada y su marca de tiempo. **Nada más**». Este cambio lo contradice.

Se reescribe la decisión nombrando qué se suma y por qué la propiedad que protegía sigue en pie. El precedente es el invariante #14 de `CLAUDE.md`: una regla reinterpretada deja de restringir a nadie, y deja a quien revisa sin nada contra qué evaluar. La regla vieja se enmienda o se cumple; estirarla no es ninguna de las dos.

## Alternativas descartadas

**Transcripción completa enmascarada.** Es lo que hacen los chatbots de propósito general, y con un breakpoint de caché sobre el historial sale ~+12% por turno —barato—. Se descarta por tres cosas: exige reordenar el prompt para que el historial sea append-only y cacheable, amplifica la grieta de `BR-asistente-002` de un turno a todos los siguientes, y **no resuelve nada que el arrastre de consulta no resuelva** en el caso que la motivó. Queda como opción si aparece evidencia medida de que el arrastre no alcanza.

**Alias estables a lo largo del hilo.** Guardar «documento 1» → valor real para que el modelo pueda correferenciar entre turnos. Se descarta porque el diccionario **es** el dato personal: no evita almacenarlo, lo muda de las filas a la tabla de alias. Y un alias estable durante veinte turnos es un identificador seudónimo al que se le acumulan atributos sin nombrar a nadie, que bajo la Ley 25.326 sigue siendo dato personal.

**Pedir aclaración en vez de resolver.** Es complementario, no alternativo: queda como red de seguridad para cuando la reescritura falla igual. Se hace después y en su propio change, porque para ofrecer opciones concretas necesita el contexto que éste pone a disposición.
