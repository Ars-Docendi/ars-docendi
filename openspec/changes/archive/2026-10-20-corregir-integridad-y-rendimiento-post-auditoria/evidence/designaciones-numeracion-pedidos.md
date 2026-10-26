# Evidencia — numeración de pedidos de Designaciones

## Criterio 3.1

La migración incremental `20261013155148_NumeracionSinTruncamientoDesignaciones` reemplaza la función de numeración sin alterar la secuencia ni escribir/renumerar pedidos existentes. El cambio usa padding mínimo de cuatro dígitos, evalúa `nextval` una sola vez y conserva los gaps propios de secuencias PostgreSQL. El SQL y su wrapper provienen del scaffolder; no se editó el baseline histórico.

## RED → GREEN

- **RED causal en baseline:** en una base recién migrada hasta `20261005000200_BaselineDesignaciones`, una aserción temporal antes de aplicar el upgrade exigió sufijo `-10001`. La función histórica `lpad(nextval(...)::text, 4, '0')` devolvió `2026-1000`; el filtro falló 1/1 como se esperaba. Evidencia: `.artifacts/apply-post-auditoria/task-3-1-history/task-3-1-baseline-red.trx`. La aserción temporal se retiró del código fuente.
- **GREEN:** build Release exitoso; `NumeracionPedidosTests` pasó **6/6**, 0 fallidos y 0 omitidos. TRX: `.artifacts/apply-post-auditoria/task-3-1-history/task-3-1-green.trx`.
- El filtro cubre correlativos `1000`, `9999`, `10000` y `10001`; 16 asignaciones concurrentes distintas; gap no reutilizado tras rollback; y upgrade/no-op sobre la base.
- El upgrade inserta primero una fila histórica sintética `2023-0017`, registra el conteo de auditoría y fija la secuencia en 10001. Tras migrar, la fila conserva exactamente su número, el conteo de auditoría no cambia, la siguiente asignación termina en `-10001` y reaplicar migraciones no-op deja la siguiente en `-10002`.

El build reporta dos advertencias preexistentes xUnit2031 en `Portal/ConcurrenciaSeccionesTests.cs`; no reporta errores. No se usaron bases compartidas ni se hizo commit/push/deploy.
