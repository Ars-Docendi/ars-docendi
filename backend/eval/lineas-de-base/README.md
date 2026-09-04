# Líneas de base del gate de regresión

Un archivo por eje, con el **veredicto de cada ítem** de la corrida que se tomó como
referencia, más los tres hashes del sellado.

## Por qué lock por ítem y no un umbral

Con pocas decenas de ítems puntuados, **tres que se rompen y tres que se arreglan dan
delta cero** y pasan cualquier umbral, mientras el asistente cambió de comportamiento.
Y un solo ítem vale un par de puntos porcentuales: un umbral fino sería ruido y uno
grueso no detectaría nada.

Ventaja adicional: el lock **no depende del tamaño del dataset**. Un dataset de esta
escala tiene un intervalo de confianza de varios puntos, así que ninguna comparación de
agregados puede sostener una afirmación de mejora o regresión. El lock esquiva el
problema en vez de intentar resolverlo creciendo el dataset.

## Cuándo se regenera

**Nunca automáticamente.** Si regenerar fuera un efecto de correr el eje, una regresión
real se absorbería sola en el primer commit que la causara y el gate no detectaría nada
nunca.

Se regenera a mano, y el diff del archivo es lo que se revisa:

- Cuando cambió el **dataset** (se agregaron o editaron ítems).
- Cuando cambió el **prefijo del prompt** (se tocó el esquema o las instrucciones).
- Cuando cambió el **fixture**.
- Cuando una regresión se acepta a propósito, con el motivo en el mensaje del commit.

En los tres primeros casos el gate **se niega a comparar** y lo pide él mismo: los
hashes identifican contra qué se midió, y comparar ítem a ítem con un sello distinto
sería comparar dos cosas que no son la misma.

## Qué cuenta como «pasaba»

Solo los dos aciertos: traducción correcta y abstención correcta.

La abstención sobre algo contestable **no** cuenta como pasar —es una falta de
capacidad, aunque no reste puntos—, así que un ítem que va de «tradujo bien» a «se
abstuvo» **es** una regresión y el gate la ve.

## Todavía no hay ninguna

Generar una línea de base exige una corrida real, y una corrida real exige un proveedor
de modelo que todavía no está elegido (TD-008). Este directorio queda con su
documentación y sin archivos: un archivo de línea de base generado con el proveedor
simulado registraría el comportamiento del simulador, no el del asistente.
