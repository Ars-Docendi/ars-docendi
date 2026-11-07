# Diseño — respuestas que llevan al sistema

Base visual: el prototipo [Asistente: rediseño](https://claude.ai/artifact/Srtyn2Mrc6TauhYLaMT1Jn), página «Tipos de respuesta», tarjetas 01–20. Este diseño cubre los tipos que dependen de destinos que ya existen; el resto del prototipo queda fuera (ver la propuesta).

## Decisiones

### D1 — Las posiciones del texto salen de los vínculos ya resueltos, no de un segundo resolutor

`CierreDelTurno` ya resuelve los vínculos de las celdas contra el módulo dueño. Con el texto final en mano, busca en él cada **valor de celda que tiene vínculo** y devuelve sus apariciones. No se consulta a nadie más y no se agrega una regex por tipo: si una celda no se resolvió, su valor no se enlaza en la prosa.

- **La coincidencia es exacta, ordinal y por palabra completa.** Antes y después del valor no puede haber letra, dígito ni guion, para que «2026-90051» no enlace «2026-9005».
- **Se calcula sobre el texto que ve el usuario.** El paso va después de cualquier reescritura (`TextoDelRechazo`) y sobre la `Respuesta` final del `ResultadoDelTurno`.
- **Las posiciones son índices de `string` de .NET.** Son unidades de código UTF-16, las mismas que usa `String.prototype.slice` en el cliente, así que no hace falta convertir nada.
- **Cambia la regla anterior.** El comentario de `BuscadorDeVinculos` que dice que una mención en prosa no produce vínculo pasa a decir: una mención en prosa se enlaza **sólo** si el mismo valor ya tiene vínculo en una celda.

Campo nuevo de la respuesta: `vinculosDelTexto: [{ desde, largo, tipo, id }]`. No se llama `referencias` porque ese nombre ya lo usan las menciones de la pregunta (`ConsultaDelAsistente.referencias`, `turno_historico.referencias`).

### D2 — El elemento principal se decide con una regla, no con el modelo

Hay elemento principal cuando el resultado tiene **una fila** y esa fila tiene **un vínculo**. Es una regla determinista, que se puede testear sin modelo y que no depende de lo que el modelo haya escrito.

La ficha la pide un **puerto nuevo** del asistente, `IProveedorDeFichas`, con su implementación vacía `SinFichas` registrada por el módulo. El Host lo compone con `FichasDeDesignaciones`, de la misma forma que `VinculosDeDesignaciones`:

1. Exige la política `designaciones.ver` sobre el principal.
2. Delega en `IDesignacionesQueries.FichaDePedidoAsync(Guid id, ct)`.
3. Designaciones aplica `AlcanzaAmbito`, el mismo criterio de su detalle, y devuelve `FichaDePedidoDto` o `null`.

El asistente no puede referenciar el DTO de Designaciones (ARS-46). Por eso el puerto devuelve `FichaDelElemento(Tipo, Id, Datos)`, donde `Datos` es un diccionario de **claves documentadas por tipo**:

- Para `pedido-designacion`: `numero`, `novedad`, `materia`, `estado`, `estadoEtiqueta`, `periodo`, `cargo?`, `dedicacion?` y `ultimaActividad`.
- La etiqueta del estado la pone Designaciones, que es la dueña del vocabulario.
- El tono visual (aprobado, en curso, rechazado) lo decide el cliente a partir de `estado`, con un neutro para cualquier código que no conozca.

Igual que con los vínculos, un fallo del proveedor se registra como advertencia y el turno responde sin principal. Los datos de la ficha no pasan por el modelo.

Campo nuevo de la respuesta: `principal: { tipo, id, datos } | null`.

### D3 — Ficha o chips, no las dos; y con ficha no se repite la tabla

- **Con principal, el texto no lleva chips.** El enlace lo lleva el botón de la ficha. Es la regla del prototipo: con un elemento principal, el texto es una línea y la tarjeta lleva el enlace.
- **Con principal, la tabla de una fila no se muestra debajo.** Sigue disponible en «Ampliar tabla», el botón que ya existe en la barra de acciones; la exportación a CSV también sigue.
- **Si el cliente no conoce el tipo del principal**, ignora la ficha y muestra la respuesta como hoy, con tabla y chips. Es el mismo criterio que con los vínculos de tipo desconocido.

### D4 — Las áreas de datos salen del SQL validado, en códigos de schema

Después de validar la consulta, `TokenizadorSql` recorre sus identificadores calificados y se queda con los schemas habilitados para el asistente (`designaciones`, `identity`, `portal`).

- **El campo nuevo es `origenes: string[]`**, con esos códigos, deduplicados y en orden estable.
- **El cliente los traduce** a «Designaciones», «Portal» e «Institucional» (para `identity`), y omite los que no conoce.
- **Sin consulta ejecutada no hay orígenes**: aclaración, rechazo o error devuelven la lista vacía.

No se informa la cantidad de filas: en una respuesta truncada podría leerse como el total, que es justo el dato que el contrato no revela.

### D5 — «Repetir consulta» es «Editar y reenviar» sin editar

`useAsistente.reenviarUltima(texto)` ya hace todo lo necesario:

- reemplaza el último turno en el estado;
- ubica las menciones;
- manda `reemplaza` al backend, que reemplaza el turno en el hilo y en el historial;
- cuenta como turno nuevo para el cupo y para el lock.

«Repetir consulta» llama a esa función con la pregunta tal cual. No se agrega endpoint ni semántica nueva.

El botón va en `HerramientasDePregunta`, junto a «Editar y reenviar», con la misma condición (`esUltimo && !enVuelo && !bloqueado`), y además exige que el turno tenga `respuesta`. Un turno con error ya tiene «Reintentar», y un turno restaurado del historial tiene «Volver a consultar».

### D6 — El lanzador sabe que la navegación vino de un vínculo

El lanzador ya cierra el modal cuando cambia la ruta (ajuste en render, `LanzadorAsistente.tsx:59–73`). Le falta saber **por qué** cambió la ruta:

- **Marcar el origen.** Un `onClickCapture` en el contenedor del modal registra en una ref que se siguió un vínculo de respuesta cuando el clic cae dentro de un `a[data-vinculo-del-asistente]`. Ese atributo lo llevan el chip, la celda con vínculo y el botón de la ficha. No hace falta pasar props por todo el árbol.
- **Mostrar el botón.** Si la ruta cambia con esa marca, el lanzador cierra el modal, limpia la marca y muestra `VolverALaConversacion`.
- **Ocultar el botón.** Se oculta en el próximo cambio de ruta o al reabrir el asistente.
- **Cerrar sin navegar no lo muestra.** Ni la «×» ni Escape ni el botón «Atrás» del navegador fijan la marca.

`VolverALaConversacion` es un botón fijo abajo a la derecha, por debajo del modal en el apilado, con nombre accesible y tamaño táctil ≥ 44 px.

### D7 — El bloque de respuesta es una regla de CSS sobre el contenedor que ya existe

`.adoc-asistente-respuesta` pasa a tener:

- `background: var(--color-bg-raised)`;
- `border: 1px solid var(--color-border-default)`;
- `border-radius: var(--radius-sm)`;
- `padding: var(--space-4)`.

Así se cumplen las dos guardas de estilo: sólo tokens del tema (`asistente.tokens.test.ts`) y ninguna clase huérfana (`asistente.clases.test.ts`). «Consultando…» y los avisos de estado ya se pintan dentro de ese contenedor.

### D8 — `Mensaje.tsx` no crece

`Mensaje.tsx` ya tiene 426 líneas, por encima del tope blando de ~300. Las piezas nuevas van en componentes propios:

- `TextoConVinculos`: arma los segmentos a partir de `desde`/`largo` y respeta `white-space: pre-wrap`, porque los chips son `<a>` en línea.
- `FichaDelElemento`: incluye la ficha del pedido y el mapa de tono por estado.
- `ProcedenciaDeLosDatos`.
- `VolverALaConversacion`.

El cuerpo de la respuesta se extrae a `CuerpoDeLaRespuesta.tsx` para dejar `Mensaje` cerca del tope.

## Alternativas descartadas

- **Que el modelo marque los enlaces en el texto** (por ejemplo, `[[pedido:…]]`). El modelo inventaría marcas, y con columnas sensibles no ve los valores reales. Además, toda la línea del vínculo descansa en que el modelo no decide qué se abre.
- **Pasar el texto por el resolutor con una regex de número de trámite.** Enlazaría números que el resultado no devolvió, y obligaría a mantener una regex por tipo en el asistente.
- **Que la ficha traiga etiquetas y orden de presentación.** El backend dice qué es y el cliente decide cómo se ve; las etiquetas de campo son de la interfaz. La excepción es la etiqueta del estado, que es vocabulario del dominio.
- **Persistir texto, vínculos y ficha en el historial.** Necesita una migración y reabre la discusión de qué datos guarda `turno_historico`. Un turno restaurado sigue sin chips ni ficha, como hoy.
- **«Repetir» como turno nuevo agregado al final.** Duplicaría la pregunta en el hilo y requeriría otra semántica en el backend; reemplazar es lo que ya hace «Editar y reenviar».

## Riesgos

- **Que las posiciones se desalineen del texto.** Si el cliente transforma el texto (lo recorta, normaliza espacios), las posiciones dejan de coincidir. Mitigación: el cliente pinta el texto tal cual llega, y un test cubre que cada segmento señalado coincide con el valor del vínculo. Si no coincide, ese segmento se pinta como texto.
- **Redacción en flujo.** Los borradores parciales llegan sin posiciones, así que los chips aparecen recién con el evento final `resultado`. Es el mismo momento en que hoy aparecen tabla y vínculos.
- **Ficha y detalle desalineados.** Si el criterio de la ficha y el del detalle divergen, aparece un botón que termina en 403. Mitigación: la ficha reusa `AlcanzaAmbito`, y un test compara los dos para el mismo actor.
