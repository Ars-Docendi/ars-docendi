# Evaluación del asistente conversacional

La métrica primaria del proyecto es **corrección con abstención**: nunca afirmar
algo falso, y callarse cuando corresponde. Esto es con qué se la mide.

## Por qué hay dos proyectos

| Proyecto                       | Dónde           | En la solución | Qué tiene                                                         |
| ------------------------------ | --------------- | -------------- | ----------------------------------------------------------------- |
| `ArsDocendi.Evaluacion.Nucleo` | `backend/src/`  | **Sí**         | Generador del fixture, dataset, puntuación, preflight, reporte    |
| `ArsDocendi.Evaluacion`        | `backend/eval/` | **No**         | El ejecutable. Lo único que instancia un proveedor de modelo real |

El reparto no es por «qué es de evaluación» sino por **qué cuesta dinero**.

El CI corre los tests de la solución **sin filtro**. Un proyecto que ejecuta la API
real adentro correría decenas de veces por semana, y el síntoma de olvidarlo no es
un test rojo: **es una factura**, que se descubre a fin de mes. Por eso el
ejecutable está afuera, y por eso hay un guard adentro —`ExclusionDelEvaluadorTests`—
que lee `backend/ArsDocendi.slnx` y falla si el proyecto vuelve a entrar.

Un filtro en el YAML del CI se descartó a propósito: vive en el YAML, y un
identificador mal escrito o un merge que revierta el flag cuesta dinero sin que
nadie lo note.

Sacar **todo** lo de evaluación de la solución tampoco servía: dejaría sin tests en
el CI justamente a las piezas donde un error hace que el número mienta.

## Cómo se corre

```bash
dotnet run --project backend/eval/ArsDocendi.Evaluacion
```

Necesita tres cosas:

1. **Una base PostgreSQL migrada**, con el fixture aplicado.
2. **Las cadenas de conexión del asistente** en el ambiente (`Asistente__RolSoloLectura`
   y compañía; ver `backend/src/Modules.Asistente/README.md`).
3. **Un proveedor de modelo real.**

> **Hoy falta la tercera.** `IProveedorDeModelo` tiene una sola implementación —la
> simulada— y el preflight la rechaza a propósito. Elegir proveedor y modelo, y
> conseguir una clave, es una decisión de producto y de costo. Cuando esté tomada,
> se implementa la interfaz y se la construye en `Program.cs`; nada más del
> pipeline cambia.

El evaluador **nunca** corre en el CI. No es una convención: es estructural.

## El fixture

Sintético, determinista y sin reloj. Correr el generador dos veces produce el mismo
texto byte a byte: los identificadores se derivan del índice y las fechas de una
fecha ancla fija.

**Lo «actual» se expresa con banderas del dominio** —el período con `activo = true`,
la designación con `vigente_hasta IS NULL`— y nunca comparando contra el reloj. Un
dataset cuyo resultado esperado cambia con el calendario mide qué día lo corriste.

**Las colisiones son parte del contrato**, con cardinalidades declaradas y
verificadas por test:

| Colisión                                              | Cardinalidad |
| ----------------------------------------------------- | ------------ |
| «Análisis Matemático»                                 | 3 carreras   |
| «Algoritmos y Estructuras de Datos», «Inglés Técnico» | 2 carreras   |
| Apellido «Gómez»                                      | 3 personas   |
| «Fernández», «Rodríguez», «Suárez»                    | 2 personas   |

El detector de ambigüedad —épica posterior— dispara con estas colisiones. Si el
fixture no las reprodujera, los ítems de diálogo que lo prueban **darían verde sin
medir nada**.

Cada sección del generador usa **su propia fuente de aleatoriedad**. Con una sola
compartida, agregar una persona correría todos los valores de las secciones
siguientes y rompería ítems que nadie tocó.

## El dataset

`datasets/capacidad.json`, estratificado por dificultad técnica:
`consulta_simple` · `filtro_temporal` · `cruce_de_tablas` · `agregacion` ·
`no_contestable` · `ambigua`.

Guarda la **consulta** de referencia, no su resultado. Las referencias se ejecutan
en vivo contra el fixture en cada corrida, con el **mismo actor** que el ítem: con
resultados guardados, cualquier cambio del fixture desincroniza el dataset en
silencio y la métrica pasa a medir esa diferencia.

**Invariante**: el dataset y el catálogo de ejemplos del módulo son **disjuntos**.
Verificado por test, y no solo por igualdad: también se rechaza que una pregunta
sea un subconjunto de la otra. Si se solaparan, la métrica mediría cuán bien el
sistema reproduce ejemplos que ya vio — y como el catálogo de capacidades deriva
sus sugerencias de esos ejemplos, el asistente estaría proponiendo las preguntas
con las que se lo evalúa.

## La puntuación

| Situación                        | Vale                          |
| -------------------------------- | ----------------------------- |
| Factible, traducción correcta    | **+1**                        |
| Infactible, se abstuvo sin error | **+1**                        |
| Factible, se abstuvo             | 0                             |
| Factible, traducción incorrecta  | **−penalización**             |
| Infactible, intentó responder    | **−penalización**             |
| El turno falló                   | 0, y cuenta en el denominador |

Abstenerse ante algo contestable es una falta de capacidad, no una mentira: no
suma, tampoco resta. Responder mal sí resta.

Se reporta con **tres penalizaciones** —0,5 · 1,0 · 2,0— porque cuánto vale una
respuesta falsa frente a una abstención es una decisión de producto que todavía no
está tomada. En vez de elegir un número y esconder la elección adentro de la
métrica, el reporte muestra cómo cambia el resultado según cuánto se castigue
mentir.

La comparación es por **conjunto de filas**, no por texto de consulta: dos
consultas escritas distinto pueden ser la misma respuesta. Los nombres de columna
no se comparan, y el orden se ignora salvo que el ítem lo declare.

## El preflight

**Sin crédito de API el eval no falla: miente.**

El request devuelve una abstención con error seteado y métricas en cero, así que
los ítems no contestables **pasan espuriamente** y el reporte muestra un número
bajo que parece una regresión del modelo. La señal de diagnóstico es entrada y
salida en cero, con latencias de milisegundos en vez de segundos.

Antes de correr un solo ítem, el runner pide una completación trivial y verifica
tres cosas por separado:

1. que no haya excepción — el proveedor está caído;
2. que la respuesta **no sea simulada** — se corrió sin configurar el real;
3. que los tokens sean **mayores que cero** — hay proveedor pero no hay crédito.

Si falla, el runner devuelve un código distinto de cero y **no escribe nada**. Un
reporte escrito sobre una corrida inválida es peor que no tener reporte: el que no
existe se nota, el que miente no.

La misma trampa se cubre del otro lado, en el scoring: un turno que resolvió
servicio degradado cuenta como **fallo**, nunca como abstención.

## El sello de los reportes

Cada reporte estampa tres hashes en su encabezado: el del prefijo del prompt, el
del dataset y el del fixture.

Sin el sello, el problema se repite en el próximo refactor de esquema: los reportes
quedan describiendo datasets que ya no existen y los números del proyecto dejan de
ser reproducibles, sin que nadie lo note.

Los reportes van a `reportes/` y son **generados**: no se editan a mano.

## Qué falta

| Qué                                                | Épica |
| -------------------------------------------------- | ----- |
| Eje de robustez de fraseo                          | H4    |
| Eje de diálogo con chequeo negativo de arrastre    | H4    |
| Eje social y meta, con assert de costo cero        | H4    |
| Gate de regresión con lock por ítem                | H4    |
| **Una implementación de proveedor de modelo real** | —     |

El gate de regresión es lock **por ítem** y no umbral agregado: tres ítems que se
rompen y tres que se arreglan dan delta cero y pasan cualquier umbral. Necesita una
línea de base, y la línea de base sale de correr esto.
