## Contexto

El módulo ya tiene tres piezas que este cambio reusa en lugar de duplicar:

- `NormalizadorLexico` — minúscula, sin acentos, sin puntuación. Es el mismo texto contra el que ya trabajan el enrutador social, el selector de ejemplos y el detector de cambio de tema.
- `CatalogoDeEntidades` / `IIndiceDeEntidades` — materias y personas leídas de la base y cacheadas, con sus colisiones ya calculadas. Su propio comentario anticipa este uso: «lo va a necesitar el enrutador de dominio de la épica siguiente para resolver sus slots».
- `LectorDeCatalogo` — el patrón de leer `pg_catalog` preguntando «¿qué puedo leer yo?», con `search_path` vacío y todo calificado.

Lo que falta es el vocabulario del **trámite**: estados, novedades, tipos de baja y cargos.

## Decisiones

### D1 — El catálogo es un archivo declarativo embebido, no código

Un JSON en `Recursos/`, hermano de `ejemplos-sql.json` y cargado igual.

**Por qué no código**: el criterio de aceptación pide que el catálogo sea «un archivo declarativo, no código disperso», y el motivo es operativo. Una intención mal reconocida se diagnostica leyendo una tabla de cinco filas; repartida en `if`s se diagnostica leyendo el módulo. Además el archivo es lo que hace barata la disciplina de «una intención nueva, un caso de prueba»: el test itera el catálogo, así que una intención sin prueba no puede entrar.

**Costo aceptado**: el JSON no tiene tipos, así que una intención mal escrita falla al cargar y no al compilar. Se compensa con un test que valida el catálogo entero al arrancar la suite —clases de slot existentes, destinos no vacíos, términos normalizados— y falla nombrando la intención culpable.

### D2 — Los valores del trámite se leen de los `CHECK`, no de una lista

`estado`, `novedad` y `tipo_baja` no tienen tabla: son restricciones `CHECK ... IN (...)` sobre `designaciones.pedidos`. Los cargos sí tienen tabla.

**Se leen de `pg_constraint`**, parseando la lista de literales de la restricción.

**La alternativa era escribirlos en el catálogo**, y es exactamente lo que el ticket prohíbe. El problema no es la duplicación sino el modo de fallar: cuando alguien agregue un estado, la lista escrita a mano no rompe nada. El resolutor simplemente deja de reconocer ese estado, la pregunta cae al carril SQL y nadie se entera de que había un camino más barato. Un desajuste que no falla es un desajuste que dura.

**Costo aceptado**: parsear el texto de un `CHECK` es más frágil que leer una tabla. Se acota pidiendo la forma exacta —`columna = ANY (ARRAY[...])`, que es como PostgreSQL normaliza `IN`— y fallando ruidosamente si la restricción no tiene esa forma, en lugar de devolver una lista vacía. Una lista vacía silenciosa es el mismo modo de fallar que la decisión quiere evitar.

**La deuda queda anotada**: el día que estos vocabularios pasen a tablas de catálogo —como ya pasó con `cargos`—, este lector se simplifica y el resto no se entera.

### D3 — Reconocer es «están todos los términos», no una expresión regular

Cada intención declara un conjunto de términos que tienen que aparecer en la pregunta normalizada. Se reconoce si están **todos**.

**Por qué no regex**: una expresión regular sobre lenguaje natural es una promesa de precisión que el orden de las palabras no sostiene. «¿en qué estado está el pedido de Pérez?» y «¿el pedido de Pérez en qué estado está?» son la misma pregunta y romperían la misma regex. El conjunto de términos es insensible al orden por construcción.

**Costo aceptado**: es más laxo, así que va a reconocer intenciones donde un humano vería otra cosa. Eso está cubierto por la regla de los slots (D4) y por el default a SQL del enrutador, que vive en el cambio siguiente.

### D4 — Un slot que colisiona no se resuelve

Si el término de un slot corresponde a más de un valor del dominio, el slot queda **sin resolver**, y una intención con un slot sin resolver **no es una intención reconocida**.

Es la misma regla del detector de ambigüedad, y por el mismo motivo, escrito en el sentido de este carril: enrutar a la API con el «Pérez» equivocado devuelve las filas de otro Pérez, y esa respuesta es indistinguible de la correcta para quien preguntó. Con dos Pérez, no resolver es lo único honesto.

**Y la asimetría es deliberada**: no resolver manda la pregunta al carril caro, que es el que puede responderla —o pedir la aclaración—. Fallar hacia el carril que puede responder es el default seguro de toda la épica.

### D5 — El catálogo nombra un destino; no lo llama

Cada intención declara un destino como cadena lógica: `designaciones/pedidos-por-persona`. Nada más.

Esto es lo que mantiene el corte del ticket en pie. El módulo no gana ninguna referencia nueva, los tests de arquitectura siguen exigiendo que solo referencie `ArsDocendi.Shared`, y el acuerdo del equipo sobre los edges se pide cuando haga falta de verdad —al cablear el enrutador— y no antes.

### D6 — Cero llamadas al modelo; la base se lee una vez

El reconocimiento y la resolución son puros sobre datos ya cargados. Los dos catálogos —entidades y vocabulario del trámite— se cargan **perezosamente y se cachean**, con el mismo patrón y por el mismo motivo que el índice de entidades: el invariante #3 pide que el `ping` responda con la base detenida, así que nada se construye al arrancar el Host.

### D7 — El vocabulario se compone al lado del índice, no adentro

`CatalogoDeEntidades` queda **exactamente como está**. El vocabulario del trámite vive en un catálogo propio, y una pieza nueva —`ICatalogoDelDominio`— compone los dos para el resolutor.

**La alternativa era ampliar el índice existente**, que es lo que primero parece más simple: un solo catálogo, un solo caché, una sola consulta. Se descartó al ver quiénes lo consumen. El detector de ambigüedad dispara cuando un término del índice colisiona, y el de cambio de tema mide el solapamiento de entidades entre dos preguntas. Meterles «borrador», «Alta» y «Titular» adentro les cambia el comportamiento en silencio: palabras que hoy son texto común pasarían a ser entidades del dominio, y el cambio de tema empezaría a medir contra un vocabulario que no eligió.

Componer afuera hace que esos dos detectores queden intactos **por construcción** y no por un test que lo vigile. El costo es una interfaz más y una segunda consulta a la base, ambas baratas y cacheadas igual que la primera.

## Riesgos

- **El catálogo chico se va a querer agrandar.** Cada intención nueva agranda la superficie donde un reconocimiento incorrecto manda una pregunta al carril que no la responde. La mitigación es la disciplina, no el código: un caso de prueba por intención, con sus slots resueltos y sin resolver, y el test que itera el catálogo y falla si falta.
- **El parseo del `CHECK` depende de cómo PostgreSQL normaliza la restricción.** Un cambio de versión del motor podría cambiar el texto. Falla ruidoso y hay un test contra la base real, así que se entera el CI y no producción.
