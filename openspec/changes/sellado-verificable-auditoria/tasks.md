# Tasks — sellado verificable de auditoría

Los ítems de implementación se cierran con código y evidencia local. Los gates de producción al final **no** se consideran cumplidos por pasar tests con dobles de testigos; hasta resolverlos, el cambio no puede habilitarse ni archivarse.

## 1. Secuencia y línea base

- [x] 1.1 Cursor transaccional, tablas e índices `audit` mediante SQL versionado/migración EF. PostgreSQL aislado: base nueva, rollback, concurrencia y migración repetida.
- [x] 1.2 Trigger con `seal_seq` y corte legacy consistente; digest legacy en manifiesto, baseline `anchored` sólo tras doble acuse. El anclaje acredita el estado observado, no autenticidad retrospectiva.
- [x] 1.3 DDL/invariantes documentados, `Database.HasPendingModelChanges()` sin cambios. Microbenchmark reproducible de cinco corridas/50 escritores: mediana 714,3 ms; `backend/TestResults/audit-seal/benchmark.json`. El SLO de producción queda en G5.

## 2. Sellado y custodia — implementación

- [x] 2.1 Canonicalización versionada y SHA-256: JSON numérico sin pérdida, null, UTC, unicode, nonce, ambiente, continuidad y rechazo de huecos/duplicados; manifiesto sin snapshots ni IDs.
- [x] 2.2 Job one-shot de cierre encadenado, reintentos idempotentes, heartbeat firmado sin eventos y rechazo de lote alterado. Antes de preparar/publicar consulta ambos testigos: génesis exige doble 404 y baseline `observed`; un backup atrasado o hash/rango divergente impide publicar, pero se permite reintentar un acuse parcial. La firma del firmador se comprueba con clave pública fijada **antes** de enviar el manifiesto. Contrato real y retención: G1.
- [x] 2.3 Migración de hardening del trigger, permisos API negativos y script de rol sellador con SELECT de eventos/estado, INSERT de lotes y UPDATE limitado de firma/acuses/baseline. ACL efectivas y separación administrativa en infraestructura: G2.
- [x] 2.4 Compose/timer one-shot sin puertos públicos, variables sin secretos embebidos, configuración fail-closed de lectores y runbook de rotación. Instalación/aislamiento prod–staging–PR y custodia de transición: G3.

## 3. Comprobación y respuesta — implementación

- [x] 3.1 Verificador local y GET-only de dos testigos: cadena, contenido, firmas RSA-PSS históricas, claves fijadas y transiciones firmadas; retroceso, firma o testigo inválido no son saludables. Proveedor independiente real: G1.
- [x] 3.2a Checkpoint privado y digest anclado al manifiesto de la tabla **declarada** `identity.roles` (todas las columnas), replay incremental y cotejo completo de estado; detecta alteración directa, baja física y soft-delete sin publicar filas. La cobertura se anuncia parcial.
- [ ] 3.2b Ampliar el inventario al conjunto institucional de tablas críticas y fijar la periodicidad del escaneo completo. Requiere selección/aprobación del alcance: G4; no declarar íntegra la base por `identity.roles`.
- [x] 3.3 Sonda GET-only de frescura y reporte local, sin acceso DB ni listener; silencio, divergencia, sello incompleto o reporte vencido son no verificados. Despliegue y alertas fuera de VM: G3.
- [x] 3.4 Runbook de estados, métricas, umbrales pendientes de aprobación, límites y respuesta; ejemplo de alerta sin PII, claves o snapshots. Entrega real y SLO: G3/G5.

## 4. Recuperación e integración — implementación

- [x] 4.1 `pg_dump -Fc` y `pg_restore` reales por stream en dos bases Testcontainer descartables; un testigo GET-only más nuevo detecta el retroceso. La compuerta conectada al **publicador** bloquea POST tras restauración atrasada. Ensayo con lectoras de prod: G1/G6.
- [x] 4.2 Build backend sin warnings/errores; suite Testcontainer completa **234/234, 0 omitidos**, `TestResults/audit-seal-apply-20260929.trx`; `git diff --check`, validación OpenSpec estricta, `bash -n` y Compose con variables sintéticas. Búsqueda heurística de claves privadas/tokens de proveedor en `.cs` sin hallazgos; no equivale a auditoría de secretos integral.

## Gates de producción — pendientes, no simulables con el repositorio

- [ ] G1 Provisionar firmador externo y dos custodios bajo administraciones independientes; demostrar retención inmutable, GET `/latest`/404 por ambiente, POST idempotente, lectura de histórico y reintentos parciales reales. Ensayar lectura **sin escritura** de testigos prod desde restauración aislada.
- [ ] G2 Auditar owners, membresías y ACL efectivos de API/migrador/sellador/verificador/custodios en el host real y negar herencias/DDL no previstas.
- [ ] G3 Instalar jobs/sonda en dominios administrativos separados; probar aislamiento de secretos prod frente a staging/pr-N, publicación y retención de transiciones de clave y acuse de alertas fuera de la VM.
- [ ] G4 Aprobar tablas/columnas críticas y frecuencia de inventario completo; implementar cobertura aprobada y verificar que una tabla omitida no se comunique como íntegra.
- [ ] G5 Aprobar SLO/umbral y repetir benchmark con carga y recursos representativos; no habilitar si la contención del cursor lo excede.
- [ ] G6 Comparar esquema/triggers/migraciones de la base prod real con el DDL versionado antes de migrar; ensayar dump/restore y reconciliación sin tocar sus testigos de escritura.
- [ ] G7 Revisión de seguridad de artefactos, credenciales y contrato de retención por responsables institucionales; no archivar ni habilitar producción antes de aprobar G1–G6.
