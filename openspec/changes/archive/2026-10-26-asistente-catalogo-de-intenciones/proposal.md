## Why

El asistente hoy tiene un solo carril con datos: genera SQL. Funciona, y cuesta dos llamadas al modelo por turno. Para las preguntas que la API del sistema **ya sabe responder** —«¿en qué estado está el pedido de Pérez?»— eso es pagar un modelo para reconstruir una consulta que ya está escrita, probada y con sus reglas de negocio adentro.

El carril determinista de la épica E6 enruta esas preguntas a la API en lugar de generarlas. Este cambio construye su **primera mitad**: el catálogo de intenciones y la resolución de slots. El enrutador que lo consume y decide el carril queda explícitamente afuera, igual que las referencias a los `Contracts` de los módulos consumidos.

**Por qué el corte va acá.** El catálogo y el resolutor son una pieza pura, verificable en memoria contra la base, y sin ninguna dependencia nueva del módulo. Los edges hacia `Modules.Designaciones.Contracts` —lo único que necesita acuerdo del equipo por el checklist de cinco pasos— viven del otro lado del corte. Construir esta mitad no consume esa aprobación ni la anticipa.

**Clasificar la intención con el modelo está descartado con evidencia**: 60% de F1 en triage de cinco clases, 77,4% en nueve vías. Un clasificador que falla una de cada cuatro veces, cuesta una llamada y corta el flujo es peor que una tabla.

**Y conviene decir lo que esta pieza no es.** La guarda que hace viable el enrutador social —interceptar solo si no queda ningún token de contenido— no sirve acá: distinguir «¿cuál es el estado del pedido de Pérez?» de una pregunta arbitraria exige intención **y** slots. Es viable sobre un catálogo chico de preguntas frecuentes y **no generaliza**. El catálogo nace con cinco intenciones y crece de a una, cada una con su caso de prueba.

## What Changes

- **Catálogo cerrado de intenciones**, declarativo y embebido, hermano de `ejemplos-sql.json`. Cada intención declara los términos que la identifican, los slots que exige y el destino lógico al que enrutaría. El catálogo **no ejecuta nada**: nombra un destino, no lo llama.
- **Lector de catálogos cerrados del dominio**, que lee de la base los valores admitidos de `estado`, `novedad` y `tipo_baja` —desde los `CHECK` que los declaran— y los cargos desde su tabla. Nada de esto se escribe a mano: un estado nuevo aparece solo.
- **Resolutor de slots** sobre las dos fuentes: el índice de entidades que ya usan el detector de ambigüedad y el de cambio de tema —materias y personas— más los catálogos cerrados recién leídos.
- **Un slot que resuelve a más de un valor no se resuelve.** Queda sin resolver, y una intención con un slot sin resolver no es una intención reconocida. Es la misma regla que ya gobierna al detector de ambigüedad, y por el mismo motivo.

## Capabilities

### New Capabilities

- `asistente-catalogo-intenciones`: catálogo cerrado y declarativo de intenciones, cada una con sus términos, sus slots exigidos y su destino lógico, reconocido sobre texto normalizado y a cero llamadas al modelo.
- `asistente-resolucion-de-slots`: resolución de los slots de una intención contra la base —índice de entidades y catálogos cerrados del dominio—, con la regla de que la colisión no resuelve.

## Out of Scope

- **El enrutador de dominio** que consume el catálogo y decide el carril (ARS-45). Acá el catálogo se limita a reconocer y resolver; nadie lo llama todavía.
- **Los edges hacia los `Contracts`** de los módulos consumidos (ARS-46), que requieren acuerdo del equipo.
- **Cualquier llamada real a la API del sistema.** El destino de una intención es un nombre, no una invocación.
