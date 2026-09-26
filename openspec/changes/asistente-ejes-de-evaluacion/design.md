# Diseño — Los tres ejes que faltan y el gate

## Decisiones

### D1 — La reutilización byte-idéntica es estructural, no vigilada

El invariante que no se puede romper es que cada ítem de robustez use la consulta de referencia **byte-idéntica** de su ítem de origen. La forma obvia es copiarla y poner un test que compare.

Es peor. Un test que compara copias falla **después** de que alguien las desincronizó, y el error de copiar es exactamente el que un humano comete al agregar el ítem quince.

Acá el ítem de robustez **no tiene** campo de consulta: declara el identificador de su origen, y el cargador la resuelve del dataset de capacidad. No hay dos copias que puedan diferir. Lo que el test verifica es lo que queda: que un ítem que declara consulta propia sea rechazado al cargar, y que un origen inexistente también.

Efecto secundario deseable: agregar una perturbación cuesta dos líneas, así que el eje puede crecer sin ceremonia.

### D2 — El eje de diálogo mide lo que NO tiene que aparecer

Un diálogo puede dar 100% mientras el sistema arrastra silenciosamente el filtro del turno anterior: si el turno de prueba es autocontenido, el arrastre **no se ve en el resultado**.

Cada turno declara `terminos_prohibidos`, y el runner los busca en la **pregunta interpretada** —no en la respuesta—. Es la única superficie donde el arrastre es visible antes de convertirse en filas.

El pivote duro es el caso donde esto muerde: turno uno sobre una entidad, turno dos sobre otra **sin ninguna referencia anafórica**, con los términos del primero prohibidos en el segundo.

**La consecuencia esperada es que empiece rojo.** Ese es el punto: es la línea de base honesta contra la cual medir las mejoras del reescritor, y registrarla es parte del entregable.

### D3 — El eje social mide tokens, y por eso el runner los mide él

«Cero tokens de entrada» no se puede afirmar mirando el resultado del turno: el contrato expone llamadas al modelo, no tokens.

El runner recibe un `MedidorDeConsumo`, que es un decorador del proveedor **y** el instrumento: el llamador envuelve el proveedor real con él y le pasa la misma instancia al runner. Así el número que el eje afirma sale del transporte y no de una inferencia.

Cero llamadas implica cero tokens, pero no al revés: un proveedor que devuelve vacío consumió entrada igual. Medir tokens y no llamadas es lo que hace que el assert signifique lo que dice.

### D4 — La trampa del proveedor caído se cubre al revés que en el eje de capacidad

En el eje de capacidad, sin crédito todos los ítems infactibles pasan espuriamente porque el turno falla y devuelve abstención. Ahí la defensa es que un turno degradado cuente como **fallo**.

Acá se agrava y se invierte: el assert es «consumió cero tokens», y **un proveedor caído consume cero tokens en todos los ítems**. La corrida entera daría verde perfecto.

Por eso el runner social falla ruidosamente si **todos** los turnos consumieron cero: en una corrida sana, los ítems negativos —preguntas legítimas que el enrutador no debe capturar— tienen que haber llegado al modelo. Que ninguno lo haya hecho significa que no hubo modelo.

### D5 — El gate es lock por ítem y no umbral agregado

Con pocas decenas de ítems, tres que se rompen y tres que se arreglan dan delta cero y pasan cualquier umbral mientras el asistente cambió de comportamiento. Y un solo ítem vale un par de puntos porcentuales: un umbral fino sería ruido y uno grueso no detectaría nada.

Ventaja adicional, y no es menor: el lock **no depende del tamaño del dataset**. Un dataset de esta escala tiene un intervalo de confianza de varios puntos, así que ninguna comparación de agregados puede sostener una afirmación de mejora o regresión. El lock esquiva el problema en vez de intentar resolverlo creciendo el dataset.

### D6 — Si cambió el sello, el gate no compara: exige regenerar

Los tres hashes —prefijo, dataset, fixture— identifican **contra qué** se midió. Con cualquiera distinto, comparar ítem a ítem sería comparar dos cosas que no son la misma.

El gate no regenera solo. Regenerar es un acto explícito y versionado: si fuera automático, una regresión real se absorbería sola en el primer commit que tocara el prompt.

### D7 — Cuatro reportes separados

Los ejes **no son comparables entre sí**: un 0,7 de robustez y un 0,7 de diálogo no significan lo mismo ni valen lo mismo. Un reporte único invitaría a promediarlos, y el promedio de cuatro cosas incomparables no mide nada.

## Alternativas descartadas

| Alternativa                                                          | Por qué no                                                        |
| -------------------------------------------------------------------- | ----------------------------------------------------------------- |
| Copiar la consulta en cada ítem de robustez, con un test que compare | El test falla después del error; la derivación lo hace imposible. |
| Medir el diálogo solo por el resultado del último turno              | El arrastre no se ve ahí: un turno autocontenido acierta igual.   |
| Afirmar «cero tokens» mirando `LlamadasAlModelo`                     | Cero llamadas implica cero tokens, no al revés.                   |
| Umbral agregado para el gate                                         | Tres rotos y tres arreglados dan delta cero.                      |
| Regenerar la línea de base automáticamente                           | Absorbe la regresión en el mismo commit que la causó.             |
| Un reporte único con los cuatro ejes                                 | Invita a promediar cosas incomparables.                           |
