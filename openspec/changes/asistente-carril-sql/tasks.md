## 1. Comentarios de esquema (ARS-28, primera mitad)

- [x] 1.1 `COMMENT ON TABLE` para las 14 tablas concedidas por el manifiesto, en español y con los sinónimos del dominio. **Reparto**: `database/identity/013_identity_comentarios_asistente.sql` y `database/designaciones/010_designaciones_comentarios_asistente.sql`, y no un archivo del asistente — el dueño del bounded context escribe el DDL de sus objetos, mismo criterio con que las policies RLS viven en el DDL de `designaciones`
- [x] 1.2 Mismos archivos — `COMMENT ON COLUMN` para cada columna concedida a cualquiera de los dos roles, incluidos los valores admitidos de las columnas con restricción de dominio
- [x] 1.3 Verificar que las dos tablas denegadas del manifiesto **no** reciben comentario
- [x] 1.4 Test: toda tabla concedida por el manifiesto tiene comentario no vacío en el catálogo
- [x] 1.5 Test: toda columna concedida por el manifiesto tiene comentario no vacío
- [x] 1.6 Test: ninguna tabla denegada tiene comentario
- [x] 1.7 Verificar la idempotencia aplicando la migración dos veces

## 2. Proveedor de esquema y prefijo cacheado (ARS-28, segunda mitad)

- [x] 2.1 Leer del catálogo las columnas **efectivamente legibles** por la conexión, con sus tipos, sus claves foráneas y sus comentarios
- [x] 2.2 Renderizar el prefijo con orden determinista y sin ningún dato variable por turno
- [x] 2.3 Cachear el prefijo por rol, calculándolo perezosamente y una sola vez por proceso
- [x] 2.4 Exponer la huella del prefijo completo
- [x] 2.5 Registrar el proveedor de esquema en el módulo
- [x] 2.6 Test: el prefijo de los dos roles difiere exactamente en las columnas personales
- [x] 2.7 Test: el prefijo no menciona ninguna tabla denegada
- [x] 2.8 Test: dos turnos con preguntas, actores y fechas distintas comparten prefijo byte a byte
- [x] 2.9 Test: el prefijo no contiene la fecha de referencia ni ningún identificador de actor
- [x] 2.10 Test: varios turnos consecutivos consultan la base una sola vez
- [x] 2.11 Test: la huella es estable entre procesos y cambia al conceder una columna nueva
- [x] 2.12 Test de no regresión: el `ping` sigue respondiendo con la base detenida

## 3. Catálogo de ejemplos y selector léxico (ARS-29)

- [x] 3.1 `backend/src/Modules.Asistente/Recursos/ejemplos-sql.json` — catálogo versionado con pregunta, SQL y categoría por ejemplo, embebido como recurso
- [x] 3.2 Cubrir las familias de preguntas del alcance: cobertura de cátedra, composición del plantel, estado del trámite, historial y agregaciones
- [x] 3.3 Normalizador léxico: minúsculas, sin acentos, sin palabras vacías del español, con los sinónimos del dominio
- [x] 3.4 Selector por solapamiento de tokens, con umbral mínimo y tope de ejemplos
- [x] 3.5 Exponer la huella del catálogo
- [x] 3.6 Registrar el selector en el módulo
- [x] 3.7 Test: toda consulta del catálogo ejecuta sin error contra el esquema vigente
- [x] 3.8 Test: toda consulta del catálogo pasa el validador
- [x] 3.9 Test: dos formulaciones que difieren solo en acentos y mayúsculas seleccionan lo mismo
- [x] 3.10 Test: un ejemplo que comparte solo palabras vacías pierde contra uno que comparte un término del dominio
- [x] 3.11 Test: una pregunta sin parentesco devuelve selección vacía
- [x] 3.12 Test: la selección no consume llamadas al modelo, verificado con el techo del turno agotado
- [x] 3.13 Test: los ejemplos aparecen en el prompt de usuario y el prefijo no cambia con ellos
- [x] 3.14 Test: la huella del catálogo cambia al agregar un ejemplo
- [ ] 3.15 La verificación de disjunción con el dataset de capacidad se implementa en el cambio de evaluación, junto con el dataset

## 4. Fecha de referencia (ARS-30, primera mitad)

- [x] 4.1 Interfaz de fecha de referencia con la implementación real y la fija
- [x] 4.2 Resolver la fecha una vez por turno y llevarla al prompt de **usuario**
- [x] 4.3 Registrar la implementación real en el módulo
- [x] 4.4 Test: la fecha aparece en el prompt de usuario y no en el prefijo
- [x] 4.5 Test: con la misma fecha inyectada, dos generaciones producen la misma solicitud

## 5. Generación de SQL (ARS-30, segunda mitad)

- [x] 5.1 Armar el prompt de usuario con los ejemplos, la fecha y la pregunta
- [x] 5.2 Llamar al modelo con temperatura cero y el prefijo sin modificar
- [x] 5.3 Interpretar la respuesta como objeto con contestable, consulta, razonamiento y categoría
- [x] 5.4 Tolerar la respuesta envuelta en delimitadores de bloque de código
- [x] 5.5 Ante una respuesta ininteligible, resolver no contestable sin intentar extraer una consulta del texto
- [x] 5.6 Cortar el turno cuando la pregunta no es contestable, sin ejecutar y sin segunda llamada
- [x] 5.7 Propagar el razonamiento al resultado del turno, también cuando el turno se abstiene
- [x] 5.8 Test: la solicitud lleva temperatura cero y el prefijo del proveedor de esquema
- [x] 5.9 Test: una respuesta envuelta en delimitadores se interpreta igual
- [x] 5.10 Test: una respuesta ininteligible resuelve no contestable
- [x] 5.11 Test: una pregunta no contestable consume exactamente una llamada al modelo
- [x] 5.12 Test: una pregunta no contestable no abre ninguna conexión de solo lectura
- [x] 5.13 Test: el razonamiento llega al resultado en el caso con respuesta y en el caso de abstención

## 6. Validador de la SQL generada (ARS-31)

- [x] 6.1 Tokenizador que descarta comentarios de línea y de bloque, literales de texto y literales delimitados por signo pesos
- [x] 6.2 Tokenizador que **emite** el contenido de los identificadores entre comillas dobles, con la regla de escape de la comilla duplicada
- [x] 6.3 Chequear los identificadores entrecomillados contra funciones prohibidas **únicamente**, no contra palabras clave
- [x] 6.4 Lista de funciones prohibidas, con las ocho de reloj y las de ajuste de sesión
- [x] 6.5 Lista de palabras clave de mutación y de definición de datos
- [x] 6.6 Rechazar entradas con más de una sentencia, tolerando el punto y coma final
- [x] 6.7 Registrar el validador en el módulo
- [x] 6.8 Test: rechaza la escritura del ajuste del actor con identificador entrecomillado
- [x] 6.9 Test: rechaza la variante con subconsulta escalar
- [x] 6.10 Test: rechaza la variante con join lateral
- [x] 6.11 Test: rechaza la lectura del ajuste con identificador entrecomillado, y la misma con mayúsculas mezcladas y espacios intercalados
- [x] 6.12 Test: **acepta** `SELECT count(*) AS "cantidad" FROM identity.carreras`
- [x] 6.13 Test: acepta un alias entrecomillado que coincide con una palabra clave prohibida
- [x] 6.14 Test: rechaza cada una de las ocho funciones de reloj, con y sin paréntesis donde corresponda
- [x] 6.15 Test: rechaza mutación y definición de datos
- [x] 6.16 Test: rechaza dos sentencias, acepta el punto y coma final y el punto y coma dentro de un literal
- [x] 6.17 Test: una palabra prohibida dentro de un comentario o de un literal no rechaza una consulta legítima
- [x] 6.18 Test: un comentario de bloque intercalado entre el nombre de la función y su paréntesis no evade
- [x] 6.19 Test de integración: el ataque autocontenido ejecutado por el carril es rechazado, o devuelve el alcance legítimo del actor y nunca el global
- [x] 6.20 Verificar en rojo: quitar la emisión de identificadores entrecomillados hace fallar los tests del ataque

## 7. Ejecución envuelta (ARS-32)

- [x] 7.1 Abrir conexión y transacción nuevas por ejecución, declaradas de solo lectura
- [x] 7.2 Fijar el actor con alcance de transacción, con el identificador de `identity.users` del usuario autenticado
- [x] 7.3 Fijar el timeout de sentencia dentro de la transacción y el timeout de comando en el cliente
- [x] 7.4 Envolver la consulta generada pidiendo una fila más que el tope
- [x] 7.5 Descartar la fila sonda y devolver el indicador de truncado, sin ningún conteo del total
- [x] 7.6 Registrar el ejecutor en el módulo, con el tope y los timeouts en las opciones
- [x] 7.7 Test: cada ejecución abre transacción nueva, incluido el reintento
- [x] 7.8 Test: una escritura dentro de la transacción es rechazada por el motor
- [x] 7.9 Test: el ajuste del actor está vacío al reutilizar la misma conexión física del pool
- [x] 7.10 Test: dos turnos consecutivos con actores distintos aplican cada uno el suyo
- [x] 7.11 Test: por debajo del tope no marca truncado; por encima recorta y marca; exactamente en el tope no marca
- [x] 7.12 Test: el resultado no lleva ningún total sin recortar
- [x] 7.13 Test: el timeout de sentencia está fijado dentro de la transacción
- [x] 7.14 Test: un identificador del directorio externo usado como actor falla de forma visible

## 8. Política de abstención (ARS-33)

- [x] 8.1 Guard de resultado que reconoce cero filas **y** la fila única de nulos
- [x] 8.2 Consultar el alcance global del actor antes de decidir el reintento
- [x] 8.3 No gastar el reintento cuando el actor no es global y el resultado es vacío
- [x] 8.4 Resolver los siete casos de abstención con su estado y su texto
- [x] 8.5 Reglas de abstención dentro del prompt de redacción: prohibición de inexistencia con actor no global, marco de alcance, prohibición de conteo con truncado
- [x] 8.6 Test: cero filas y fila de nulos se consideran vacío; una fila con cero y una fila con algún valor no nulo, no
- [x] 8.7 Test: con actor acotado, un vacío no gasta el reintento
- [x] 8.8 Test: con actor global, el reintento se comporta como en el caso base
- [x] 8.9 Test: dato existente sin permiso responde falta de acceso, no inexistencia
- [x] 8.10 Test: ninguna respuesta menciona cuántas filas quedaron afuera
- [x] 8.11 Test: el prompt de redacción lleva las reglas correspondientes a cada caso

## 9. Redacción (ARS-34)

- [x] 9.1 Segunda llamada al modelo con temperatura baja no cero y sin prefijo cacheado
- [x] 9.2 Prompt de redacción con las filas, el indicador de truncado y el marco de alcance
- [x] 9.3 Devolver la pregunta interpretada solo cuando difiere del mensaje del usuario
- [x] 9.4 Exponer el razonamiento tal cual, sin llamada adicional
- [x] 9.5 Sanear los textos de rechazo: sin esquema, sin tablas, sin columnas, sin SQL, sin errores crudos
- [x] 9.6 Test: la solicitud de redacción lleva temperatura mayor que cero y menor que uno
- [x] 9.7 Test: las filas llegan al prompt de redacción
- [x] 9.8 Test: la pregunta interpretada se devuelve cuando difiere y no cuando coincide
- [x] 9.9 Test: el razonamiento se expone sin consumir una llamada extra
- [x] 9.10 Test: los textos de rechazo no contienen vocabulario de esquema ni el error crudo del motor

## 10. Orquestador del carril

- [x] 10.1 Componer las siete piezas en el servicio del carril, con el resultado del turno como valor de retorno
- [x] 10.2 Elegir la conexión de lectura según el permiso de datos personales del actor, dejando el enmascaramiento para la épica de datos sensibles
- [x] 10.3 Registrar el carril en el módulo
- [x] 10.4 Test de punta a punta con proveedor guionado: una pregunta de cobertura de cátedra responde correctamente y acotada al alcance del actor
- [x] 10.5 Test: el turno completo no supera el techo de llamadas al modelo
- [x] 10.6 Test de arquitectura: el carril no referencia ningún módulo ajeno

## 11. Documentación en el mismo cambio

- [x] 11.1 `docs/architecture/domains/asistente.md` — el carril, sus siete piezas y las dos llamadas al modelo
- [x] 11.2 `docs/architecture/data-model.md` — los comentarios de esquema como parte del contrato con el asistente
- [x] 11.3 `backend/src/Modules.Asistente/README.md` — cómo agregar un ejemplo al catálogo y cómo se recalcula el prefijo
- [x] 11.4 Verificar que el grafo de dependencias no cambia: sin edges nuevos
