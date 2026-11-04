## Why

El carril SQL deja escribir al modelo la consulta completa: uniones, fechas, agregaciones y literales. Con el modelo local (Qwen3-8B Q4_K_M en la RTX 3070) eso produce 7 respuestas falsas sobre 34 ítems de capacidad. Las preguntas compuestas son el caso más expuesto: combinan una población, varias condiciones y una operación, y cada pieza es un lugar donde una respuesta plausible puede ser falsa.

La investigación de `reports/MCP frente a híbrido por precisión.md` propone, para esas preguntas, un **plan estructurado compilado**: el modelo solo traduce la pregunta a un plan tipado sobre un catálogo cerrado; el código valida el plan, lo compila a SQL certificado y lo ejecuta bajo el mismo rol de solo lectura y RLS. Es la hipótesis más incierta del diseño ganador y la que puede cambiar la decisión, así que se prueba primero con un prototipo acotado antes de construir el banco de pruebas completo.

## What Changes

- Un carril nuevo, **plan compilado**, dentro de `CarrilSql`, detrás de una opción apagada por omisión. Con la opción apagada, ninguna solicitud ni respuesta cambia.
- Un **catálogo semántico** mínimo de la población «docentes con designación vigente»: condiciones por cargo, carrera, materia, categoría de dedicación, cantidad de carreras, cantidad de materias, antigüedad desde la primera designación y antigüedad declarada en el portal; medidas conteo, porcentaje y listado.
- Generación del plan con salida estructurada restringida por esquema JSON, **muestreo k-de-k** (primera muestra a temperatura 0; las demás solo si la primera es válida) y **acuerdo exacto** entre planes normalizados.
- Validación determinista antes de ejecutar: catálogo, anclaje de cada condición en el texto de la pregunta, cierre (términos fuera del catálogo desvían al carril SQL) y resolución de entidades del lado del servidor.
- Aclaración determinista para conceptos con más de una definición («antigüedad» sin calificar) y ante desacuerdo entre muestras.
- Respuesta por plantilla, con numerador, denominador e interpretación a la vista. El modelo no escribe números.
- Evaluación: suplemento opcional del fixture (más de una designación por persona, antigüedades y experiencias declaradas) y dataset `compuestas.json` con `sql_referencia` y `plan_referencia`, corrido con `--compuestas`.

Opciones nuevas (todas con default que no cambia el comportamiento):

| Opción                         | Default |
| ------------------------------ | ------- |
| `PlanCompilado`                | `false` |
| `MuestrasDelPlan`              | `3`     |
| `TemperaturaDeMuestrasDelPlan` | `0.6`   |

## Capabilities

### New Capabilities

- `asistente-plan-compilado`: traducción de preguntas sobre docentes a un plan tipado, su validación, compilación y ejecución, con acuerdo entre muestras y aclaración determinista.

### Modified Capabilities

Ninguna. El carril SQL solo gana un punto de desvío opcional antes de su generación.

## Impact

- **Módulos:** solo `Modules.Asistente` (carpeta nueva `Application/PlanCompilado/`) y el núcleo del evaluador (`ArsDocendi.Evaluacion.Nucleo`, fixture y dataset). No cambia ningún `*.Contracts`, ni el grafo de dependencias, ni el schema, ni la API HTTP.
- **Base de datos:** sin migraciones. El compilador lee las mismas tablas y columnas que el rol del asistente ya puede leer (`designaciones.designaciones`, `designaciones.cargos`, `designaciones.dedicaciones`, `identity.materias`, `identity.carreras`, `identity.personas` en columnas básicas, `portal.perfiles`, `portal.experiencias`).
- **Llamadas al modelo:** con la opción encendida, hasta `MuestrasDelPlan` llamadas por turno para las preguntas candidatas, sin redacción por modelo. Si la primera muestra declara la pregunta no expresable, el turno sigue por el carril SQL con una llamada gastada; el techo de 4 llamadas por turno se respeta.
- **Rollback:** apagar `PlanCompilado`.
- **Reglas de negocio:** las definiciones del catálogo (vigencia, antigüedad) son definiciones operativas del prototipo, no normativa institucional; quedan documentadas en el README del módulo y deben ratificarse antes de salir del prototipo.
