## Context

El carril SQL está construido y verde. Lo que falta es la única cosa que puede decir si **traduce bien**: el resto de los tests verifica que cada pieza hace lo suyo, no que la pregunta llegue a la consulta correcta.

Todo este cambio está organizado alrededor de una sola pregunta incómoda: **¿cómo sabemos que el número que da el reporte significa algo?** Las dos trampas verificadas —el eval que miente sin crédito, y el proyecto que gasta plata metido en el CI— son las dos formas en que ese número deja de significar algo sin que nadie lo note.

## Goals / Non-Goals

**Goals**

- Medir el eje de capacidad con una puntuación que penalice afirmar algo falso.
- Que un reporte no pueda escribirse sobre una corrida inválida.
- Que un reporte no pueda quedar describiendo un dataset o un esquema que ya no existen.
- Que el CI no pueda gastar dinero, ni por olvido ni por un merge descuidado.
- Que el fixture sea reproducible byte a byte y no dependa del calendario.

**Non-Goals**

- Los otros tres ejes —robustez de fraseo, diálogo con chequeo negativo de arrastre, social y meta— son de H4, y dos de ellos necesitan capas que todavía no existen.
- El gate de regresión con lock por ítem es de H4: primero hace falta una línea de base, y la línea de base sale de correr esto.
- La implementación de un proveedor de modelo real. El runner la pide por interfaz; hoy no hay ninguna.

## Decisions

### D1 — Se parte en dos proyectos según **qué cuesta dinero**, no según qué es «de evaluación»

`ArsDocendi.Evaluacion.Nucleo` está **en** la solución: el generador del fixture, el modelo del dataset, la puntuación, el sellado y la orquestación del runner. Todo eso es código puro sobre datos, y el CI lo prueba con un proveedor de guion.

`ArsDocendi.Evaluacion` —el ejecutable— está **fuera**: es lo único que instancia un proveedor real.

La alternativa obvia —poner _todo_ lo de evaluación afuera— tiene un costo que no se paga: la puntuación, el generador y el preflight dejarían de tener tests en el CI. Y son exactamente las piezas donde un error hace que el número mienta. Un eval sin tests es un eval en el que no se puede confiar, que es lo mismo que no tenerlo.

Lo que hay que sacar del CI no es «la evaluación»: es **la llamada facturada**.

### D2 — La exclusión es estructural, y además hay un guard

El runner no figura en `ArsDocendi.slnx`, y hay un test **dentro** de la solución que lee ese archivo y falla si el runner aparece.

Un filtro en el YAML del CI se descartó y el motivo está escrito: vive en el YAML, y un identificador mal escrito o un merge que revierta el flag cuesta dinero sin que nadie lo note. El costo del olvido es asimétrico —el síntoma es una factura, no un test rojo— así que la protección tiene que fallar ruidosamente y del lado que sí se ejecuta.

El guard se verifica del único modo que vale: agregando el proyecto al archivo de solución a mano y comprobando que el test falla.

### D3 — El preflight aborta **sin dejar reporte**

Antes de correr un solo ítem, el runner le pide al proveedor una completación trivial y verifica tres cosas: que no haya excepción, que la respuesta **no sea simulada**, y que los conteos de tokens sean mayores que cero.

Los tres importan por separado. Sin crédito, la llamada devuelve una abstención con error seteado y métricas en cero: entrada y salida en cero, con latencias de milisegundos en vez de segundos. Los ítems que esperan una abstención pasan **espuriamente**, y el reporte muestra un número bajo que parece una regresión del modelo. La bandera de simulado cubre el otro caso: correr el eval contra el proveedor simulado por olvidar la configuración.

Cuando el preflight falla, el runner devuelve un código distinto de cero y **no escribe nada**. Un reporte escrito sobre una corrida inválida es peor que no tener reporte: el que no existe se nota, el que miente no.

### D4 — El scoring exige la abstención **sin error**

Un ítem no contestable no aprueba con que el booleano esté en falso: tiene que abstenerse **y** no traer error.

Es la misma trampa que el preflight, vista desde el otro lado. Con la clave vacía, todos los ítems no contestables devuelven «no contestable» —porque el turno falló— y un scoring que solo mirara el booleano los daría por acertados. El eje de abstención, que es la métrica primaria, sería el que más se infla cuando el sistema no funciona.

Las dos defensas se necesitan: el preflight impide que la corrida arranque, y este chequeo impide que un ítem individual se acredite por una falla.

### D5 — Las consultas de referencia se ejecutan **en vivo**

El dataset guarda la consulta de referencia, no su resultado.

Con conjuntos de resultados guardados, cualquier cambio del fixture desincroniza el dataset en silencio: la referencia dice una cosa y la base tiene otra, y la métrica empieza a medir esa diferencia en vez de medir al asistente. Ejecutando en vivo, referencia y respuesta se mueven juntas.

El costo es que cada corrida ejecuta el doble de consultas. Son consultas contra un fixture chico y no cuestan tokens: es el lado barato del intercambio.

### D6 — La comparación es de **conjuntos de filas**, no de texto de consulta

Dos consultas escritas distinto pueden ser la misma respuesta. Comparar el SQL generado contra el de referencia mediría estilo.

Se comparan los resultados: mismas filas, mismos valores, sin importar el orden salvo que el ítem declare que el orden es parte de la pregunta. Los nombres de columna **no** se comparan —un alias distinto no es un error de traducción—.

### D7 — La puntuación se reporta con **varios valores de penalización**

Cuánto vale una respuesta falsa respecto de una abstención es una decisión de producto, no de ingeniería, y todavía no está tomada.

En vez de elegir un número y esconder la elección adentro de la métrica, el reporte trae la misma corrida puntuada con tres penalizaciones: 0,5 · 1,0 · 2,0. Quien lee ve cómo cambia el ranking según cuánto se castigue mentir, que es la conversación que hay que tener.

### D8 — Cada reporte se sella con tres hashes

Prefijo del prompt, dataset y fixture.

Sin esto el problema se repite en el próximo refactor de esquema: los reportes quedan describiendo datasets que ya no existen y los números del proyecto dejan de ser reproducibles, sin que nadie lo note. Con el sello, una corrida vieja se identifica como vieja en lugar de compararse de igual a igual con una nueva.

Los tres hashes ya existen: el del prefijo lo expone el proveedor de esquema, el del catálogo lo expone el selector, y el del fixture lo expone su generador. Este cambio los junta en un encabezado.

### D9 — El fixture usa una fuente de aleatoriedad **por sección**

La trampa está documentada en el ticket y es real: con una sola fuente compartida, cambiar cuántas veces se la llama en una sección corre todos los valores de las siguientes. Y los apellidos compartidos son el ancla de varios diálogos, así que ese acoplamiento rompería ítems que no se tocaron.

Cada sección del generador —carreras, materias, personas, designaciones, pedidos— tiene su propia fuente, sembrada con un valor derivado del nombre de la sección. Agregar una persona más no mueve ningún pedido.

### D10 — Lo «actual» del fixture se expresa con banderas del dominio, nunca con el reloj

Un período `activo = true` y una designación con `vigente_hasta IS NULL`. Ninguna consulta de referencia usa funciones de reloj —el validador ya las rechaza— y la fecha ancla del fixture es fija.

Un dataset cuyo resultado esperado cambia con el calendario mide qué día lo corriste. El mismo motivo por el que la fecha de referencia del turno entra por parámetro.

### D11 — Las colisiones del dominio son parte del contrato del fixture

Nombres de materia repetidos entre carreras y apellidos compartidos, con cardinalidades declaradas y verificadas por test.

Sin ellas, el detector de ambigüedad —épica E5— no dispara nunca, y los ítems de diálogo que lo prueban **dan verde sin medir nada**. Es la misma clase de problema que un dataset que no mide lo que dice medir, y por eso las cardinalidades se afirman explícitamente en vez de confiar en que el generador «probablemente» las produzca.

### D12 — La disjunción con el catálogo de ejemplos se verifica acá

El cambio del carril dejó la tarea abierta a propósito: no se puede verificar contra un archivo que no existe. Ahora existe.

Si se solapan, la métrica mide cuán bien el sistema reproduce ejemplos que ya vio. Y como el catálogo de capacidades —épica E7— deriva sus sugerencias de esos ejemplos, el asistente estaría proponiendo las preguntas con las que se lo evalúa.

## Risks / Trade-offs

**El eval no se puede correr todavía.** No hay ninguna implementación de proveedor real: `IProveedorDeModelo` tiene una sola, la simulada, y el preflight la rechaza a propósito. El runner está completo y probado con un proveedor de guion, pero producir un número exige elegir proveedor y modelo, y tener una clave. Es una decisión de producto y de costo, no de ingeniería, y queda planteada.

**El núcleo está en la solución y depende de `Modules.Asistente`.** Es una dependencia de herramienta hacia producción, no al revés, y ningún proyecto de producción la referencia. El guard de arquitectura del asistente no la ve porque solo escanea el módulo. Vale la pena que quede escrito: si algún día un proyecto de producción referenciara el núcleo, sería un error.

**Tres penalizaciones no son una métrica única.** Reportar tres números en vez de uno hace más difícil decir «el eval da 0,82». Es deliberado —la elección es de producto— pero tiene un costo de comunicación, y el día que el equipo elija un valor conviene fijarlo y dejar los otros dos como contexto.

**El fixture es sintético.** Mide traducción sobre datos que se parecen a los reales, no sobre los reales. Un patrón del dominio que el generador no reproduzca es un patrón que el eval no mide. Las colisiones están puestas porque son las que se conocen; van a aparecer otras.
