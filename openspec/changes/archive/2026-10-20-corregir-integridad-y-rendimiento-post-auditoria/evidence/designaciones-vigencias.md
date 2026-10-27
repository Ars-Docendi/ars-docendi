# Evidencia — vigencias de Designaciones

## Criterios 3.2 y 3.3

### RED → GREEN: Cambio intradía

- La regresión causal se reprodujo mutando temporalmente la rama `vigente.VigenteDesde == hoy` a `if (false)`: PostgreSQL rechazó la aceptación con `23P01` en `designaciones_sin_solapamiento`. TRX RED: `.artifacts/apply-post-auditoria/task-3-2-green/task-3-2-red.trx`. Se restauró inmediatamente la rama productiva.
- GREEN: `Cambio_intradía_conserva_id_inicio_snapshot_y_audita_valores_y_origenes`, 1/1 PASS en `.artifacts/apply-post-auditoria/task-3-2-green/task-3-2-green.trx`.
- La prueba usa reloj fijo `2026-10-13`; confirma mismo ID e inicio de vigencia, cargo/dedicación y las tres cargas actualizadas, origen anterior/nuevo, snapshot inmutable y comentario serializado con valores anteriores/nuevos y fecha efectiva determinística. Verifica además `old_row`/`new_row` de auditoría con cargos, dedicaciones, horas y origen.

### RED → GREEN: Cambio anterior, reintento y rollback

- La misma suite cubre Cambio sobre vigencia anterior (cierre/apertura sin solapamiento), rechazo de inicio futuro antes de efectos parciales, doble aceptación con la misma clave idempotente, rollback de ambos caminos ante fallo de apertura/actualización, y Baja intradía que cierra al día siguiente.
- La mutación causal confirma que el camino de apertura para un Cambio intradía viola la EXCLUDE `designaciones_sin_solapamiento`; las pruebas normales conservan las semánticas de `vigente_hasta IS NULL` para la vigencia actual.
- Corrida integral Release de Designaciones: **110/110 PASS**, 0 fallidos/omitidos, TRX `.artifacts/apply-post-auditoria/designaciones-post-auditoria-3x/designaciones-post-auditoria-3x.trx`.

La base usada es desechable/Testcontainers y se elimina al finalizar. No se modificó el SQL histórico ni se utilizó una base compartida.

## Criterio 3.4 — completitud antes del circuito

- RED causal: se suprimió temporalmente el guard final que lanza los errores de completitud. `Circuito_rechaza_incompleto_por_campo_sin_snapshot_ni_evento` falló en la aserción esperada («No exception was thrown») al intentar enviar un pedido sin cargo. Se restauró el guard productivo antes del GREEN. TRX: `.artifacts/apply-post-auditoria/designaciones-completitud-red/designaciones-completitud-red.trx`.
- GREEN backend: build Release de IntegrationTests, 0 warnings/errores; **18/18 PASS** en `.artifacts/apply-post-auditoria/designaciones-completitud-green/designaciones-completitud-green.trx`. Incluye matriz de enviar/reenviar/aprobar con campos ausentes o cero, CHECK SQL para cargo/dedicación/horas, documentación/legajo/BR, Baja válida sin cargo/dedicación/horas y rechazo de Baja sin designación vigente.
- Los casos de pedido legado se construyen sólo en PostgreSQL Testcontainers desechable, retirando temporalmente el CHECK y deshabilitando el trigger dentro de la preparación del fixture. La llamada del circuito verifica validación por campo y que no cambien estado, snapshot ni historial. La migración incremental, en cambio, falla cerrada en su preflight si ya existen pedidos incompletos en estados de circuito; no repara datos ni los completa automáticamente.
- `ValidacionCircuitoPedidos` consulta el cargo y la dedicación activos; para Baja/Cambio resuelve la persona por ID, exige legajo y designación vigente. La validación se ejecuta antes de snapshot, transición y registro de evento.
- GREEN frontend: pruebas de `DetallePedidoPage` y `PedidoFormPage`, **8/8 PASS**. Verifican presentación de errores de validación por campo con etiquetas conocidas y fallback seguro.
- Los TRX y fixtures usan datos sintéticos; no contienen credenciales ni PII real.

## Criterio 3.5 — materia canónica de una designación

- La migración incremental `20261013170855_IntegridadMateriaDesignaciones` toma bloqueo de escritura sobre `designaciones.designaciones`, aborta con `23503` si el preflight encuentra referencias huérfanas y agrega una FK simple a `identity.materias(id)` con `ON DELETE RESTRICT`. No introduce FK compuesta a carrera/plan ni reasigna filas.
- RED causal: se retiró temporalmente la declaración de la FK en el SQL de la migración; las pruebas de INSERT/UPDATE fallaron con «No exception was thrown». Se restauró exactamente la declaración antes del GREEN. TRX: `.artifacts/apply-post-auditoria/integridad-materia-red/integridad-materia-red.trx`.
- `IntegridadMateriaTests`: **4/4 PASS** en `.artifacts/apply-post-auditoria/integridad-materia-green/integridad-materia-green.trx`. Cubre INSERT y UPDATE con materia inexistente, borrado de materia referenciada sólo por una designación, upgrade que aborta preservando el huérfano, y upgrade limpio posterior con constraint validada y fila conservada.
- El ensayo de upgrade usa bases PostgreSQL/Testcontainers con nombres únicos y se eliminan al finalizar. La reparación que permite continuar la segunda mitad del ensayo ocurre únicamente en esa base descartable; no forma parte del SQL de producción.
