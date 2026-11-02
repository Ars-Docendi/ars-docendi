# Proposal

## Why

El circuito de pedidos de designación deja trazabilidad (`designaciones.pedido_historial` y
`audit.change_log`), pero no integridad: quien tenga las credenciales de la base puede cambiar el
estado de un pedido, reescribir quién aprobó o qué se justificó, borrar eventos y luego borrar las
filas de auditoría que lo delatan. Para un sistema que queda en producción y cuyo resultado (el
lote) llega a Decanato, una alteración interna fuera del circuito debe ser **detectable e
irrefutable**, y esa evidencia no puede depender sólo de la base que se quiere proteger.

## What Changes

- Cada evento del circuito de un pedido, desde su primer envío a revisión, se **sella** en una
  cadena de hashes por período: cada sello incluye el hash del anterior, el hash del estado
  canónico del pedido tras el evento y una firma HMAC-SHA256 con una clave que vive en los
  secretos del host, nunca en la base.
- Nuevas tablas append-only `designaciones.pedido_sellos` y `designaciones.periodo_anclas`. La base
  rechaza UPDATE/DELETE sobre ellas y sobre `pedido_historial`, y rechaza borrar un pedido que ya
  salió de borrador. El encadenamiento se serializa por período en la base (sin bifurcaciones).
- Verificación de integridad por período que detecta: sello con hash o firma inválida, secuencia
  rota, evento de historial sin sello, pedido cuyo estado actual diverge del último sello y ancla
  que la cadena ya no reproduce.
- `GET /api/designaciones/periodos/{id}/integridad`, protegido por el permiso existente
  `auditoria.ver`.
- La exportación del lote **verifica la cadena antes de generar el archivo**: si no está íntegra
  responde 409 y no exporta. Si está íntegra, registra un ancla e **imprime el hash de cabeza** en
  el XLSX, que es el ancla externa institucional.
- El diagnóstico operativo de integridad existente incorpora los hallazgos de la cadena.
- Comando explícito del host para sellar el historial existente (previo a este change o sembrado),
  marcado como retroactivo.
- La aplicación no arranca en producción sin la clave de sellado configurada.

No hay cambios breaking en la API ni en la UI. Los borradores siguen siendo editables y
eliminables por el Jefe de Cátedra: la cadena empieza en el envío a revisión.

## Capabilities

### New Capabilities

- `integridad-circuito-pedidos`: sellado encadenado y firmado de los eventos del circuito de
  pedidos, inmutabilidad en base, verificación, anclaje y sellado retroactivo.

### Modified Capabilities

- `exportacion-lote-designaciones`: la exportación exige cadena íntegra (409 si no) y el archivo
  incluye el hash de cabeza anclado.
- `persistencia-designaciones`: `pedido_historial` pasa a ser append-only y un pedido fuera de
  borrador no puede borrarse físicamente.

## Impact

- **Módulos**: sólo Designaciones (y configuración del Host). Sin cambios en `*.Contracts`, sin
  consumidores cross-module nuevos y sin edges nuevos en el grafo de dependencias.
- **Base de datos**: migración incremental en `designaciones` (dos tablas, triggers, funciones). No
  se modifica SQL histórico.
- **Backend**: `ServicioPedidos` (sellar al registrar historial), `ServicioLoteDesignaciones`
  (verificar + anclar + imprimir hash), nuevos `SelladorPedidos` y `VerificadorIntegridadPedidos`,
  lector de diagnóstico, endpoint, comando del host, configuración `Integridad:*`.
- **Dependencias**: ninguna nueva (`System.Security.Cryptography`).
- **Operación**: nueva clave secreta por ambiente y procedimiento de rotación. Se recomienda separar
  el rol dueño/migrador del rol de la app; queda documentado como operación, sin bloquear el change.
- **Reglas**: nuevas reglas de proceso `BR-designaciones-019` (eventos sellados y append-only),
  `BR-designaciones-020` (exportación sólo con cadena íntegra y anclada). Son reglas de
  seguridad del sistema, no normativa institucional; su fuente es este change.
- **Rollback**: la migración es aditiva. Para revertir el comportamiento se despliega la versión
  anterior de la app; las tablas y los sellos quedan sin uso y no afectan al circuito. Desactivar
  los triggers append-only requiere una migración nueva y explícita, nunca editar la publicada.
