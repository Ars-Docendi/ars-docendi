# Tasks

## 1. Base de datos: dedicación por catálogo y sectores externos

- [x] 1.1 Generar la migración con `node scripts/migrations/scaffold.mjs Designaciones DedicacionCatalogoYExternos` y verificar que se crearon el SQL en `database/designaciones/` y su wrapper EF.
- [x] 1.2 En el SQL, dejar sin uso `designaciones.dedicacion` y `pedidos.dedicacion_solicitada` (los triggers siguen rechazando texto nuevo; no se eliminan). Verificar con `--validar-recursos` y con `MigracionesRuntimeTests` (instalación nueva y reejecución) en verde.
- [x] 1.3 Ampliar el catálogo a 0–6 (reemplazar `dedicaciones_codigo_check`) e insertar la Categoría 0 con `orden = 0`. Verificar con un test de integración que se puede registrar una designación con Categoría 0.
- [x] 1.4 Crear `designaciones.sectores_externos` (id, codigo único, nombre, activo, created_at), sembrar los nueve sectores con id fijos y adjuntarle auditoría. Verificar con un test que el catálogo trae los nueve.
- [x] 1.5 Agregar `sector_externo_id` con FK en `designaciones` y `pedidos`, volver nullable `designaciones.cargo_id` y agregar los `CHECK` de coherencia (decisión 2). Reemplazar `pedidos_completitud_circuito` y reescribir `validar_dedicacion_vigente` y `validar_dedicacion_pedido`. Verificar con tests de integración que la base acepta un externo válido y rechaza las combinaciones mixtas o incompletas, y que un sector inactivo se rechaza en una nueva selección.
- [x] 1.6 Actualizar entidades, mapeos y el snapshot EF de `DesignacionesDbContext` (`SectorExterno`, `CargoId?`, sin `Dedicacion`/`DedicacionSolicitada`). Verificar con `node --test scripts/tests/migrations.test.mjs`, `node scripts/migrations/validate.mjs` y `--validar-recursos` tras `dotnet build -c Release`.
- [x] 1.7 Documentar en `docs/architecture/data-model.md` y `docs/architecture/domains/designaciones.md` el catálogo 0–6, la tabla de sectores y la regla interno/externo. Verificar con `pnpm format:check` y `node scripts/docs/check-links.mjs`.

## 2. Backend de Designaciones

- [x] 2.1 Quitar la lectura de respaldo por texto (`?? Dedicacion`, `?? DedicacionSolicitada`) en lote, consultas, administración, avisos y diagnóstico. Verificar que `dotnet build` no deja referencias a las propiedades eliminadas.
- [x] 2.2 Exponer `sectoresExternos` en el catálogo de Designaciones y `sectorExternoId`, junto con el nombre y el código del sector, en pedidos, designaciones vigentes, snapshot y ficha de pedido. Verificar con tests de la API de catálogos y de detalle de pedido.
- [x] 2.3 Validar en crear, editar, enviar, reenviar y aprobar la regla interno/externo con errores por campo. Verificar con tests de `PedidosApiTests` para Alta externa sin sector, externo con cargo y Alta interna sin cargo.
- [x] 2.4 Materializar el sector al aprobar (alta y cambio, incluida la conversión interno ↔ externo) y guardar `SectorExterno` en el snapshot. Verificar con un test de integración del Cambio de interno a externo que conserve el estado anterior en el snapshot.
- [x] 2.5 Adaptar `CanonicoSellos` (decisión 5): quitar `dedicacion_solicitada` y sumar `sector_externo_id` y el `sector` del snapshot. Verificar con `IntegridadPedidosTests` en verde y un caso nuevo en el que un pedido externo sellado verifica y una alteración de su sector se detecta.
- [x] 2.6 Lote: celda única de cargo/dedicación con la sigla del sector para externos, en la propuesta, la designación anterior, `ALTAS` y `BAJAS`. Verificar con tests del lote para continuidad externa, Alta externa y Cambio de interno a externo.
- [x] 2.7 Contracts y administración: `CargoId`/`DedicacionId` nullables y `SectorExternoId` en los DTO de designación vigente. `ServicioAdministracionDesignaciones` valida la coherencia y conserva los externos; se actualizan `ArsDocendi.Host/Administracion` (modelos, servicio, etiquetas y política de auditoría). Verificar con un test de `AdministracionPersonasTests` que guardar una persona conserva su designación externa.
- [x] 2.8 Actualizar `docs/architecture/api-contracts-designaciones.md` y `docs/architecture/api-contracts-administracion.md` con los campos nuevos y los nullables. Verificar con `node scripts/docs/check-links.mjs`.

## 3. Asistente

- [x] 3.1 Generar con el scaffold una migración del contexto Asistente que conceda `SELECT` sobre `sectores_externos` y las columnas `sector_externo_id`, con `COMMENT ON` para la tabla y las columnas nuevas, y actualizar `database/asistente/manifiesto-privilegios.json` y `manifiesto-sensibilidad.json`. Verificar con el test del manifiesto del asistente.
- [x] 3.2 Pasar `CompiladorDePlan` y `BuscadorDeMenciones` a `LEFT JOIN cargos` y exponer el sector. Verificar con un test de integración en el que un externo aparece en un listado del asistente.

## 4. Frontend

- [x] 4.1 Tipos y API (`types.ts`, `catalogos.ts`, `pedidosApi.ts`): sectores externos, `sectorExternoId` y cargo y dedicación nullables. Actualizar `src/test/contratos/pedido-detalle.json`. Verificar con `pnpm --filter frontend test:run` en `pedidosApi.test.ts`.
- [x] 4.2 Componente `SelectorSectorExterno` e interruptor "Docente externo" en `SeccionDesignacionSolicitada`, con la regla en `pedidoValidacion.ts`. Verificar con tests de `PedidoForm` para activar, desactivar y enviar sin sector.
- [x] 4.3 Mostrar "Externo – {nombre} ({código})" en `DatosActualesPanel`, `ResumenPedido`, el detalle y el tablero. Verificar con tests de `DetallePedidoPage` y `DatosActualesPanel`.
- [x] 4.4 Interruptor de externo por fila en `features/personas/AsignacionesPersona`, con la validación en `formularioPersona.ts`. Verificar con un test del editor que guarda una designación externa.
- [x] 4.5 Actualizar los artículos del Centro de ayuda `crear-un-pedido`, `editar-un-pedido`, `descargar-el-lote-de-finalizados` y `administrar-personas`. Verificar con `integridad.test.ts` del Centro de ayuda.

## 5. Designaciones sólo por pedido

- [x] 5.1 Backend de personas: quitar designaciones de `GuardarPersonaDto`, del servicio del Host y de `IAdministracionDesignaciones` (validar y reemplazar), y la regla de asignación obligatoria; ajustar los tests que usan esas operaciones. Verificar con `dotnet build` sin errores.
- [x] 5.2 Frontend de personas: designaciones en modo lectura (internas y externas), sin validación de asignaciones ni envío. Verificar con `tsc` y lint.
- [x] 5.3 Cargadores: regla en `MaquinaEstadosPedido` para crear, editar, eliminar, enviar y corregir el primer nivel; rol del historial; migración de Identity que otorga `designaciones.gestionar` a Administrativo y Secretaría. Verificar con `dotnet build` y la migración aplicada en una base desechable.
- [x] 5.4 Frontend de pedidos para Administrativo y Secretaría: todas las materias en el Alta y el acceso a Nuevo pedido. Verificar con `tsc` y lint.
- [x] 5.5 Alta de persona existente: endpoint de búsqueda y pop-up en el Alta. Verificar con `dotnet build`, `tsc` y lint.
- [x] 5.6 Documentación: `api-contracts-administracion.md`, `api-contracts-designaciones.md` y los artículos de ayuda de personas y de crear pedido. Verificar con `check-links`.

## 6. Verificación integral

- [ ] 6.1 Correr `dotnet test backend/ArsDocendi.slnx` filtrado a Designaciones, Administración, Asistente e Integridad, y `pnpm --filter frontend test:run`, `lint` y `build`. Verificar que todo pasa.
- [x] 6.2 Sobre una base desechable recién migrada, correr los scripts de carga inicial (fuera del repo) con la Categoría 0 y los externos incluidos. Verificar que cargan sin errores y que repetirlos no duplica nada.
- [ ] 6.3 Correr `pnpm exec openspec validate --all` y verificar que el change valida.

## Workflow follow-up

- Archivar el change después de la revisión del PR.
