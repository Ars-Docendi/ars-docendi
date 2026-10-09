## Context

`CarrilSql` traduce cada pregunta a SQL libre con una llamada al modelo y la redacta con otra. La seguridad está en el motor (rol de solo lectura, GRANT por columna, RLS por ámbito), pero la corrección depende de que el modelo escriba bien uniones, fechas, agregaciones y literales. El informe `reports/MCP frente a híbrido por precisión.md` recomienda, para las preguntas compuestas, que el modelo solo decida **qué** se pregunta y el código decida **cómo** se calcula. Este change construye un prototipo de esa idea para medirla antes de comprometer el diseño completo.

Restricciones del código existente que ordenan el diseño:

- `IEjecutorDeConsulta.EjecutarAsync` solo liga parámetros UUID (`$refN`). No hay forma de ligar texto, números ni fechas.
- `ContadorDeLlamadasDelTurno` corta en 4 llamadas por turno (`MaximoDeLlamadasPorTurno`).
- `RunnerDeCapacidad` llama a `CarrilSql.ResponderAsync` directamente; un carril que no viva ahí no se mide.
- El fixture de evaluación tiene exactamente una designación por persona, así que ninguna pregunta compuesta sobre «varias carreras» o «antigüedad» discrimina nada.

## Goals / Non-Goals

**Goals:**

- Responder preguntas sobre docentes con designación vigente, simples o compuestas, sin que el modelo escriba SQL ni números.
- Abstenerse o aclarar —nunca responder— cuando las muestras no coinciden, cuando un concepto tiene dos definiciones o cuando una entidad no se resuelve.
- Medir el prototipo con el evaluador existente, comparando ítem por ítem contra el carril SQL con el mismo modelo.

**Non-Goals:**

- Otras poblaciones (pedidos, períodos, portal como población), `agrupar_por`, promedios o sumas de horas.
- Exponer el plan por MCP o consultar a la API de Claude como segunda opinión: son brazos posteriores del experimento.
- Editar el plan en un seguimiento («¿y en Industrial?»): el prototipo no deja consulta reutilizable en el hilo.
- Cambiar líneas de base, cassettes o el comportamiento con la opción apagada.

## Decisions

### D1 — El carril vive dentro de `CarrilSql`, antes de la generación

`CarrilSql` recibe el carril del plan como dependencia opcional y lo consulta antes de `GeneradorDeSql` cuando `PlanCompilado` está encendida. Si el plan responde, aclara o se abstiene, el turno termina ahí; si la pregunta no es expresable, sigue por el carril SQL de siempre. Así el evaluador lo mide sin cambios en su runner, y el resto del sistema (capa conversacional, historial, auditoría) lo ve como un turno del carril SQL.

**Alternativa descartada:** un enrutamiento en `CapaConversacional`. Obligaba a cambiar `RunnerDeCapacidad` y no aporta nada al prototipo.

### D2 — El catálogo semántico es código, no configuración

Cada condición del catálogo es un fragmento de SQL certificado con su test. Ponerlas en un JSON separaría la definición de su compilación y permitiría cambiar una sin la otra. El catálogo vive en `CatalogoDelPlan.cs` y de él se derivan el esquema JSON de salida, el texto del prompt y las etiquetas de la interpretación, de modo que no pueden divergir.

### D3 — El plan es pequeño, tipado y con valores como texto

```json
{
  "expresable": true,
  "medida": "porcentaje",
  "filtros": [{ "campo": "cargo", "operador": "=", "valor": "titular" }],
  "condiciones": [
    { "campo": "antiguedad_designacion", "operador": ">", "valor": "20" },
    { "campo": "cantidad_carreras", "operador": ">=", "valor": "2" }
  ]
}
```

`filtros` define la población (el denominador de un porcentaje); `condiciones` solo existe para `porcentaje` y define el numerador. Una muestra de conteo o de listado que igual reparte sus condiciones entre las dos listas no se rechaza: en esas medidas son la misma conjunción y el validador las pliega en `filtros`. Un porcentaje sin `condiciones` sí es inválido. El valor viaja como texto y el validador lo interpreta según el tipo del campo, para que la gramática quede chica y sirva igual en llama-server (GBNF), vLLM (xgrammar) y cualquier proveedor que no la respete.

### D4 — Una puerta determinista antes de llamar al modelo

Antes de gastar una llamada, la pregunta pasa por una puerta léxica:

- Si menciona vocabulario de dominio **fuera del catálogo** (pedidos, períodos, horas, habilidades, certificaciones, bajas, licencias…), el plan no corre y el turno sigue por el carril SQL con cero llamadas gastadas.
- Si no menciona a la población (docentes, profesores, titulares, cargos…), tampoco corre.
- Si lo que pide no son personas («¿qué asignaturas se dictan en Ingeniería Industrial?», «¿en qué carreras dictan los titulares?»), tampoco corre, aunque nombre a la población: las tres medidas cuentan o listan docentes. Se decide por la palabra que sigue a cada interrogativo (materia, carrera, categoría, cargo o designación).
- Si menciona «antigüedad» sin calificarla (desde la designación o declarada en el portal), el turno **aclara** sin llamar al modelo, con una opción por definición.

### D5 — Muestreo k-de-k con acuerdo exacto

1. Primera muestra a temperatura 0.
2. Si declara `expresable: false`, el turno sigue por el carril SQL (una llamada gastada).
3. Si la primera muestra es inválida (fuera del catálogo, sin anclaje o con una entidad que no resuelve), el turno se abstiene: la pregunta parecía expresable y el modelo no la expresó bien, y desviarla a SQL libre sería apostar al carril menos preciso.
4. Si es válida, se piden `MuestrasDelPlan − 1` muestras más a `TemperaturaDeMuestrasDelPlan`.
5. Se responde solo si **todas** las muestras son válidas y su forma canónica es idéntica. La forma canónica ordena las condiciones, normaliza valores y unifica sinónimos de cargo.
6. Si hay desacuerdo, el turno pide aclaración y ofrece cada interpretación válida distinta como opción. Con una sola interpretación válida, la ofrece como confirmación.

Con `MuestrasDelPlan = 3`, el peor caso son 3 llamadas sin redacción, dentro del techo de 4 aunque la capa conversacional haya reescrito la pregunta.

**Riesgo conocido:** el proveedor Anthropic ignora la temperatura y la clave de cassette no la incluye, así que en una corrida grabada las muestras coinciden por construcción. El prototipo se mide con el modelo local, que no graba cassettes.

### D6 — Anclaje y cierre deterministas

Cada condición del plan tiene que estar anclada en el texto normalizado de la pregunta: su término (un cargo, «carrera», «materia», «categoría», «antigüedad»), su valor (el nombre de la entidad o el número, en cifras o en palabras) y, para los operadores de comparación, una señal compatible («más de», «al menos», «menos de», «como máximo»). Un número sin señal admite `=` o `>=`. En sentido inverso, todo término del catálogo presente en la pregunta tiene que aparecer en el plan. Una condición sin ancla o un término sin consumir invalida la muestra.

### D7 — Las entidades se resuelven en el servidor y viajan como marcadores

- **Materias:** con `IBuscadorDeMenciones.BuscarAsync(actor, Materia, …)`, que ya respeta `identity.asistente_materias_visibles()`, y coincidencia exacta del nombre normalizado. Un nombre compartido por varias carreras («Análisis Matemático») es ambiguo —la misma política que el detector de ambigüedad de la capa conversacional— salvo que la misma lista del plan nombre la carrera; la aclaración ofrece una opción por carrera.
- **Carreras:** con una lectura de `identity.carreras` a través del ejecutor y coincidencia exacta normalizada, o por contención única.

Los ids resueltos se ligan como `$refN`, lo único que el ejecutor sabe ligar. El resto de los literales sale de listas cerradas (códigos de cargo) o son enteros validados, y se escriben en el SQL por el compilador, nunca por el modelo. Una entidad que no resuelve produce una abstención explícita («no encontré la carrera X»); una ambigua, una aclaración.

### D8 — Compilación a SQL certificado

El compilador arma una CTE `vigentes` (designaciones con `vigente_hasta IS NULL`, con su materia, carrera, cargo y categoría) y una población `docentes` (personas distintas de `vigentes`). Las condiciones afirmativas de cargo, carrera, materia y categoría de una misma lista se evalúan juntas, en un solo `EXISTS`, sobre **una misma designación**: «titulares de Ingeniería Industrial» son quienes son titulares EN Industrial, no quienes son titulares en otra carrera y adjuntos en Industrial. Las negadas van cada una en su `NOT EXISTS`, y las de la persona (cantidades y antigüedades) cada una con su subconsulta. La antigüedad se calcula con `age()` contra la fecha de referencia del turno (`IFechaDeReferencia`), nunca contra el reloj. Las medidas son:

| Medida       | Columnas             |
| ------------ | -------------------- |
| `conteo`     | `total`              |
| `porcentaje` | `cumplen`, `total`   |
| `listado`    | `apellido`, `nombre` |

El SQL compilado pasa igual por `ValidadorDeSql` como defensa en profundidad, y se ejecuta con el rol **básico** aunque el actor vea datos personales: el plan nunca necesita columnas sensibles.

### D9 — Respuesta por plantilla, sin redacción por modelo

La respuesta se arma con el resultado y el plan: número, porcentaje con numerador y denominador, o lista de apellidos y nombres. Lleva siempre la interpretación del plan, y la frase «dentro de lo que podés ver» cuando el actor no alcanza todo. `SqlEjecutado` queda nulo, así que un seguimiento no edita la consulta compilada (non-goal).

### D10 — Evaluación sin tocar las líneas de base

- `GeneradorDeFixture` gana un parámetro opcional `conSuplementoCompuesto`, apagado por omisión, así que la huella y el SQL del fixture de siempre no cambian.
- El suplemento agrega designaciones vigentes en otras materias y carreras, designaciones históricas antiguas y experiencias declaradas.
- `backend/eval/datasets/compuestas.json` usa el formato de `capacidad.json` y suma `plan_referencia` por ítem.
- `--compuestas` corre solo ese eje y escribe `reportes/compuestas.md` sin gate.
- Un test sin modelo compila cada `plan_referencia`, lo ejecuta contra el fixture con suplemento y exige el mismo resultado que su `sql_referencia`.

## Risks / Trade-offs

- **Cobertura chica por diseño.** Todo lo que el catálogo no expresa sigue por SQL libre. → Es lo que se quiere medir: precisión dentro de la cobertura y costo en cobertura.
- **Anclaje léxico frágil ante paráfrasis.** → Rechaza (no responde) cuando no ancla, así que el costo es cobertura, no precisión. El dataset incluye paráfrasis.
- **Definiciones operativas no ratificadas** («vigente», las dos antigüedades). → Documentadas en el README y en la interpretación de cada respuesta; deben ratificarse antes de salir del prototipo.
- **El techo de 6 resultados del buscador de menciones** puede dejar afuera una materia en catálogos grandes. → Si no hay coincidencia exacta entre los resultados, la entidad no resuelve y el turno se abstiene.

## Open Questions

- ¿Un número sin señal («dicta en dos carreras») significa exactamente dos o dos o más? El prototipo admite las dos lecturas y deja que decida el acuerdo entre muestras; el equipo debe fijarlo.
- ¿Qué definición de antigüedad es la institucional?
