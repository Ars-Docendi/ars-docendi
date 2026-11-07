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

Congeladas **en vivo**, sin cassettes, contra `claude-sonnet-5` el 2026-11-07 (tarde),
con el prefijo `263d74f4f90389eb…`, que incluye `comisiones`, las solicitudes de aula
(`asistente-lee-aulas`), los proyectos del portal (`asistente-portal-jefe-catedra`) y la
regla de generación que pide las columnas clave de los enlaces
(`asistente-enlaces-personas-y-aulas`). Capacidad suma `cap-051`..`cap-061` y robustez
`rob-019`..`rob-022`.

| Eje       | Ítems | Aciertos | Normalizado |
| --------- | ----- | -------- | ----------- |
| Capacidad | 61    | 60       | 98,4 %      |
| Robustez  | 22    | 21       | 95,5 %      |
| Diálogo   | 11    | 11       | 100,0 %     |
| Social    | 20    | 20       | 100,0 %     |

Los dos que no aciertan (`cap-004`, `rob-022`) acertaron en las otras dos corridas del
mismo código y quedan en la tabla de abajo. Las líneas anteriores, de comisiones, están
en [`antes-de-aulas-y-portal/`](./antes-de-aulas-y-portal/README.md).

Antes de congelar, tres corridas en vivo mostraron fallas que se corrigieron:

- **«Solicitudes» a secas se iba a las reservas de aula** (`cap-008`, `cap-012`,
  `cap-023`): el comentario de `aulas.solicitudes_reserva` dice ahora que esa tabla es
  sólo para preguntas que nombran «aula» o «reserva»; lo demás son pedidos de
  designación.
- **«Solicitudes urgentes» dejó de traducirse** (`cap-016`, `rob-012`): una migración
  nueva agrega «urgente» como sinónimo en el comentario de `designaciones.pedidos.prioritario`.
- **`cap-032` («¿Qué proyectos de investigación cargó cada docente?») pasó a
  contestable**: los proyectos del portal ahora se conceden.
- **Referencias que exigían columnas que la pregunta no pide** (`cap-040`, `cap-051`,
  `cap-054`, `cap-056`): el comparador exige que la respuesta contenga todas las columnas
  de la referencia, así que la referencia proyecta sólo lo que la pregunta pide.
- **«De mi cátedra» admitía dos lecturas** (`cap-057`, `cap-061`, `rob-021`): ¿el propio
  jefe cuenta? La RLS ya acota a quien pregunta, así que las preguntas no lo dicen.
- **`cap-046`** se reformuló otra vez («nunca figuraron…»), para no ser un superconjunto
  de un ejemplo del catálogo.
- **Una columna de texto libre suprimida se narraba como «no hay datos»** (`cap-056`,
  motivos de cancelación): la redacción recibe ahora un aviso, sin el nombre ni el valor de
  la columna, de que ese texto está en la tabla.

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
| `cap-047`                           | 4 de 5                 | no agrupa por período (D10)         |
| `cap-004`                           | 2 de 3                 | se abstiene ante contestable        |
| `rob-022`                           | 2 de 3                 | se abstiene ante «mi cátedra»       |

Los tres últimos se observaron en las corridas de la tarde, con aulas y portal en el prefijo. `dia-003-pivote-duro#1` ya oscilaba antes de comisiones.

**Decisión: se marcan como tolerantes** (change `evaluador-items-inestables`). Viven en
[`inestables.json`](./inestables.json), por eje y con su motivo:
`{ "capacidad": { "cap-009": "motivo" } }`. En diálogo el id es el de la línea de base,
por turno (`dia-001-seguimiento#2`).

- Si un ítem listado pasa de acierto a falla, el gate **no devuelve 4**: lo informa como
  «Oscilaciones conocidas» en la consola, en una sección del reporte del eje y en el
  resumen final (cuántas se toleraron).
- Todo lo demás conserva el lock estricto. Una mejora en un ítem listado se informa como
  cualquier otra.
- El archivo está **fuera del sello**: no entra en el hash del dataset, así que listar o
  quitar un ítem no obliga a regenerar las líneas de base.
- Un id que no está en la línea de base de su eje (o un eje desconocido, o un motivo
  vacío) corta la corrida con código 64: la lista no se pudre en silencio.

Para **agregar** un ítem, sumá su id y el motivo con la evidencia (cuántas corridas
oscilaron) en el eje que corresponde. Para **quitarlo**, borrá la entrada: vuelve al lock
estricto. Un ítem que se reformula o se arregla debe salir de la lista en el mismo diff.

Las de antes de portal están en [`antes-de-portal/`](./antes-de-portal/README.md),
que explica qué costó ese cambio.

## Por qué el sello es lo primero que mira el gate

Los tres hashes identifican **contra qué** se midió. Con cualquiera distinto el gate se
niega a comparar y lo dice —«cambió el prefijo del prompt. Regenerá la línea de base y
volvé a correr»— en vez de emitir un veredicto sobre dos cosas que no son la misma.

Es lo que va a pasar cuando `portal` entre al esquema del asistente: el prefijo cambia,
las cuatro líneas quedan obsoletas de golpe, y hay que regenerarlas con una corrida en
vivo contra el modelo de producción.
