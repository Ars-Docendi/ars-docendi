## Why

El asistente ya sabe qué trámite abre cada fila (`asistente-vinculo-al-tramite`), pero lo ofrece en un solo lugar: la celda de la tabla. Cuando la respuesta trata de **un** pedido —«¿en qué estado está el 2026-9005?»—, el usuario lee el estado en un párrafo, baja a una tabla de una fila y recién ahí encuentra el enlace. El texto nombra el trámite y no lleva a él.

La respuesta tampoco se distingue del resto del hilo: es texto suelto sobre el fondo del panel, sin borde, y en una conversación larga cuesta ver dónde empieza y dónde termina cada una. El prototipo de rediseño ([Asistente: rediseño](https://claude.ai/artifact/Srtyn2Mrc6TauhYLaMT1Jn)) lo resolvió con un bloque propio, y ese prototipo es la base acordada de este cambio.

Faltan además dos atajos que el prototipo validó:

- **Repetir la última consulta sin reescribirla.** Hoy el único camino es «Editar y reenviar» sin editar nada.
- **Volver a la conversación después de seguir un vínculo.** El modal se cierra al navegar y la conversación sobrevive, pero nada en la pantalla de destino dice cómo volver a ella.

## What Changes

- **Cada respuesta va en un bloque propio**: fondo elevado, borde y radio del tema, separado de la burbuja de la pregunta.
- **El texto enlaza el trámite que nombra**, como un chip. Sólo se enlaza lo que el resultado ya vinculó: el backend ubica en el texto final los valores de las celdas que resolvió y devuelve sus posiciones. El modelo no escribe enlaces, y un número que aparece sólo en la prosa no se enlaza.
- **Una respuesta sobre un único trámite lo muestra en una ficha** con sus datos clave y el botón «Abrir pedido». La ficha la arma Designaciones con el mismo criterio de autorización que su detalle. Esos datos no pasan por el modelo. Con ficha, la tabla de una fila no se repite: queda disponible en «Ampliar tabla».
- **La respuesta dice de qué área salen sus datos** («Datos de Designaciones»), sin cantidad de filas, sin SQL y sin hablar de cómo se consultó.
- **«Repetir consulta»** en el último turno respondido. Reenvía la misma pregunta por el camino de «Editar y reenviar»: reemplaza el último turno y cuenta para el cupo.
- **«Volver a la conversación»**: después de seguir un vínculo desde el asistente, la pantalla de destino muestra un botón flotante que reabre el asistente en el mismo hilo.

Fuera de alcance (están en el prototipo, pero quedan para cambios posteriores):

- **Páginas de detalle nuevas.** El perfil docente, la materia y la carrera no tienen detalle hoy; sin destino no hay enlace.
- **Solicitudes de aula.** Dependen de `feature/reserva-aulas`, que no está en `develop`, y de permisos del asistente sobre el schema `aulas`.
- **Acciones desde la respuesta**, como «Asignar aula». Se descartaron en la revisión del prototipo.

## Capabilities

### New Capabilities

_(ninguna; el cambio amplía capacidades que ya tienen delta en changes sin archivar)_

### Modified Capabilities

- `asistente-vinculos-del-resultado`: los vínculos dejan de vivir sólo en las celdas. El texto señala los elementos ya vinculados, y una respuesta sobre un único elemento lo presenta en una ficha.
- `asistente-superficie-frontend`: bloque de respuesta, chips en el texto, ficha, procedencia de los datos, «Repetir consulta» y «Volver a la conversación».
- `asistente-contrato-de-respuesta`: la respuesta nombra las áreas de datos que leyó.
- `aprobacion-pedidos-designacion`: el módulo entrega la ficha de un trámite acotada a lo que el actor puede abrir.

## Impact

- `Modules.Designaciones.Contracts`: nuevo método en `IDesignacionesQueries` y DTO de la ficha. Es un cambio aditivo; el consumidor es el adaptador del Host.
- `Modules.Designaciones`: consulta de la ficha en el repositorio y el servicio. Reusa `AlcanzaAmbito`.
- `Modules.Asistente`:
  - Puerto de fichas, con su implementación vacía.
  - Ubicación de los vínculos en el texto, en `CierreDelTurno`.
  - Orígenes de los datos, a partir de los schemas citados en el SQL validado.
  - Campos nuevos en `ResultadoDelTurno` y `RespuestaDelAsistente`.
- `ArsDocendi.Host`: adaptador de fichas, compuesto igual que `VinculosDeDesignaciones`.
- `frontend/src/features/asistente`:
  - Tipos.
  - El bloque de respuesta y los componentes nuevos: texto con chips, ficha, procedencia, «Repetir consulta» y el botón de volver.
  - `destinos.ts` y `asistente.css`.
- Documentación en el mismo diff: `docs/architecture/api-contracts.md` (campos nuevos de la respuesta), `docs/architecture/domains/asistente.md` y `docs/product/designs/asistente-conversacional-design-spec.md` (regla 9).
- **Grafo de dependencias sin aristas nuevas.** Asistente sigue sin referenciar `Modules.Designaciones.Contracts`; la composición vive en el Host. Lo verifica el test del manifiesto, que ya corre.
- **Sin migraciones.** El historial no persiste texto ni vínculos, y este cambio tampoco: un turno restaurado sigue sin chips ni ficha.
- **Rollback**: todo es aditivo. Revertir el frontend vuelve a la presentación actual. Sin el adaptador de fichas, el puerto vacío devuelve «sin ficha» y la respuesta se muestra como hoy.
