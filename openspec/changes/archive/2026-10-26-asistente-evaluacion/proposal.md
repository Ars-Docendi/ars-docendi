## Why

El carril SQL ya responde. Lo que todavía no existe es **con qué saber si responde bien**.

La métrica primaria del proyecto es **corrección con abstención**: nunca afirmar algo falso, y callarse cuando corresponde. Es una métrica, no una impresión, y hasta ahora no hay nada que la calcule. Los tests del carril verifican que cada pieza hace lo suyo; ninguno verifica que una pregunta en español se traduzca a la consulta correcta, que es justamente donde el sistema puede fallar de la forma más cara: respondiendo algo plausible y falso.

Este cambio construye la evaluación del eje de **capacidad** —traducir la pregunta a la consulta correcta— y la infraestructura común que van a usar los otros tres ejes. Hay dos trampas verificadas que el diseño evita, y las dos son la razón por la que este cambio existe en H2 y no más tarde:

**Sin crédito de API el eval no falla: miente.** El sistema devuelve una abstención con error seteado, y los ítems que esperan abstención pasan espuriamente. El reporte da un número bajo que parece una regresión del modelo cuando en realidad no se ejecutó nada. Un reporte escrito sobre una corrida inválida es peor que no tener reporte.

**El proyecto que gasta plata no puede estar en la solución.** El CI corre los tests de la solución sin filtro. Un runner adentro ejecutaría la API real decenas de veces por semana, y el síntoma de olvidarlo **es una factura, no un test rojo**: el descubrimiento llega a fin de mes.

## What Changes

- **Núcleo de evaluación** como biblioteca **dentro** de la solución, con todo lo que no cuesta dinero: el generador del fixture, el modelo del dataset, la puntuación, el sellado y la orquestación del runner. Se prueba en el CI con un proveedor de guion.
- **Runner** como proyecto de consola **fuera** del archivo de solución, más un guard **dentro** que falla si vuelve a entrar. Es lo único que instancia un proveedor real.
- **Fixture sintético determinista**, con fecha ancla fija y sin ningún dato personal real. Correr el generador dos veces produce el mismo archivo, byte a byte. Reproduce a propósito las colisiones que el detector de ambigüedad necesita: nombres de materia repetidos entre carreras y apellidos compartidos.
- **Dataset de capacidad** estratificado por dificultad técnica, con las consultas de referencia ejecutándose **en vivo** contra el fixture en cada corrida, sin conjuntos de resultados guardados.
- **Puntuación con penalización**: suma la consulta correcta sobre una pregunta factible y la abstención correcta sobre una infactible; no suma la abstención sobre una factible; y **resta** la consulta incorrecta o el intento sobre una infactible. Se reporta con varios valores de penalización.
- **Preflight obligatorio** que aborta con código distinto de cero y **no deja reporte en disco** cuando el proveedor no responde de verdad.
- **Sellado de identidad**: cada reporte estampa el hash del prefijo del prompt, el del dataset y el del fixture.
- **Verificación de disjunción** entre el catálogo de ejemplos y el dataset de capacidad — la tarea que el cambio del carril dejó abierta a propósito.

## Capabilities

### New Capabilities

- `asistente-fixture-determinista`: fixture sintético reproducible byte a byte, sin reloj y sin datos personales reales, con las colisiones del dominio garantizadas por test.
- `asistente-eje-capacidad`: dataset estratificado y puntuación con penalización, con las consultas de referencia ejecutadas en vivo y la abstención exigida sin error.
- `asistente-runner-sellado`: ejecutor común de los ejes, con preflight que aborta sin dejar reporte y reportes sellados con tres hashes.
- `asistente-exclusion-ci`: exclusión estructural del runner del archivo de solución, con un guard adentro que falla si vuelve a entrar.

### Modified Capabilities

- `asistente-ejemplos-lexicos`: la disjunción con el dataset de capacidad pasa de convención escrita a verificación mecánica.

## Impact

- `backend/src/ArsDocendi.Evaluacion.Nucleo/` — proyecto nuevo, **en** la solución.
- `backend/eval/ArsDocendi.Evaluacion/` — proyecto nuevo, **fuera** de la solución.
- `backend/eval/datasets/capacidad.json` — el dataset versionado.
- `backend/eval/README.md` — cómo correr el eval a mano.
- `backend/tests/ArsDocendi.IntegrationTests/Evaluacion/` — tests del núcleo y el guard de exclusión.
- `backend/ArsDocendi.slnx` — se agrega el núcleo; el runner **no**.
- `docs/quality/` — la métrica, cómo se lee un reporte y qué significa cada eje.
- **Grafo de dependencias**: `ArsDocendi.Evaluacion.Nucleo` depende de `Modules.Asistente` y de `ArsDocendi.Shared`. Es una dependencia de herramienta, no de producción: ningún proyecto de producción la referencia. El runner depende del núcleo y de nada más del repositorio.

## Rollback

Aditivo y aislado. El núcleo no lo consume ningún proyecto de producción, así que quitarlo de la solución no afecta al Host ni a los módulos. El runner no está en la solución, y el fixture y el dataset son archivos.
