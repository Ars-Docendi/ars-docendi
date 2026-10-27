# Tareas

## 1. La autoridad: Designaciones ubica un trámite

- [x] 1.1 Sobrecarga de `MaquinaEstadosPedido.AlcanzaAmbito` que toma la materia; la que toma el `Pedido` **delega** en ella.
- [x] 1.2 `IRepositorioPedidos.UbicarPorNumerosAsync`: id, número, materia y carrera, sin colecciones.
- [x] 1.3 `IDesignacionesQueries.UbicarPedidosAsync` en el `Contracts` (deja de ser placeholder).
- [x] 1.4 Implementación: descarta lo que no tiene forma de número de trámite, resuelve el actor y filtra con `AlcanzaAmbito`.
- [x] 1.5 Test: el jefe ubica el de su materia y no el ajeno; el coordinador el de su carrera; el global todos.
- [x] 1.6 Test: un número inexistente no devuelve nada y no rompe.
- [x] 1.7 Test: el filtro de forma descarta texto libre sin ir a la base.

## 2. El puerto del asistente

- [x] 2.1 `IResolutorDeVinculos` en `Modules.Asistente.Application`, con la implementación vacía que registra el módulo.
- [x] 2.2 `BuscadorDeVinculos` puro: de columnas y filas a candidatos, y de candidatos resueltos a vínculos con fila y columna.
- [x] 2.3 Test: sólo se proponen candidatos que parecen identificadores; el texto con espacios no viaja.
- [x] 2.4 Test: un candidato que aparece en dos celdas produce dos vínculos.
- [x] 2.5 Test: sin resolución no hay vínculos (el puerto vacío no inventa ninguno).

## 3. El adaptador en el Host

- [x] 3.1 `VinculosDeDesignaciones`: valida la política `designaciones.ver` contra el principal y delega en `IDesignacionesQueries`.
- [x] 3.2 Registro después de `AddAsistenteModule`, reemplazando la implementación vacía.
- [x] 3.3 Test: en el contenedor del Host, `IResolutorDeVinculos` resuelve al adaptador y no a la implementación vacía.
- [x] 3.4 Test: sin el permiso en el principal no se ubica nada, aunque el ámbito alcance.

## 4. El turno y el contrato

- [x] 4.1 `ResultadoDelTurno` gana `Vinculos`; `RespuestaDelAsistente` lo mapea.
- [x] 4.2 `CapaConversacional` los resuelve sólo con filas, y un fallo del resolutor **no** tumba el turno.
- [x] 4.3 Test: un turno sin filas no consulta al resolutor.
- [x] 4.4 Test: si el resolutor revienta, el turno responde igual y sin vínculos.
- [x] 4.5 `docs/architecture/api-contracts.md`: el campo nuevo (invariante #6).

## 5. La interfaz

- [x] 5.1 Tipos del cliente y mapa `tipo → destino`; un tipo desconocido no se pinta.
- [x] 5.2 `TablaDeResultado` enlaza la celda, con nombre accesible que dice a dónde va.
- [x] 5.3 `LanzadorAsistente` cierra al cambiar de ruta.
- [x] 5.4 Test: la celda con vínculo es un enlace a la ruta del detalle; la de al lado no.
- [x] 5.5 Test: un `tipo` que el cliente no conoce deja la celda como texto.
- [x] 5.6 Test: navegar desde el modal lo cierra y la conversación sigue al reabrirlo.
