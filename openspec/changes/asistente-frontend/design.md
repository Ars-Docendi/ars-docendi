# Diseño — La superficie de usuario en el frontend

## Decisiones

### D1 — El acceso se pregunta, no se deduce del rol

El frontend no tiene los permisos del usuario: `CurrentUser` trae nombre, rol y roles disponibles. Lo tentador es una lista —«todos menos Docente»— y es el mismo antipatrón que el backend rechazó al sembrar el permiso: `identity.roles` **no es un catálogo cerrado**, Secretaría puede crear roles nuevos, y una lista embebida falla **abierta** con cualquiera que no conozca.

El gate consulta `GET /api/asistente/capacidades`. Si responde, hay acceso; si responde `403`, no. Es el mismo permiso que protege el turno, resuelto por quien lo administra, y no cuesta tokens.

Efecto lateral deseable: el catálogo que el gate trae es el mismo que la vista necesita para su pantalla inicial, así que la consulta no es un costo extra sino el primer dato útil.

### D2 — Una sola vista, dos montajes

`PanelAsistente` es la vista. La ruta `/asistente` la monta a página completa; el lanzador la monta en un cajón lateral sin sacar al usuario de donde está.

Dos implementaciones distintas se desincronizarían: la de la ruta recibiría una mejora y la del cajón no, y nadie lo notaría hasta que un usuario reportara que «desde el botón anda distinto».

### D3 — El hilo vive en el componente, no en un store global

El asistente es una conversación de una sesión, y el backend ya decidió no persistirla. Un store global agregaría una decisión de ciclo de vida —cuándo se limpia, qué pasa al cambiar de rol— para un estado que muere igual al recargar.

El identificador de hilo se guarda en el estado del panel y viaja en cada turno.

### D4 — La región viva envuelve la lista de mensajes y nada más

El defecto a no repetir está verificado en el prototipo: la región envolvía el contenedor entero, así que **cada re-render hacía que el lector leyera todo de nuevo**, línea de métricas incluida.

`role="log"` sobre la lista de mensajes, con `aria-live="polite"`. Las métricas van en un elemento hermano, fuera. El indicador de proceso va en su propio `role="status"`, también fuera del log: es un estado, no un mensaje de la conversación.

### D5 — El indicador aparece con umbral y no muestra etapas

Los tres carriles tienen latencias que se diferencian en un orden de magnitud: el social responde al instante y el de SQL tarda segundos. Mostrar «procesando» durante los milisegundos de una respuesta determinista **parpadea**, y un parpadeo es peor que nada — para un lector de pantalla es un anuncio que aparece y desaparece antes de leerse.

Un temporizador de umbral lo monta recién si el turno sigue en curso.

**No se inventan etapas.** «Interpretando… consultando… redactando…» exigiría streaming del servidor, que cambia el contrato y agrega infraestructura difícil de justificar en un módulo no-core. Un progreso simulado por temporizador sería exactamente el fake UI que el invariante #7 prohíbe.

### D6 — La idempotencia se genera por intento, no por turno

Cada envío genera una clave nueva. Un reintento del **mismo** envío —el usuario aprieta dos veces, el cliente reintenta por timeout— reusa la clave, que es para lo que existe.

Generarla una vez por conversación haría que el segundo turno recibiera la respuesta del primero.

### D7 — El degradado se muestra como estado, no como error

Un banner rojo de error le dice al usuario que hizo algo mal. El servicio degradado no es culpa suya y su pregunta no tiene nada de malo: se presenta como una situación temporal del asistente, con el texto que el backend ya redactó —que distingue «se agotó tu cupo, volvés a las 15:40» de «el asistente no está disponible»—.

### D8 — El truncado se dice sin números

«Ves 3 de 124» es un canal de inferencia sobre datos que el usuario no puede ver, y el backend por eso devuelve un booleano y nunca un conteo. La interfaz respeta la misma regla: dice que hay más, no cuántos.

## Alternativas descartadas

| Alternativa                            | Por qué no                                                                   |
| -------------------------------------- | ---------------------------------------------------------------------------- |
| Gate por lista de roles en el frontend | Falla abierta con cualquier rol nuevo; `identity.roles` no es cerrado.       |
| Store global para el hilo              | Agrega decisiones de ciclo de vida para un estado que muere al recargar.     |
| Región viva sobre el contenedor entero | Es el defecto verificado del prototipo: relee todo en cada render.           |
| Etapas de progreso simuladas           | Fake UI. Etapas reales exigen SSE y cambian el contrato.                     |
| Portar el HTML del prototipo           | Arrastra el defecto de accesibilidad que este cambio existe para no repetir. |
