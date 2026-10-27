## Why

El `EnrutadorDeDominio` corre en **modo sombra**: decide y no ejecuta, a propósito, hasta que existan los edges hacia los `Contracts` (ARS-46) y los adaptadores de respuesta. El comentario del paso 5 de `CapaConversacional.ResolverAsync` dice por qué está cableado igual: «el pedido de aprobación de los edges se fundamenta con un número —qué proporción del tráfico real captura un catálogo de cinco intenciones, y cuántas veces se equivoca— y ese número no existe si la decisión no se toma nunca».

**La decisión se toma y no llega a ningún registro.** `TurnoParaRegistrar` no tiene campo para ella, `asistente.registro_operativo` no tiene columna, y `CarrilDe` deriva el carril del estado del turno, no de la ruta que el enrutador eligió. La única traza es un `LogInformation`. Hoy, producir el número que ARS-46 necesita exige minar logs — que rotan, que no se agregan y que nadie va a consultar para fundamentar una decisión de arquitectura.

El cambio anterior [`asistente-enrutador-de-dominio`](../asistente-enrutador-de-dominio/proposal.md) declaró en su spec que «la decisión se observa sin cambiar la respuesta» y que «queda registrado qué intención la habría capturado». Ese requisito quedó cumplido a medias: se observa en el log, no en el registro. Este cambio lo cierra.

**Y trae la mitad que no depende de esperar tráfico.** El resolutor es determinista y cuesta cero llamadas al modelo, así que se lo puede correr hoy sobre los datasets de evaluación —`capacidad.json` y `robustez.json`— sin esperar un solo turno real ni gastar un centavo. Esa corrida, fijada como tabla dorada, es la que produce el número.

## What Changes

- **La decisión viaja al registro**: `TurnoParaRegistrar` lleva el nombre de la intención que el enrutador sombra eligió, o nulo. El registro **operativo** la persiste en `intencion_sombra text NULL`.
- **Decisión de privacidad, tomada explícitamente**: la intención **NO** va al registro analítico. §3.4 de la definición desvincula los dos registros a propósito y TD-012 declara un canal residual por orden físico de filas; una dimensión más de agrupación achica el conjunto anónimo sin comprar nada que el operativo no dé ya. El motivo completo está en el diseño.
- **`carril` no cambia de significado**: sigue siendo la ruta **real** del turno, no la que se habría tomado. Son dos columnas porque son dos hechos distintos, y colapsarlos volvería incomparables las series de antes y después de conectar el carril.
- **Tabla dorada offline**: un test corre el enrutador sobre las preguntas de `capacidad.json` y `robustez.json` y fija el mapeo pregunta → intención capturada. Si el catálogo o los datasets cambian lo que capturan, falla en rojo nombrando qué se movió.
- **Consistencia de fraseo como medida de error**: cada ítem de `robustez.json` declara `origen`, apuntando al ítem de capacidad del que es paráfrasis. Dos fraseos de la misma pregunta que producen decisiones distintas son un error del enrutador, medible offline y sin tráfico real.
- **El README del módulo documenta la consulta** que produce el número de cobertura sobre tráfico real.

## Capabilities

### New Capabilities

- `asistente-medicion-del-enrutador-sombra`: la decisión del enrutador sombra se persiste en el registro operativo y solo ahí, `carril` conserva su significado, y una tabla dorada offline sobre los datasets de evaluación produce la cobertura y la consistencia de fraseo sin tráfico real ni llamadas al modelo.

### Modified Capabilities

<!-- Ninguna. Las capabilities del asistente todavía no están consolidadas en openspec/specs/. -->

## Impact

- **Módulo**: solo `Modules.Asistente`. Sin edges nuevos, sin cambios en el grafo de dependencias, sin `.Contracts` ajenos.
- **Contrato HTTP**: sin cambios. La intención sombra es telemetría; no viaja en `ResultadoDelTurno` ni en la respuesta de la API.
- **Esquema**: una columna anulable en `asistente.registro_operativo`. Nada que leer se rompe: toda consulta existente sigue devolviendo lo mismo.
- **Docs en el mismo commit** (invariante #6): `docs/architecture/data-model.md` y el README del módulo.
- **Rollback**: la columna es anulable y nadie la lee para responder. Revertir el cambio deja filas con un valor que nada consulta; no hay migración inversa que correr.
