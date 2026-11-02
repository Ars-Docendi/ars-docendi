# Tasks

## 1. Base de datos

- [x] 1.1 Generar SQL y wrapper con `node scripts/migrations/scaffold.mjs Designaciones SellosIntegridadPedidos`
- [x] 1.2 Crear `designaciones.pedido_sellos` y `designaciones.periodo_anclas` con PK, FK (sin cascada), checks de 32 bytes y de `motivo`, e índice por `pedido_id`
- [x] 1.3 Trigger `BEFORE INSERT` de `pedido_sellos`: advisory lock por período, exigir `secuencia = cabeza + 1` y `hash_anterior` = hash de cabeza (génesis = `sha256(periodo_id)`)
- [x] 1.4 Triggers append-only: `rechazar_modificacion()` en UPDATE/DELETE de sellos y anclas y en UPDATE de `pedido_historial`; DELETE de historial sólo con pedido padre en `borrador`; DELETE de `pedidos` sólo en `borrador`
- [x] 1.5 `audit.attach` sobre las dos tablas nuevas
- [x] 1.6 Entidades `PedidoSello` y `PeriodoAncla`, mapeo en `DesignacionesDbContext` y snapshot EF actualizado a mano

## 2. Sellado

- [x] 2.1 Opciones `Integridad:ClavesSellado` / `Integridad:VersionVigente` con validación al arrancar (falla en Production sin clave; clave de desarrollo en `appsettings.Development.json` y en la fábrica de tests); nunca loguear valores
- [x] 2.2 Serializador canónico del payload y del estado del pedido (orden fijo, UTC con microsegundos, sin `Version`) con test que congela el hash de un evento conocido
- [x] 2.3 `SelladorPedidos`: tomar el lock del período, leer cabeza, calcular `hash_estado`, `hash` y `firma` HMAC-SHA256 e insertar el sello; omitir pedidos que nunca salieron de borrador
- [x] 2.4 Truncar `CreadoEn` del historial a microsegundos y llamar al sellador desde `ServicioPedidos.RegistrarEnHistorial`, dentro de la misma transacción

## 3. Verificación y consumidores

- [x] 3.1 `VerificadorIntegridadPedidos` con las seis categorías de hallazgo y resultado `(integro, secuencia, hash, hallazgos)`
- [x] 3.2 Endpoint `GET /api/designaciones/periodos/{id}/integridad` con `auditoria.ver` (404 si el período no existe)
- [x] 3.3 `ExportarAsync`: verificar dentro de la transacción `RepeatableRead`; 409 `lote-integridad-comprometida` sin detalle; si es íntegra, anclar cuando la cabeza difiera de la última e imprimir hash y posición en la cabecera del XLSX
- [x] 3.4 Lector `IDiagnosticoIntegridadModulo` de Designaciones emite los hallazgos de cada período como `RegistroIntegridad("hallazgo", ...)`
- [x] 3.5 Modo `--sellar-historial-existente` en `SolicitudMigracion` y despacho en `Program.cs`: sellado retroactivo idempotente por período con ancla `retroactivo`

## 4. Tests (integración, bases desechables)

- [x] 4.1 Transición sellada y cadena íntegra; borrador eliminable sin hallazgos; sellado fallido revierte el evento
- [x] 4.2 Ataques por SQL: UPDATE de `pedidos.estado` → estado divergente; INSERT de historial → sin sello; sello con firma de otra clave → firma inválida
- [x] 4.3 Base rechaza UPDATE/DELETE de historial, sellos y anclas, DELETE de pedido enviado y sello fuera de secuencia; con triggers deshabilitados, editar un comentario sellado → hash inválido
- [x] 4.4 Dos transiciones concurrentes en el mismo período no bifurcan la cadena
- [x] 4.5 Exportación: 409 ante ruptura; hash en el libro y ancla única al repetir sin cambios; borrar un pedido enviado completo (triggers deshabilitados) → ancla no reproducida
- [x] 4.6 Endpoint 403 sin `auditoria.ver`; rotación de clave verifica sellos de ambas versiones; sellado retroactivo idempotente

## 5. Documentación

- [x] 5.1 `docs/business-rules/designaciones.md`: BR-designaciones-019 y 020 con fuente y mapping a tests
- [x] 5.2 `docs/architecture/data-model.md` y `api-contracts-designaciones.md` (tablas, triggers, endpoint, 409 de exportación)
- [x] 5.3 `docs/operations/migrations-persistence.md`: clave por ambiente, rotación, respaldo, comando retroactivo, recomendación de separar rol dueño y rol de app; paso en README tras el seed de dev
- [x] 5.4 Centro de ayuda: mencionar en el artículo de exportación de lote el hash impreso y el bloqueo por integridad

## 6. Verificación

- [x] 6.1 `dotnet test backend/ArsDocendi.slnx`
- [x] 6.2 `node --test scripts/tests/migrations.test.mjs`, `node scripts/migrations/validate.mjs`, `dotnet build -c Release` y `--validar-recursos`
- [x] 6.3 `pnpm format:check`, `node scripts/docs/check-links.mjs`, `pnpm exec openspec validate --all`
