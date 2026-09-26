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

## Cómo se congela

```bash
dotnet run --project backend/eval/ArsDocendi.Evaluacion -- --congelar
```

Sin `--congelar`, cada eje que tenga línea de base se compara contra ella y una
regresión devuelve **4**. Con la bandera, se escribe y **no** se compara: congelar ES
el acto de aceptar el comportamiento actual como referencia, y compararlo contra sí
mismo en la misma corrida no informaría nada.

## Las cuatro vigentes

Congeladas reproduciendo los 107 cassettes, así que **no costaron una corrida
financiada**. Modelo `claude-sonnet-5`.

| Eje       | Ítems | Aciertos | Normalizado |
| --------- | ----- | -------- | ----------- |
| Capacidad | 24    | 23       | 95,8 %      |
| Robustez  | 15    | 14       | 93,3 %      |
| Diálogo   | 9     | 8        | 88,9 %      |
| Social    | 20    | 20       | 100,0 %     |

El ítem que no acierta en diálogo es `dia-003-pivote-duro#1`, que **se abstiene ante
algo contestable**. Queda congelado así a propósito: la línea de base registra el
comportamiento que hay, no el que se querría. Que mejore va a aparecer como «mejora»
en el gate, que es donde se lo quiere ver.

**Ojo con ese ítem si el gate llegara a correr contra el modelo en vivo.** Reproducido
de cassette es determinista —tres corridas dieron reportes idénticos byte a byte—, pero
en vivo osciló entre acierto y abstención sin que nada cambiara. Un ítem que oscila
produce regresiones falsas con lock por ítem. Mientras el gate reproduzca cassettes, no
es un problema; el día que se corra en vivo, hay que decidir si se lo marca como
tolerante o se lo reformula.

## Las cuatro vigentes son de después de portal

Se regeneraron con la corrida financiada que regrabó los cassettes contra el
prefijo nuevo (`0147ac99…` y sucesores, 20 tablas). Las de antes están en
[`antes-de-portal/`](./antes-de-portal/README.md), que explica qué costó el cambio.

| Eje       | Ítems | Aciertos | Normalizado |
| --------- | ----- | -------- | ----------- |
| Capacidad | 32    | 31       | 96,9 %      |
| Robustez  | 15    | 13       | 86,7 %      |
| Diálogo   | 9     | 8        | 88,9 %      |
| Social    | 20    | 20       | 100,0 %     |

## Por qué el sello es lo primero que mira el gate

Los tres hashes identifican **contra qué** se midió. Con cualquiera distinto el gate se
niega a comparar y lo dice —«cambió el prefijo del prompt. Regenerá la línea de base y
volvé a correr»— en vez de emitir un veredicto sobre dos cosas que no son la misma.

Es lo que va a pasar cuando `portal` entre al esquema del asistente: el prefijo cambia,
las cuatro líneas quedan obsoletas de golpe, y hay que regenerarlas en la misma corrida
financiada que regrabe los cassettes.
