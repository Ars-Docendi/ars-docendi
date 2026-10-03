# Finalidad del historial del asistente y del acceso de soporte

> Nota de finalidad, en el mismo espíritu que
> [`consulta-secretaria-portal-asistente.md`](consulta-secretaria-portal-asistente.md):
> qué es cada superficie, para quién, y qué **no** hace. No es documentación
> técnica.

## El historial propio

**Qué es.** Cada usuario del asistente ve, busca, renombra y borra sus propias
conversaciones pasadas, y puede retomar cualquiera para seguir preguntando.

**Para quién.** Para quien ya usa el asistente (`asistente.consultar`). No
agrega ningún permiso nuevo: es una capacidad del mismo uso que ya tenía.

**Toda conversación se registra, sin excepción por conversación.** No existe
un modo "no guardar esta" ni un botón de "conversación privada". Es
deliberado: el sistema necesita poder responder "¿qué le preguntó este
usuario al asistente?" — a través de este mismo historial y, cuando haga
falta, a través del acceso de soporte descripto abajo —, y una opción para
saltear el registro contradiría eso directamente. La única excepción es
técnica y no una opción del usuario: un turno que revienta con un error
interno no llega a producir una respuesta, así que no hay nada que guardar
de él.

**Qué se guarda: la pregunta y la consulta SQL que la respondió, nunca los
datos.** Nunca las filas que devolvió una consulta, nunca el texto de la
respuesta redactada. Guardar la consulta permite volver a ejecutarla más
adelante bajo el acceso vigente del usuario en ese momento — nunca una foto
vieja de datos a los que quizás ya no tiene acceso.

**Retención: 180 días desde la última vez que se usó esa conversación**, no
desde que se creó. Después de eso se borra sola, automáticamente.

**Qué NO hace.** No comparte conversaciones entre usuarios. No genera ningún
enlace público. No permite exportar la conversación de otra persona.

## El acceso de soporte

**Qué es.** Una vía **estrictamente limitada** para que soporte técnico
pueda leer el historial de otro usuario cuando esa persona reporta un
problema con el asistente y hace falta ver qué preguntó y qué le respondió.

**Para quién.** Para nadie, hasta que Secretaría decida lo contrario. Es un
permiso propio (`asistente.leer_historial_ajeno`) que **no viene incluido**
en ningún rol — ni siquiera en el de administración del sistema — y se
concede desde la pantalla de roles, igual que cualquier otro permiso de la
aplicación.

**Toda lectura exige una razón, y queda registrada para siempre.** Quien use
este acceso tiene que escribir por qué lo necesita, y el sistema registra,
de forma permanente y no editable, quién leyó, a quién, cuándo y por qué —
así haya sido para ver la lista de conversaciones o para abrir una en
particular.

**Qué NO hace.**

- No muestra los datos que una consulta devolvió, sólo la pregunta, la
  consulta SQL, el resultado (respondida/no contestable/etc.) y los momentos.
- No permite volver a ejecutar la consulta de otra persona, ni bajo ninguna
  forma de "hacerse pasar por" ese usuario.
- **No le avisa al usuario que su historial fue leído.** Es una decisión
  tomada, no una pendiente: mostrar quién accedió expondría, uno por uno, al
  personal de soporte frente a la persona cuyo caso investigan, y podría
  alertar a mitad de una investigación en curso. Es el mismo criterio con el
  que operan las herramientas de soporte comparables (ChatGPT Enterprise,
  Claude Enterprise, Microsoft Purview): el registro de acceso queda del
  lado de quien administra, no del lado de quien fue consultado.

## Por qué se declara esto, si los datos ya están en el sistema

Mismo criterio que la nota del Portal Docente: los datos de una conversación
ya existen apenas se hace la pregunta. Este documento no habilita un dato
nuevo — declara para qué se guarda y quién puede leerlo más allá de su
dueño, que es exactamente lo que la Ley 25.326 pide declarar antes de un uso
que no es el que motivó la recolección original.
