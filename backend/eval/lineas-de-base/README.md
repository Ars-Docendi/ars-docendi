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

Congeladas **en vivo**, sin cassettes, contra `claude-sonnet-5` el 2026-11-07, con el
prefijo `9ae0bab9468ee2df…` que ya incluye el schema `comisiones`
(change `asistente-lee-comisiones`). Capacidad suma `cap-040`..`cap-050` y robustez
`rob-016`..`rob-018`.

| Eje       | Ítems | Aciertos | Normalizado |
| --------- | ----- | -------- | ----------- |
| Capacidad | 50    | 49       | 98,0 %      |
| Robustez  | 18    | 18       | 100,0 %     |
| Diálogo   | 11    | 9        | 81,8 %      |
| Social    | 20    | 20       | 100,0 %     |

Los tres que no aciertan están en la tabla de abajo: oscilan, no son regresiones de
comisiones. Qué se movió respecto de las anteriores está en
[`antes-de-comisiones/`](./antes-de-comisiones/README.md).

Antes de congelar se corrigieron tres ítems propios del change, con el motivo
registrado:

- `cap-047` («¿Cuántas comisiones hay de Investigación Operativa?») contaba sin
  agrupar. Por la decisión D10, una pregunta sin período se responde por cada período
  del año más reciente: la referencia agrupa por `periodo_lectivo_nombre`.
- `cap-046` admitía dos lecturas (por período o sobre todos) y el modelo alternaba
  entre ellas. La pregunta dice ahora «en ningún período importado».
- `rob-005` («¿Quiénes dictan Bases de Dattos?») respondía con los docentes de las
  comisiones. Por D8, «quién dicta» una materia se responde por designaciones: se
  reforzó el comentario de `sga_comision_docentes` y se agregó un ejemplo de «quiénes
  dictan» resuelto por designaciones. Pasó en las tres corridas siguientes.

## Ítems que oscilan en vivo

Los cassettes ya no existen (Anthropic dejó de ser proveedor de producción), así que
el gate corre en vivo. Con lock por ítem, un ítem que oscila da regresiones falsas.
Cuatro corridas del 2026-11-07, mismo modelo y mismo fixture (la primera, antes de los
ajustes de arriba), mostraron estos:

| Ítem                                | Aciertos en 4 corridas | Cuando falla                        |
| ----------------------------------- | ---------------------- | ----------------------------------- |
| `cap-009`                           | 3                      | se abstiene ante contestable        |
| `cap-020`                           | 3                      | responde en vez de pedir aclaración |
| `dia-001-seguimiento#2`             | 2                      | se abstiene ante contestable        |
| `dia-003-pivote-duro#1`             | 2                      | se abstiene o traduce mal           |
| `dia-005-referencia-al-resultado#2` | 3                      | se abstiene ante contestable        |

Ninguno toca `comisiones`. `dia-003-pivote-duro#1` ya oscilaba antes de este change.
Sigue pendiente decidir si se marcan como tolerantes o se reformulan; hasta entonces,
una «regresión» en uno de estos ítems hay que confirmarla con otra corrida antes de
leerla como tal.

Las de antes de portal están en [`antes-de-portal/`](./antes-de-portal/README.md),
que explica qué costó ese cambio.

## Por qué el sello es lo primero que mira el gate

Los tres hashes identifican **contra qué** se midió. Con cualquiera distinto el gate se
niega a comparar y lo dice —«cambió el prefijo del prompt. Regenerá la línea de base y
volvé a correr»— en vez de emitir un veredicto sobre dos cosas que no son la misma.

Es lo que va a pasar cuando `portal` entre al esquema del asistente: el prefijo cambia,
las cuatro líneas quedan obsoletas de golpe, y hay que regenerarlas con una corrida en
vivo contra el modelo de producción.
