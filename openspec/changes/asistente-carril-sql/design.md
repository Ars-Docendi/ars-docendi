## Context

El sustrato de seguridad ya está: dos roles sin privilegios de mutación, `GRANT SELECT` columna por columna contra un manifiesto deny-by-default, funciones `SECURITY DEFINER` que resuelven el actor y policies RLS que acotan las cuatro tablas del trámite. Ese sustrato define **qué filas y qué columnas** puede tocar el asistente. No define **cómo llega** de una pregunta en español a una consulta.

Este cambio construye ese camino. La restricción que lo ordena todo es que el conjunto de consultas posibles es **infinito y desconocido hasta el runtime**: no hay dónde poner el `if`. Por eso el diseño reparte el trabajo en dos llamadas al modelo y siete piezas deterministas, y no al revés.

La métrica primaria del proyecto es **corrección con abstención**: nunca afirmar algo falso. Casi todas las decisiones de abajo se explican mejor como «qué evita que el sistema afirme algo falso» que como «qué hace que responda».

## Goals / Non-Goals

**Goals**

- Traducir una pregunta del alcance a una consulta correcta, ejecutarla acotada al actor y responder en español.
- Que ninguna consulta generada pueda salirse del alcance del actor aunque el modelo colabore con un atacante.
- Que el prefijo del prompt sea estable, para que el caché del proveedor sea lectura y no escritura.
- Que el turno sea reproducible con la misma fecha inyectada.
- Que el sistema se abstenga en los siete casos, y que «no encontré nada en tu alcance» nunca se diga como «no hay».

**Non-Goals**

- El endpoint `POST /api/asistente/consultas` y el contrato de respuesta con los cuatro estados (épica E7).
- El enmascaramiento de columnas sensibles hacia el modelo (épica E4).
- El hilo conversacional, el reescritor de seguimientos y el detector de ambigüedad (épica E5).
- El carril determinista de API y sus edges hacia `Modules.<X>.Contracts` (épica E6).
- La implementación de un proveedor de modelo real. El carril se construye contra `IProveedorDeModelo`; hoy el único proveedor registrado es el simulado.

## Decisions

### D1 — El esquema del prompt se deriva de los privilegios efectivos, no de una lista en el código

El proveedor de esquema le pregunta a la base **qué puede leer esta conexión**, recorriendo `information_schema.column_privileges` con el rol de la propia sesión, y arma el prefijo con eso.

Una lista embebida en C# se desincroniza en silencio: el día que alguien agregue una columna al manifiesto y al `GRANT`, el prompt seguiría describiendo el esquema viejo, y el modelo generaría SQL contra una columna que no existe o dejaría de usar una que sí. Peor todavía en la dirección contraria: si alguien **revoca** una columna, el prompt seguiría ofreciéndosela al modelo, que la pediría, y el turno fallaría con `permission denied` en vez de abstenerse.

Derivarlo de los privilegios efectivos hace que las dos direcciones se arreglen solas, y hace verdadera la frase «el catálogo de capacidades sale de los GRANT y nunca del payload del prompt».

Consecuencia buscada: **el prefijo del rol básico y el del rol con PII son distintos**, porque sus privilegios lo son. Son dos prefijos con dos hashes, cacheados por separado.

### D2 — Los `COMMENT ON` son parte del sistema, no documentación

El nombre `designaciones.pedidos.novedad` no le dice a un modelo que ahí vive la distinción entre un alta, una baja y un cambio de cargo. El comentario sí, y además puede nombrar los sinónimos que usa el Departamento —«trámite», «solicitud», «pedido de designación»— que en el esquema no aparecen.

Van en la base y no en un archivo del módulo por una razón concreta: **son el único lugar donde la descripción viaja pegada al objeto que describe**. Un `.md` paralelo se desactualiza en el PR que renombra la columna; un `COMMENT ON` vive en la misma migración.

Se escriben para las 14 tablas concedidas. Las dos denegadas no reciben comentario: no aparecen en el prefijo porque el rol no tiene privilegio sobre ellas, y comentarlas sería describir algo que el asistente no puede leer.

### D3 — El prefijo se calcula perezosamente y se cachea por rol

El prefijo necesita una conexión a la base. Construirlo al arrancar rompería el invariante #3: `GET /api/asistente/ping` tiene que responder con la base detenida.

Se calcula la primera vez que alguien lo pide y se guarda en un singleton, con una entrada por rol. Un `SemaphoreSlim` evita que dos turnos concurrentes lo calculen dos veces al arrancar en frío.

**No se invalida solo.** Una migración que cambie el esquema exige reiniciar el proceso para que el prefijo se recalcule. Es la decisión correcta para lo que se optimiza: un prefijo que se invalidara por su cuenta —por ejemplo, revisando el catálogo cada N minutos— podría cambiar **entre dos turnos consecutivos**, que es exactamente lo que RNF-14 prohíbe, y convertiría cada invalidación en una escritura de caché a 1,25× sobre el bloque más grande del prompt. El despliegue ya reinicia el proceso.

### D4 — Similitud léxica y no embeddings

El selector de ejemplos elige por solapamiento de tokens normalizados —sin acentos, sin palabras vacías del español, con un pequeño diccionario de sinónimos del dominio— y no por distancia de vectores.

Con un catálogo del orden de decenas de ejemplos, un vector store es infraestructura nueva —un servicio más, un modelo de embeddings más, una llamada de red más por turno— para elegir entre pocas opciones. La similitud léxica corre en proceso, cuesta cero y es **inspeccionable**: cuando elige mal, se ve por qué.

Si el catálogo creciera a centenares y la selección empezara a fallar de forma medible, la interfaz del selector deja cambiar la implementación sin tocar a quien la usa. Hoy no hay evidencia de que haga falta.

### D5 — Los ejemplos van en el prompt de usuario, el esquema en el de sistema

Es la línea que separa lo que se cachea de lo que no. Los ejemplos cambian por turno —dependen de la pregunta—, así que ponerlos en el prompt de sistema haría que **cada turno pagara escritura de caché** sobre el bloque grande.

La misma regla manda la fecha de referencia y cualquier dato del actor al prompt de usuario. La prueba de que se respeta es mecánica: dos turnos con preguntas distintas producen el mismo prefijo, byte a byte.

### D6 — La fecha de referencia es un parámetro del turno, no una función de reloj

El backend resuelve «hoy» una vez, al empezar el turno, y lo inyecta en el prompt de usuario. La SQL trabaja con una fecha literal que le dieron.

Dos cosas se arreglan a la vez. Del lado de la evaluación: con una fecha fija inyectada, el dataset es determinista y sus resultados esperados no cambian con el calendario —un dataset cuyo resultado depende del día mide qué día lo corriste—. Del lado de la seguridad: si la SQL nunca necesita saber la hora, el validador puede **rechazar el reloj entero** sin romper ningún caso legítimo, y una prohibición que hoy vive como regla del prompt pasa a estar impuesta.

La fecha se resuelve detrás de una interfaz con dos implementaciones: la real y la fija. Mismo código, distinto input.

### D7 — El validador tokeniza emitiendo el contenido de los identificadores entrecomillados

En PostgreSQL las comillas dobles delimitan un **identificador**, no una cadena. `"set_config"(...)` es la función `set_config`. Un tokenizador que trate las comillas dobles como comillas de cadena y descarte su contenido deja pasar exactamente eso: la función que escribe el ajuste del actor, con la que una consulta puede fijarse un actor distinto del suyo y **saltear RLS**. `[VERIFICADO]` en el prototipo previo: 26 filas contra 138 sobre una base real.

Por eso el tokenizador emite el contenido de las comillas dobles como token, con su regla de escape (`""`). Y por eso ese token se chequea contra **funciones** prohibidas únicamente, y no contra palabras clave: `SELECT count(*) AS "cantidad"` es legítimo y `cantidad` no tiene nada de malo, pero si el chequeo fuera contra palabras clave, un alias llamado `"select"` o `"from"` rompería una consulta correcta.

El validador es la **segunda** capa de defensa. La primera es el rol sin privilegios de mutación, que actúa aunque el validador tenga un agujero. Cada vez que las dos capas dicen cosas distintas, gana la del motor.

### D8 — Una sola sentencia, y el punto y coma corta

El validador rechaza cualquier SQL con más de una sentencia. No porque Npgsql las ejecute —la envoltura en subconsulta ya haría fallar la segunda—, sino porque una consulta con dos sentencias **no es lo que el modelo dijo que era**: el razonamiento describe una y se ejecutarían dos. Rechazar es barato y la ambigüedad no.

### D9 — El límite pide una fila de más

La envoltura es `SELECT * FROM (<sql generada>) AS resultado LIMIT tope + 1`.

Con un límite exacto, «la consulta devolvió exactamente N filas» y «la consulta devolvió más de N y la recortamos» son **indistinguibles desde el resultado**. La redacción termina afirmando un total sobre un recorte, que es una afirmación falsa producida por el sistema y no por el modelo.

Con una fila de más, la distinción es aritmética: si volvieron `tope + 1` filas, hubo truncado. La fila sonda se descarta antes de que el resultado salga del ejecutor —nunca llega ni al modelo ni al cliente— y el indicador de truncado viaja al prompt de redacción, que tiene prohibido afirmar conteos cuando está en verdadero.

### D10 — Conexión y transacción nuevas por ejecución, con el actor transaction-local

Cada ejecución abre su propia conexión y su propia transacción, la declara `READ ONLY` y fija el actor con `set_config('app.asistente_user_id', ..., true)` — el tercer parámetro en verdadero es lo que lo hace transaction-local.

Un ajuste de sesión sobreviviría al `COMMIT` y a la devolución de la conexión al pool, y el turno siguiente que tomara esa conexión física **heredaría el actor del anterior**. Es un fallo silencioso: no tira error, responde con el alcance equivocado. Transaction-local, el ajuste muere en el `COMMIT`.

`READ ONLY` es la tercera capa, después del rol y del validador. Es una línea y cierra la clase entera de escrituras.

### D11 — El valor del actor es el id de `identity.users`, nunca el `oid` de Azure

Los dos son UUID, así que confundirlos **compila y ejecuta**. La diferencia aparece en el resultado: el `oid` no corresponde a ninguna fila de `identity.users`, la función que resuelve el actor devuelve nulo o falla, las policies filtran todo y el asistente contesta «no encontré nada» sobre una base llena.

Un error que se manifiesta como una respuesta plausible es peor que uno que tira excepción. Por eso la identidad se toma del usuario autenticado del sistema y **ningún dato enviado por el cliente la determina**; la función `identity.asistente_actor()` además levanta excepción si el UUID que recibe no es un usuario activo, para que el caso rompa fuerte en vez de responder vacío.

### D12 — Cero filas y una fila de nulos son el mismo caso

Una agregación sobre cero filas no devuelve cero filas: devuelve **una fila con nulos**. `SELECT count(*)` devuelve una fila con `0`, pero `SELECT max(horas)` sobre un conjunto vacío devuelve una fila con `NULL`.

El guard reconoce las dos formas. Si solo mirara el conteo de filas, el caso de la agregación pasaría como «resultado con datos» y la redacción hablaría de un máximo que no existe.

### D13 — El reintento no se gasta cuando el vacío puede ser de permiso

RLS convierte «no tenés permiso» en cero filas. Es **exactamente la misma firma** que «el literal no matcheó»: mismo conteo, mismo tipo de resultado, ninguna señal.

Confundirlos tiene doble costo. Gasta el único reintento de generación en un caso donde ningún reintento puede ayudar —la consulta estaba bien, el alcance no la alcanza—, y hace que la redacción diga «no hay designaciones registradas» cuando la verdad es «no podés verlas».

Antes de gastar el reintento se consulta `identity.asistente_es_global()`. Si el actor **no** es global, un resultado vacío no gasta reintento y el turno pasa al caso 3 de la política de abstención. Si es global, el comportamiento no cambia respecto del caso base: para un actor global, cero filas sí significa cero filas.

### D14 — Nunca se declara cuántas filas quedaron afuera

«Ves 3 de 124» es un canal de inferencia sobre datos que el usuario no puede ver: le dice el tamaño de un conjunto al que no tiene acceso, y repetido con distintas preguntas permite reconstruirlo.

La restricción es dura y vale para las tres superficies: el resultado del ejecutor no lleva el total sin filtrar (no lo conoce: la consulta se ejecuta ya acotada), el prompt de redacción lo prohíbe explícitamente, y el indicador de truncado es un booleano y no un número.

### D15 — La razón de un rechazo la lee el usuario final

Cuando el turno se corta —pregunta no contestable, validador que rechaza, error del proveedor— el texto que sale no menciona esquema, tablas, columnas ni SQL.

No es cosmética. El vocabulario del esquema es información sobre la estructura de la base, y un rechazo que diga «no existe la columna `personas.salario`» le confirma a quien pregunta qué columnas sí existen. Es enumeración por mensaje de error, y el asistente es una superficie especialmente cómoda para hacerla.

### D16 — El carril es un servicio, no un endpoint

El orquestador se registra como un servicio del módulo y se ejercita desde los tests. El endpoint llega con E7, junto con el contrato de respuesta de cuatro estados y la `Idempotency-Key`.

Construir el endpoint ahora obligaría a inventar el contrato dos veces: una acá, provisional, y otra en E7 cuando estén los cuatro estados, el catálogo de capacidades y el hilo conversacional. El orden evita esa doble invención y deja el carril testeable de todas formas.

## Risks / Trade-offs

**El prefijo cacheado no se invalida solo.** Una migración de esquema seguida de un despliegue que no reinicie el proceso dejaría al modelo describiendo un esquema viejo. Mitigación: el despliegue reinicia; y el hash del prefijo se sella en cada reporte de evaluación, así que una corrida contra un esquema viejo queda registrada como tal en vez de pasar desapercibida.

**El validador es una lista de prohibiciones, y las listas de prohibiciones se quedan cortas.** Una función peligrosa que no esté en la lista pasa. Mitigación estructural, no de lista: el rol no tiene privilegio de mutación ni `EXECUTE` sobre ninguna función más allá de las cuatro del actor, y la transacción es `READ ONLY`. El validador sube el costo de un ataque; el motor es lo que lo hace inútil. Los tests del validador incluyen el ataque verificado del prototipo con sus tres variantes.

**Dos prefijos distintos duplican la superficie de caché.** El rol básico y el rol con PII no comparten prefijo. Es el costo de derivarlo de los privilegios efectivos, y se acepta: compartir prefijo exigiría describirle al rol básico columnas que no puede leer, que es la falla del punto anterior en la dirección peligrosa.

**El catálogo de ejemplos y el dataset de evaluación tienen que ser disjuntos.** Si se solapan, la métrica mide cuán bien el sistema reproduce ejemplos que ya vio. Además el catálogo de capacidades —E7— deriva sus sugerencias de estos ejemplos, así que un solapamiento haría que el asistente proponga las preguntas con las que se lo evalúa. El test que lo verifica llega con el dataset, en el cambio de evaluación; hasta entonces la disjunción es una convención escrita y no una garantía mecánica.

**El carril no tiene proveedor real.** Con el proveedor simulado, la generación no produce SQL válida y el carril corta en «no contestable». Los tests del pipeline usan un proveedor guionado que devuelve consultas fijas, lo que ejercita todo salvo la calidad de la traducción. Medir esa calidad es exactamente lo que hace la épica de evaluación, y exige un proveedor real y una clave.
