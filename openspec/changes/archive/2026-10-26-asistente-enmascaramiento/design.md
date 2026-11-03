# Diseño — Enmascaramiento y datos sensibles

## Contexto

El carril SQL ya funciona de punta a punta. Entre `EjecutorDeConsulta` y `RedactorDeRespuesta` hay hoy una llamada directa: el resultado crudo entra al prompt. Este cambio mete una pieza en esa costura y no toca nada más del carril.

La frontera que se construye acá es **de salida**, y es distinta de la que ya existe. `asistente-fundaciones` decidió quién puede **leer** qué, con dos roles y `GRANT` por columna, y esa decisión la impone el motor. Este cambio decide qué **sale hacia un tercero**, y esa decisión no puede imponerla el motor porque el motor no sabe qué hacemos con las filas después.

## Decisiones

### D1 — El manifiesto vive en `database/asistente/`, hermano del de privilegios

Podría haber vivido en `Modules.Asistente/Recursos/`, junto al catálogo de ejemplos.

Se eligió `database/asistente/manifiesto-sensibilidad.json` porque los dos manifiestos responden preguntas sobre las mismas columnas y se leen juntos: quien agrega una columna a `identity.personas` tiene que tocar los dos, y tenerlos en directorios distintos hace que se olvide uno. El de privilegios ya está ahí.

El archivo se embebe como recurso del assembly por el mismo mecanismo con que ya viaja el DDL —`Include` con `Link` más un `Target` que falla el build si el glob queda vacío—, así que el módulo no depende del layout del despliegue.

### D2 — El manifiesto tipado vive en el módulo, no en los tests

El manifiesto de privilegios se deserializa en el **proyecto de tests**, y con razón: nadie en producción lo consume, existe solo para ser comparado contra la base.

Éste es al revés. El enmascarador lo consume en cada turno, así que el tipo vive en `Modules.Asistente/Application/`. El criterio «la clasificación es de una sola fuente» se cumple porque el test lee **el mismo tipo** que usa producción, no una copia.

### D3 — La clasificación se resuelve a `(OID, número de atributo)`, no a nombres de columna

Es la decisión central del cambio.

Lo obvio sería mirar `ResultadoDeConsulta.Columnas` y comparar los nombres contra el manifiesto. **No funciona**: esos nombres son los alias que eligió la consulta generada, no los de las tablas. Un `SELECT p.documento AS codigo` produce una columna llamada `codigo`, y un enmascarador que compare nombres la deja pasar entera.

No es un caso hipotético: el modelo pone alias por su cuenta, en español, todo el tiempo, y ninguna instrucción del prompt puede garantizar que no lo haga.

PostgreSQL emite en la descripción de filas, para cada columna que sea una referencia directa a una columna de tabla, el **OID de la tabla** y el **número de atributo**. Npgsql los expone como `TableOID` y `ColumnAttributeNumber`. Ese par identifica la columna de origen **sin importar el alias**, y no lo elige la consulta: lo dice el motor.

Entonces: el manifiesto se resuelve una sola vez a un conjunto de pares `(OID, attnum) → clasificación`, se cachea, y enmascarar es una búsqueda en un diccionario.

### D4 — Riesgo residual aceptado: una expresión derrota el par

El par viene vacío cuando la columna **no** es una referencia directa: `count(*)`, `documento || ''`, `substring(telefono, 1, 4)`. Para esas columnas el origen es desconocido.

Se decidió tratar el origen desconocido como **público**, y se declara acá porque es una decisión de seguridad tomada a conciencia y no un olvido.

Las alternativas y por qué se descartaron:

- **Enmascarar todo origen desconocido**: rompe `count(*)`, que es la forma más común de consulta agregada. Un asistente que no puede decir «encontré 4 docentes» pierde la mitad de su utilidad para ganar cobertura sobre un caso que requiere que el modelo activamente envuelva una columna personal en una expresión.
- **Enmascarar origen desconocido solo en turnos con la conexión de datos personales**: mismo problema, acotado a los actores globales, que son justamente los que más agregan.
- **Que el validador rechace expresiones sobre columnas personales**: el validador tokeniza SQL y no conoce el esquema; tendría que adivinar por nombre, que es exactamente la fragilidad que D3 evita.

Lo que acota el riesgo: esas columnas **solo son legibles con la conexión de datos personales**, que exige permiso y alcance global. Un actor que no la tiene no puede construir la expresión aunque quiera — el motor rechaza la consulta antes. El riesgo real es entonces «un actor autorizado a ver el dato logra que el dato salga al proveedor», que es menor que «cualquiera lo logra», pero no es cero.

Queda registrado en `tech-debt.md` y anotado en el código, junto al mecanismo que lo produce.

### D5 — El marcador es un contador, no una función del valor

Los marcadores tienen que ser **estables dentro de una respuesta**: si el mismo documento aparece en tres filas, el modelo tiene que poder decir «las tres son de la misma persona».

Lo tentador es derivarlo del valor —un hash corto— porque sale estable sin llevar estado. Se descartó: un hash de un documento es invertible por fuerza bruta en segundos, porque el espacio de documentos es chico y conocido. El marcador viajaría al proveedor y sería el dato, con un paso más.

El marcador es entonces un **contador por orden de primera aparición**, con un diccionario por respuesta. No tiene ninguna relación con el valor y no sobrevive al turno.

La etiqueta la da el manifiesto (`documento`, `teléfono`), así que el modelo sabe **de qué** es el marcador sin saber cuál. Eso es lo que le permite redactar «encontré 4 docentes» en vez de una frase sin sujeto.

### D6 — `sensible-texto` se suprime entero, columna incluida

Para `sensible-valor` alcanza con tapar el valor. Para `sensible-texto` —`pedido_historial.comentario`, los justificativos de rechazo y las devoluciones— no alcanza, porque es **texto libre que puede nombrar a cualquiera**: un comentario puede decir «hablé con Gómez, tiene el teléfono de Pérez». Redactarlo con reglas es frágil y falla en silencio.

Se suprime la columna completa: sale de la lista de columnas del prompt y sale de cada fila. El modelo no ve que existe. La alternativa —dejar el nombre con un marcador— le diría al modelo que hay un comentario que no puede leer, y eso invita a que lo mencione en la respuesta, que es ruido para el usuario.

El valor real sigue viaje al llamador, intacto.

### D7 — El enmascarador es puro y el ejecutor es quien clasifica

`EjecutorDeConsulta` ya tiene la conexión abierta y el lector, que es donde están el OID y el attnum. Clasificar ahí evita que `ResultadoDeConsulta` cargue identificadores crudos del motor hasta la capa de aplicación.

Entonces: el ejecutor devuelve el resultado **con la clasificación de cada columna ya resuelta**, y el enmascarador es una función pura sobre eso, testeable en memoria sin base ni proveedor.

### D8 — La clasificación entra como parámetro opcional del resultado

`ResultadoDeConsulta` es un `record` posicional con tres parámetros y solo dos sitios de construcción en producción. La clasificación entra como cuarto parámetro con valor por omisión, para no tocar los call sites del evaluador ni los helpers de test.

El valor por omisión trata todas las columnas como públicas, que es fail-open. Se acepta **únicamente** porque el único constructor de producción que lo omitiría es el del evaluador, que corre contra un fixture sintético sin datos personales reales, y porque el que sí importa —el ejecutor— lo pasa siempre. Un test lo fija.

### D9 — El cierre de la selección de conexión no reabre su diseño

El mecanismo está construido y es correcto: `ConsultorDeAlcance` exige permiso **y** alcance global, `CarrilSql` propaga la decisión y `EjecutorDeConsulta` elige la cadena. Lo que falta es que el riesgo residual esté anotado **en el código** con el link al ticket de endurecimiento, y dos tests de aceptación de punta a punta.

No se toca la condición ni se adelanta el endurecimiento de `identity.personas`, que tiene su propio ticket y necesita una policy RLS.

## Qué NO hace este cambio

- No agrega superficie de usuario ni contrato de API. El destinatario de las filas reales sigue siendo el llamador del carril.
- No cierra la entrada. La pregunta cruda sigue viajando al proveedor.
- No agrega RLS sobre `identity.personas`.
- No persiste nada. El registro operativo y el analítico son de otra épica; acá solo se verifica que las filas **no** aparezcan en el log.
