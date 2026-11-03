# Tareas

## 1. Designaciones entrega la ficha de un trámite

- [x] 1.1 `FichaDePedidoDto` e `IDesignacionesQueries.FichaDePedidoAsync(Guid id, CancellationToken ct)` en `Modules.Designaciones.Contracts`, sin ruta.
- [x] 1.2 Repositorio: el pedido con número, novedad, estado, período, cargo y dedicación solicitados, y la fecha del último movimiento de `pedido_historial`, sin cargar colecciones.
- [x] 1.3 Servicio: resuelve el actor, filtra con `MaquinaEstadosPedido.AlcanzaAmbito`, toma el nombre de la materia de `IConsultasIdentity` y pone la etiqueta del estado con el vocabulario del módulo.
- [x] 1.4 Test: el jefe obtiene la ficha de un trámite de su materia y no la de uno ajeno; el coordinador, la de su carrera; un rol global, cualquiera.
- [x] 1.5 Test: un identificador inexistente devuelve `null` sin romper.
- [x] 1.6 Test: para el mismo actor y el mismo trámite, la ficha existe si y sólo si `GET /api/designaciones/pedidos/{id}` responde 200.

## 2. El asistente: vínculos en el texto, principal y orígenes

- [x] 2.1 `UbicadorEnElTexto`, puro: dado el texto final y los vínculos resueltos de las celdas, devuelve `VinculoDelTexto(Desde, Largo, Tipo, Id)` por cada aparición del valor de la celda, como palabra completa.
- [x] 2.2 Test: «2026-9005» vinculado se ubica en «El pedido 2026-9005 está…» con su `desde` y su `largo` exactos; «2026-90051» no coincide; un valor sin vínculo no se ubica; dos apariciones dan dos posiciones; un texto con tildes y «ñ» antes del valor da el mismo índice que `String.prototype.indexOf`.
- [x] 2.3 Puerto `IProveedorDeFichas` y `FichaDelElemento(Tipo, Id, Datos)` en `Modules.Asistente.Application`, con la implementación vacía `SinFichas` registrada en `ModuleExtensions`.
- [x] 2.4 Regla del principal: una fila con un vínculo pide la ficha y cualquier otro caso no la pide. Va en `CierreDelTurno`, con try/catch: si falla, registra una advertencia y sigue sin principal.
- [x] 2.5 Orígenes: a partir del SQL validado y con `TokenizadorSql`, los schemas calificados que el asistente tiene habilitados, deduplicados y en orden estable. Sin consulta ejecutada, la lista va vacía.
- [x] 2.6 `CierreDelTurno` calcula `VinculosDelTexto`, `Principal` y `Origenes` después de la última reescritura del texto (`TextoDelRechazo`); `CapaConversacional` los agrega al `ResultadoDelTurno` final.
- [x] 2.7 `ResultadoDelTurno` y `RespuestaDelAsistente` incorporan `vinculosDelTexto`, `principal` y `origenes`, con su mapeo en `RespuestaDelAsistente.De`. Confirmar que la caché de idempotencia guarda el registro completo.
- [x] 2.8 Actualizar el comentario de `BuscadorDeVinculos` sobre las menciones en prosa (D1).
- [x] 2.9 Test (`VinculosDelTurnoTests`): con una fila vinculada, el turno trae su principal; con varias, no; un proveedor de fichas que lanza no tumba el turno; sin adaptador no hay principal; el texto reescrito por un rechazo no trae posiciones viejas.
- [x] 2.10 Test: una consulta sobre `designaciones.pedidos` con `JOIN identity.materias` trae `["designaciones","identity"]`; una aclaración trae `[]`.

## 3. El adaptador de fichas en el Host

- [x] 3.1 `FichasDeDesignaciones`: valida la política `designaciones.ver` contra el principal y delega en `IDesignacionesQueries.FichaDePedidoAsync`, sólo para el tipo `pedido-designacion`.
- [x] 3.2 Registro en `Program.cs` después de `AddAsistenteModule`, reemplazando `SinFichas`.
- [x] 3.3 Test: en el contenedor del Host, `IProveedorDeFichas` resuelve al adaptador; con el permiso devuelve la ficha; sin el permiso no consulta a Designaciones.
- [x] 3.4 Test del manifiesto de aristas: sigue sin arista `Modules.Asistente → Modules.Designaciones.Contracts`.

## 4. Frontend: contrato y bloque de respuesta

- [x] 4.1 `types.ts`: `VinculoDelTexto`, `PrincipalDeLaRespuesta` y `origenes` en `RespuestaDelAsistente`; defaults en `test/soporte.tsx`.
- [x] 4.2 `.adoc-asistente-respuesta` con fondo elevado, borde y radio del tema (D7), en los dos montajes.
- [x] 4.3 Extraer el cuerpo de la respuesta de `Mensaje.tsx` a `CuerpoDeLaRespuesta.tsx`, sin cambiar el comportamiento: los tests existentes de `Mensaje` pasan sin tocarse.

## 5. Frontend: chips, ficha y procedencia

- [x] 5.1 `TextoConVinculos`: segmenta el texto por `desde`/`largo`, pinta chips con `data-vinculo-del-asistente` y nombre accesible «Ver el {sustantivo} {texto}». Pinta como texto todo segmento cuyo valor no coincida o cuyo tipo no esté en `destinos.ts`.
- [x] 5.2 `FichaDelElemento` para `pedido-designacion`: eyebrow, número y materia, estado con tono, datos clave y «Abrir pedido» con `data-vinculo-del-asistente`. Un tipo desconocido no se pinta.
- [x] 5.3 Con principal de tipo conocido no hay chips y la tabla de una fila no se muestra debajo; «Ampliar tabla» y «Exportar a CSV» siguen en la barra.
- [x] 5.4 `ProcedenciaDeLosDatos`: «Datos de …» al pie del bloque con el mapa `designaciones` → Designaciones, `portal` → Portal, `identity` → Institucional, omitiendo códigos desconocidos.
- [x] 5.5 La celda con vínculo de `TablaDeResultado` lleva `data-vinculo-del-asistente`.
- [x] 5.6 Tests (Vitest + Testing Library):
  - el chip navega y cierra el modal;
  - un segmento desalineado se pinta como texto;
  - con ficha no hay chips ni tabla, y «Ampliar tabla» la muestra;
  - un principal de tipo desconocido cae a la presentación de hoy;
  - el pie nombra el área y nunca muestra cantidades.

## 6. Frontend: «Repetir consulta» y «Volver a la conversación»

- [x] 6.1 «Repetir consulta» en `HerramientasDePregunta`: sólo en el último turno con `respuesta`, sin turno en vuelo ni bloqueo, y llama a `reenviarUltima(turno.pregunta)`.
- [x] 6.2 Tests: repetir reenvía la misma pregunta con sus menciones y reemplaza el turno; no aparece en turnos anteriores, con cupo agotado ni en turnos restaurados.
- [x] 6.3 `LanzadorAsistente`: `onClickCapture` que marca la navegación por vínculo, y estado que muestra `VolverALaConversacion` tras ese cambio de ruta (D6).
- [x] 6.4 `VolverALaConversacion`: botón fijo con nombre accesible, ≥ 44 px, que reabre el asistente en el mismo hilo.
- [x] 6.5 Tests (`LanzadorAsistente.test.tsx`): tras seguir un vínculo aparece el botón y reabre con la conversación completa; cerrar con «×» no lo muestra; navegar de nuevo lo quita.

## 7. Documentación

- [x] 7.1 `docs/architecture/api-contracts.md` §Asistente: filas de `vinculosDelTexto`, `principal` y `origenes` en la respuesta de `POST /consultas`, y nota sobre `/consultas/flujo`.
- [x] 7.2 `docs/architecture/domains/asistente.md`: el puerto de fichas, el ubicador en el texto y los orígenes, con el adaptador en el Host.
- [x] 7.3 `docs/product/designs/asistente-conversacional-design-spec.md`: sección del rediseño con el bloque de respuesta, chips, ficha, procedencia, «Repetir consulta» y «Volver a la conversación», con enlace al prototipo.
- [x] 7.4 `Modules.Designaciones` y su `README`/domain: el método nuevo del contract.

## 8. Verificación

- [x] 8.1 `dotnet test backend/ArsDocendi.slnx` (al menos Asistente, Designaciones y Backend del Host).
- [x] 8.2 `pnpm --filter frontend test:run`, `pnpm --filter frontend lint` y `pnpm --filter frontend build`.
- [x] 8.3 `pnpm format:check` y `pnpm exec openspec validate asistente-respuestas-con-vinculos --strict`.
- [x] 8.4 Recorrido en la app local con el seed sintético, con un actor que puede abrir el pedido y otro que no: ficha, chip, repetir y volver.
