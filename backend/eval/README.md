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
3. **Un proveedor de modelo real**, elegido por ambiente:

   ```bash
   Asistente__Proveedor=anthropic
   Asistente__ClaveDelProveedor=<la clave>
   ```

El proveedor **se resuelve del contenedor del módulo**, no se construye acá. El
`switch` de `ModuleExtensions` es el único registro de adaptadores: armar uno a mano
en el evaluador crearía una segunda forma de elegir proveedor, capaz de quedar en
desacuerdo con la de producción sin que nada falle. Además el que devuelve el
contenedor viene ya envuelto en el reintento, el techo por turno y el corte — que es
lo que corre de verdad. Medir sobre un proveedor desnudo mediría otro sistema.

Sin la tercera, el preflight rechaza y **no se escribe ningún reporte**. Es
deliberado: un reporte escrito sobre una corrida inválida es peor que no tener
reporte, porque el que no existe se nota y el que miente no.

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

## Los cuatro ejes

| Eje                    | Dataset          | Qué mide, que ningún otro ve                               |
| ---------------------- | ---------------- | ---------------------------------------------------------- |
| **Capacidad**          | `capacidad.json` | Si traduce la pregunta a la consulta correcta              |
| **Robustez de fraseo** | `robustez.json`  | Si entiende a la gente y no solo preguntas de manual       |
| **Diálogo**            | `dialogo.json`   | La capa conversacional entera, y si **arrastra**           |
| **Social y meta**      | `social.json`    | Qué captura el carril de cero tokens, y qué se come de más |

**Los cuatro reportes van a archivos separados y no se promedian.** Un 0,7 de robustez
y un 0,7 de diálogo no significan ni valen lo mismo, y el promedio de cuatro cosas
incomparables no mide nada.

### Robustez: la consulta se hereda, no se copia

Cada ítem de robustez es una pregunta del eje de capacidad dicha de otra manera —sin
tildes, con errores de tipeo, con sinónimos, parcial, coloquial—.

**Ninguno declara su propia consulta de referencia: la hereda del ítem de origen.** Lo
directo sería copiarla y poner un test que compare las dos, y es peor: un test que
compara copias falla _después_ de que alguien las desincronizó, y copiar mal es
exactamente el error que se comete al agregar el ítem quince. Al derivarla no hay dos
copias que puedan diferir.

El motivo por el que ese invariante importa: sin él, un fallo sería ambiguo. ¿No
entendió el fraseo, o no supo escribir la consulta?

### Diálogo: mide lo que NO tiene que aparecer

Un diálogo puede dar 100% mientras el sistema **arrastra silenciosamente** el filtro
del turno anterior: si el turno de prueba es autocontenido, el arrastre no cambia el
resultado y no se ve en ningún lado.

Cada turno declara `terminos_prohibidos`, y el runner los busca en la **pregunta
interpretada** —no en la respuesta—, que es la única superficie donde el arrastre es
visible antes de convertirse en filas. El chequeo va **antes** que la comparación de
resultados: un turno que arrastra puede acertar de casualidad, y contarlo como acierto
escondería el defecto.

El dataset incluye **pivotes duros**: turno uno sobre una entidad, turno dos sobre
otra sin ninguna referencia anafórica, con los términos del primero prohibidos.

> **Se espera que este eje empiece rojo**, y ése es el punto: es la línea de base
> honesta contra la cual medir las mejoras del reescritor.

### Social: el assert de costo cero ES la métrica

Los ítems sociales aprueban **solo si consumen cero tokens de entrada**. Si el saludo
costó tokens, el enrutador no lo capturó, por perfecta que haya sido la respuesta.

Se miden **tokens y no llamadas**: cero llamadas implica cero tokens, pero no al revés
—un proveedor que devuelve vacío consumió entrada igual—. El número sale del
transporte, con un decorador que es a la vez el instrumento.

Los ítems **negativos** son preguntas legítimas tomadas del eje de capacidad y de las
clases coloquial y parcial de robustez: el enrutador **no** debe capturarlas, y
capturarlas resta. Sin negativos, un enrutador que se come todo daría perfecto.

**La trampa del proveedor caído, agravada.** Acá el assert es «consumió cero tokens», y
un proveedor caído consume cero en **todos** los ítems: la corrida entera daría verde
perfecto. Por eso el runner social **aborta ruidosamente si todos los turnos
consumieron cero** — en una corrida sana, los negativos tienen que haber llegado al
modelo.

## El dataset de capacidad

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

| Situación                              | Vale                          |
| -------------------------------------- | ----------------------------- |
| Factible, traducción correcta          | **+1**                        |
| Infactible, se abstuvo sin error       | **+1**                        |
| Factible, se abstuvo                   | 0                             |
| Factible, traducción incorrecta        | **−penalización**             |
| Infactible, intentó responder          | **−penalización**             |
| El turno falló                         | 0, y cuenta en el denominador |
| La generación se cortó por presupuesto | 0, y cuenta en el denominador |

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
servicio degradado cuenta como **fallo**, nunca como abstención. Y una generación
cortada por el techo de tokens —que resuelve «no contestable» con el mismo texto que
una abstención— se reconoce por `categoria = truncado_en_generacion` y se cuenta
**aparte**: ni acierto ni fallo del modelo, con su propia fila en el reporte.

## El sello de los reportes

Cada reporte estampa tres hashes en su encabezado: el del prefijo del prompt, el
del dataset y el del fixture.

Sin el sello, el problema se repite en el próximo refactor de esquema: los reportes
quedan describiendo datasets que ya no existen y los números del proyecto dejan de
ser reproducibles, sin que nadie lo note.

Los reportes van a `reportes/` y son **generados**: no se editan a mano.

## El gate de regresión

Lock **por ítem** contra un archivo versionado en `lineas-de-base/`, no umbral
agregado. El motivo es aritmético: con pocas decenas de ítems, **tres que se rompen y
tres que se arreglan dan delta cero** y pasan cualquier umbral mientras el asistente
cambió de comportamiento. Y un solo ítem vale un par de puntos porcentuales: un umbral
fino sería ruido y uno grueso no detectaría nada.

Ventaja adicional: el lock **no depende del tamaño del dataset**. Un dataset de esta
escala tiene un intervalo de confianza de varios puntos, así que ninguna comparación de
agregados puede sostener una afirmación de mejora o regresión.

Si cambió cualquiera de los tres hashes del sellado, el gate **no compara**: exige
regenerar. Los hashes identifican contra qué se midió, y comparar ítem a ítem con un
sello distinto sería comparar dos cosas que no son la misma.

**La regeneración nunca es automática.** Si lo fuera, una regresión real se absorbería
sola en el primer commit que la causara. Ver `lineas-de-base/README.md`.

## Un defecto que este trabajo encontró

`ContadorDeLlamadasDelTurno` es **por turno**: en producción vive con el alcance del
request. Los runners sostenían **una** instancia del pipeline para todo el dataset, así
que ese techo —cuatro llamadas— funcionaba como techo de la **corrida entera**: el
tercer ítem ya lo había agotado, resolvía degradado, y el eje habría reportado fallo
casi total.

El modo de falla es especialmente malo porque **no da error: da un número**. Los tres
runners reciben ahora una fábrica y arman el pipeline por ítem; hay un test que lo fija.

## Qué falta

| Qué                                                | Estado                                                     |
| -------------------------------------------------- | ---------------------------------------------------------- |
| **Una implementación de proveedor de modelo real** | Bloqueado por TD-008                                       |
| Las cuatro líneas de base                          | Salen de una corrida real, así que dependen de lo anterior |

Sin proveedor real, los cuatro ejes están completos y probados pero **no se pueden
correr**. Una línea de base generada con el proveedor simulado registraría el
comportamiento del simulador, no el del asistente, y el gate empezaría a defender el
número equivocado.
