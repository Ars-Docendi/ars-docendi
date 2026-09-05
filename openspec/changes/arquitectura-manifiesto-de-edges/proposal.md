## Why

El «Edge registry» de `docs/architecture/dependency-graph.md` es una tabla markdown que **ningún test lee**: nada en `backend/tests/` la nombra. `ArquitecturaIdentityTests` verifica reglas estructurales —capas, internals ajenos, escritura a `identity`— pero no el registro, así que la tabla puede desincronizarse de los `.csproj` sin que nada se ponga en rojo.

Y ya se desincronizó, en las tres formas posibles:

| Desviación                                                                              | Estado                                                       |
| --------------------------------------------------------------------------------------- | ------------------------------------------------------------ |
| `ArsDocendi.Evaluacion.Nucleo` → `Modules.Asistente`                                    | en el código, **sin fila**                                   |
| `ArsDocendi.Host` → `ArsDocendi.Shared`                                                 | en el código, **sin fila**                                   |
| `ArsDocendi.Host` → `Modules.Aulas.Contracts` / `Portal.Contracts` / `Tareas.Contracts` | fila en la tabla, **sin `ProjectReference` en el `.csproj`** |

La primera es la que más incomoda: un proyecto que no se llama `Modules.*` referencia el proyecto **interno** de un módulo. El motivo está escrito —en un comentario del `.csproj`, no en el registro— y es razonable, pero `ArquitecturaIdentityTests` no lo ve porque su glob es `Modules.*.csproj`. Una frontera que ningún test alcanza no es una frontera.

**El mecanismo correcto ya existe en este repo.** `database/asistente/manifiesto-privilegios.json` es la fuente de verdad de qué se concede, y un test lo compara contra los privilegios efectivos de la base en tres direcciones: privilegio efectivo no declarado, privilegio declarado inexistente, y objeto sin clasificar. La tercera dirección atrapó `__EFMigrationsHistory` en su primera corrida. Falta aplicar el mismo criterio a las fronteras de proyecto.

**Se vuelve urgente ahora** por ARS-64 (enmienda del invariante #14) y ARS-46 (aristas del carril determinista de API). La excepción del asistente es exactamente el `:friends` de Metabase, y allá es un **dato que el linter lee y que falla el build**, no un párrafo. Una excepción documentada en prosa es una excepción que en seis meses nadie sabe si sigue vigente ni si alguien la ensanchó.

El dato que conviene tener a la vista: el grafo de módulos de Metabase **no es un DAG** —tiene ciclos aceptados, y `:friends` es un bypass total—. A diez veces nuestro tamaño renunciaron al invariante #2. La escapatoria conviene diseñarla antes de necesitarla, mientras todavía se puede exigir que cada uso lleve motivo y ticket.

## What Changes

- **Un manifiesto versionado de aristas**, `backend/manifiesto-de-aristas.json`, con una fila por `ProjectReference` real: origen, destino, vía, motivo, y ticket cuando la arista es una excepción declarada a un invariante.
- **Un test que lo compara contra los `.csproj` de todos los proyectos de `backend/src`**, no solo los `Modules.*`, en las dos direcciones: arista en el código sin fila → rojo; fila sin arista en el código → rojo.
- **Una tercera dirección heredada del manifiesto de privilegios**: todo proyecto de `backend/src` tiene que estar clasificado en el manifiesto. Un proyecto nuevo que nadie referencia y que no referencia a nadie —hoy, `Modules.Asistente.Contracts`— es una fila con motivo, no un párrafo suelto.
- **El mismo test verifica que el grafo sigue siendo acíclico** (invariante #2), sobre las aristas leídas del código y no sobre las declaradas.
- **`dependency-graph.md` deja de tener dos verdades**: se le borra la tabla del Edge registry y pasa a **citar el manifiesto** como fuente única. El diagrama Mermaid queda marcado como dibujo de orientación, explícitamente no normativo.
- **Las excepciones a un invariante son una fila** con `motivo` y `ticket` obligatorios, verificados por el test igual que `Toda_denegacion_explicita_lleva_motivo_escrito` verifica las del manifiesto de privilegios.
- **`/architecture-drift-check` referencia el manifiesto**: sus detecciones de ciclos y de aristas no registradas dejan de ser un `grep` artesanal contra una tabla y pasan a apoyarse en el test.

## Capabilities

### New Capabilities

- `arquitectura-manifiesto-de-aristas`: manifiesto versionado de las aristas del grafo de proyectos del backend, con su verificación en dos direcciones contra los `ProjectReference` reales, la clasificación obligatoria de todo proyecto, el chequeo de aciclicidad y la regla de que una excepción a un invariante es una fila con motivo y ticket.

### Modified Capabilities

_(ninguna: ninguna capability vigente de `openspec/specs/` describe hoy el registro de aristas)_

## Impact

**Nuevo**

- `backend/manifiesto-de-aristas.json` — el manifiesto.
- `backend/tests/ArsDocendi.IntegrationTests/Backend/` — el modelo del manifiesto, el comparador y la clase de tests.

**Modificado**

- `docs/architecture/dependency-graph.md` — se borra la tabla del Edge registry, se cita el manifiesto, el diagrama queda no normativo y el procedimiento «Agregar un edge nuevo» pasa a describir la edición del manifiesto.
- `CLAUDE.md` — el invariante #2 nombra el manifiesto como el registro contra el que se chequea.
- `.claude/skills/architecture-drift-check/SKILL.md` — detecciones 2 y 5 apoyadas en el test.
- `docs/quality/tech-debt.md` — la deuda residual que este cambio no cierra.

**Sin impacto**

- Ningún `.csproj` cambia. El manifiesto se escribe con las aristas que hoy existen, incluidas las tres desviaciones de arriba: las dos que faltaban se registran con su motivo, y la fila de papel se borra. Este cambio **no arregla el grafo, lo hace verificable**.
- Ninguna API, ningún schema, ningún componente de frontend.

## Out of Scope

- **Las aristas de ARS-46** (carril determinista hacia los `Contracts` de los módulos consumidos). Este cambio es lo que las hace verificables cuando lleguen.
- **La enmienda del invariante #14** (ARS-64). El manifiesto trae el campo donde esa excepción va a vivir, y el test exige motivo y ticket a quien lo use; el texto del invariante lo escribe ARS-64.
- **Los `.csproj` de `backend/tests/`**. Un proyecto de tests referencia todo por definición y su grafo no dice nada sobre las fronteras del sistema.
- **Reparar ninguna arista existente.** Si el manifiesto deja a la vista una arista que el equipo no quiere, borrarla es otro cambio.
