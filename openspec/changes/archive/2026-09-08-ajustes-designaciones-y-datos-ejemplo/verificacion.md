# Verificación parcial — 2026-09-07

Se retomó la tarea 1.3: adaptación del catálogo y payload de pedidos a UUIDs, selector de dedicación en alta/edición de docentes, preservación de lectura histórica y actualización de contratos documentados. No se marca completa porque falta verificar el recorrido backend y referencias inactivas.

- `pnpm --filter frontend test:run src/features/docentes/components/AsignacionesSelector.test.tsx src/features/designaciones/api/pedidosApi.test.ts src/test/remoteAdapters.test.ts`: 14 tests aprobados.
- `pnpm --filter frontend build`: aprobado.
- `pnpm exec openspec validate --all --strict`: 38 elementos aprobados.
- `git diff --check`: aprobado.
- `dotnet test backend/ArsDocendi.slnx --no-restore`: 78 aprobados y 36 fallidos. PostgreSQL y Docker estuvieron disponibles. El seed anterior inserta dedicaciones textuales y dispara SQLSTATE 23514, «La designación requiere una dedicación del catálogo»; fallan por ello catálogos y recorridos que dependen del seed. También hay expectativas anteriores de DbUpdateException que ahora reciben ErrorDominioPedido por validación de dedicación.

La ejecución queda pausada ante estos errores conforme a openspec-apply-change. Corresponde actualizar el seed y los fixtures afectados, conservando las pruebas de rollback, antes de cerrar la tarea 1.3. Las tareas permanecen en 2/27; no se archivó el cambio. Se conservaron los cambios locales previos de backend, Sidebar y DetallePedidoPage.
