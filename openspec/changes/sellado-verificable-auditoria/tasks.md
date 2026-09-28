# Tasks

## 1. Secuencia y línea base auditada

- [ ] 1.1 Agregar SQL versionado y migración EF para cursor transaccional, tablas de control e índices en `audit`; verificar bases nuevas/existentes, rollback de transacción, concurrencia y re-ejecución `--migrate` con pruebas PostgreSQL.
- [ ] 1.2 Instrumentar el trigger para asignar `seal_seq` sin omitir confirmaciones tardías y hacer bootstrap del histórico bajo corte seguro; verificar con test concurrente que ningún evento puede entrar en un rango cerrado y que el histórico queda marcado como línea base no autentificada retrospectivamente.
- [ ] 1.3 Medir latencia y throughput del trigger bajo escrituras concurrentes, definir umbral de aceptación antes de prod y documentar DDL, impacto e invariantes en `docs/architecture/data-model.md`; verificar benchmark reproducible y diff de esquema/modelo EF sin contradicción.

## 2. Sellado y doble custodia

- [ ] 2.1 Implementar canonicalización versionada con vectores de prueba de JSON, tipos, null, UTC, unicode y nonce; verificar hashes reproducibles y ausencia de metadatos sensibles en manifiestos.
- [ ] 2.2 Implementar ejecutable one-shot de cierre encadenado y publicación idempotente; probar reintentos, lote vacío, fallo del primer/segundo testigo, caída intermedia y rechazo de cambio de rango ya cerrado.
- [ ] 2.3 Definir permisos mínimos para API/migraciones/sellador, servicio de firma externo y dos testigos bajo administraciones independientes; verificar con pruebas negativas que API y runners PR no pueden obtener claves ni modificar auditoría/sellos, y que staging/pr-N no pueden publicar en prod.
- [ ] 2.4 Integrar scheduler sin puertos públicos, variables de ejemplo sin secretos, aprovisionamiento y runbook de custodia/rotación; verificar que Compose de prod/staging/pr-N mantiene aislamiento y que el flujo de rotación conserva la validación de sellos históricos.

## 3. Comprobación y respuesta

- [ ] 3.1 Implementar verificador de lotes, continuidad, firmas y doble testigo; probar alteración, eliminación, duplicado, reordenamiento, retroceso, indisponibilidad y fallo de firma sin corrección automática.
- [ ] 3.2 Implementar inventario y digest de estado completo de tablas críticas declaradas, más cotejo incremental de filas afectadas; probar alteración directa, fila no modificada desde baseline, DELETE físico/soft-delete y reporte explícito de cobertura parcial.
- [ ] 3.3 Implementar sonda externa de frescura y alerta por resultados faltantes, testigos divergentes, sellos incompletos y verificación vencida; verificar que un verificador local silencioso no aparece como saludable y que la sonda no afirma comprobar contenido sin lectura independiente.
- [ ] 3.4 Documentar métricas, estados, umbrales, limitaciones y respuesta a incidentes en runbook/arquitectura; verificar que el ejemplo de alerta omite PII, claves y snapshots y que los canales operativos llegan fuera de la VM.

## 4. Recuperación e integración

- [ ] 4.1 Extender ensayo de restauración en destino descartable con lectura de testigos de prod y sin posibilidad de escribirlos; probar que detecta backup atrasado y exige reconciliación antes de reanudar los sellos.
- [ ] 4.2 Ejecutar suite backend completa, pruebas de concurrencia/permisos y pruebas end-to-end de sellado/restauración sobre infraestructura aislada; verificar `git diff --check`, `openspec validate sellado-verificable-auditoria --strict` y ausencia de secretos en artefactos.
