# Tasks

## 1. Base de datos

- [x] 1.1 Crear con `node scripts/migrations/scaffold.mjs Designaciones ConciliacionLote` la migración que reemplaza `pedidos_estado_valido` (+ `aprobado`, `no_aprobado`), `pedido_historial_accion_valida` (+ `sincronizar`, `no_aprobar`) y `pedido_historial_etapa_valida` (+ `aprobado`, `no_aprobado`)
- [x] 1.2 En la misma migración, agregar `pedidos.materializado_en timestamptz NULL` con backfill `now()` para los pedidos `en_lote` existentes
- [x] 1.3 Revisar la regla de `20261013173721_CompletitudCircuitoDesignaciones` que enumera estados y, si corresponde, ampliarla en la nueva migración para `no_aprobado` y `aprobado`
- [x] 1.4 Actualizar en una migración incremental los comentarios de columna del asistente que describen los estados del pedido
- [x] 1.5 Crear con `scaffold.mjs Identity PermisosConciliacion` la siembra de `designaciones.conciliar` y `designaciones.editar` y su concesión a `secretaria` y `administrativo`
- [x] 1.6 Agregar las dos constantes a `ArsDocendi.Shared/Auth/Permisos.cs` y mantener coherentes entidades, mapeos y snapshot EF de `Pedido` (`MaterializadoEn`)

## 2. Dominio y permisos del actor

- [x] 2.1 `EstadosPedido`: agregar `Aprobado` y `NoAprobado`, sacar `en_lote` de `Terminales` y sumar `aprobado`; `AccionesHistorial`: agregar `Sincronizar` y `NoAprobar`
- [x] 2.2 `ActorContexto` + `ResolutorActor`: exponer los permisos efectivos de la identidad persistida y `TienePermiso(code)`
- [x] 2.3 `MaquinaEstadosPedido`: transiciones `sincronizar` (`en_lote` → `aprobado`) y `no_aprobar` (`en_lote` → `no_aprobado`, motivo obligatorio), guard por `designaciones.conciliar` y ámbito, y rechazo de las acciones del circuito sobre `en_lote` y `no_aprobado`
- [x] 2.4 `PuedeEditar`: habilitar `en_lote` y `no_aprobado` con `designaciones.editar` en ámbito; editar un `no_aprobado` lo transiciona a `en_lote`
- [x] 2.5 Tests de dominio: transiciones válidas e inválidas de conciliación, motivo obligatorio y edición con y sin permiso

## 3. Materialización diferida y sincronización

- [x] 3.1 `ServicioPedidos`: quitar la materialización de la aceptación de Decanato (test: aceptar en Decanato no cambia designaciones)
- [x] 3.2 Extender `IAdministracionIdentity`/`ServicioPersonas` con `AsignarRolDocenteAsync` y `RetirarRolDocenteAsync`: sin efecto si la persona no tiene cuenta, idempotentes y con `granted_by` del actor
- [x] 3.3 `ServicioConciliacion.SincronizarAsync`: dentro de `IUnidadDeTrabajoAdministracion`, bloquear por persona y materia, validar estado, versión, ámbito y permiso, materializar salvo `materializado_en`, asignar o retirar el rol Docente, pasar a `aprobado`, registrar y sellar `sincronizar`, todo o nada e idempotente
- [x] 3.4 `ServicioConciliacion.NoAprobarAsync`: motivo obligatorio, versión, sellado, y denegación sobre pedidos heredados (`materializado_en` no nulo)
- [x] 3.5 `ServicioConciliacion.ConsultarAsync`: personas del período en ámbito, con sus designaciones vigentes de todas las materias y sus pedidos `en_lote`, `no_aprobado` y `aprobado`
- [x] 3.6 Tests de integración (base desechable): Alta con y sin cuenta, Baja que retira el rol, Cambio intradía y posterior, conflicto de versión sin efectos parciales, reintento idempotente, heredado sin doble materialización y verificación de la cadena de sellos

## 4. Administración de personas

- [x] 4.1 Membresías `docente` sugeridas para una persona sin cuenta, una por designación vigente (materia, carrera), derivadas de las `asignaciones` que `ServicioAdministracionPersonas.ObtenerAsync` ya devuelve (sin cambio de API); `GuardarAsync` sigue guardando exactamente la lista enviada
- [x] 4.2 Editor de personas: precargar esas membresías con la leyenda "Se asigna al crear la cuenta, según sus designaciones", con la posibilidad de quitarlas antes de guardar
- [x] 4.3 Test de flujo: una persona con un Alta sincronizada y sin mail muestra el rol Docente precargado; al cargar el mail y guardar, la cuenta queda con ese rol; si el operador lo quita, la cuenta queda sin él

## 5. API

- [x] 5.1 `ConciliacionController`: `GET /api/designaciones/conciliacion`, `POST /api/designaciones/conciliacion/sincronizar` (con `Idempotency-Key`) y `POST /api/designaciones/pedidos/{id}/no-aprobar`, con 403 sin permiso y 409 identificando el pedido en conflicto
- [x] 5.2 `PUT /api/designaciones/pedidos/{id}`: aceptar la edición de `en_lote` y `no_aprobado` según `PuedeEditar`; `acciones` del detalle suma `editar` en esos estados
- [x] 5.3 `VistaPedido.Simplificada`: `en_lote` → `en_proceso`, `aprobado` → `aprobado` y `no_aprobado` → `rechazado` con su motivo

## 6. Lote

- [x] 6.1 `ServicioLoteDesignaciones`: pedidos del lote `en_lote` + `aprobado`, excluir `no_aprobado`, y proyectar en `PROPUESTA COMPLETA` los Cambios `en_lote` con los valores del pedido
- [x] 6.2 Tests del lote: Cambio no sincronizado, Baja `en_lote` sin continuidad duplicada, Alta `aprobado` sin duplicar fila y `no_aprobado` ausente de las tres hojas

## 7. Frontend

- [x] 7.1 Tipos y API: `EstadoPedido` suma `aprobado` y `no_aprobado`, más `conciliacionApi` (consulta, sincronizar, no aprobar) con React Query
- [x] 7.2 `ConciliacionPage` según el prototipo validado: pestañas, filtros, filas por docente con carreras, detalle Hoy/Queda con horas complementarias, justificación y tipo de baja, aceptación por docente y todos, botón Sincronizar junto a las pestañas, y estados Loading/Empty/Error
- [x] 7.3 Modales: rechazar con motivo obligatorio y vista previa de Sincronizar (totales, detalle con carrera y aviso de que no se puede deshacer)
- [x] 7.4 Edición: abrir `PedidoFormPage` para `en_lote` y `no_aprobado`, con el aviso de que después se podrá conciliar y sincronizar
- [x] 7.5 Ruta `designaciones/conciliacion` con `RequirePermission` y entrada de navegación por `designaciones.conciliar`
- [x] 7.6 Tablero de Revisión, badges y detalle: ubicar y rotular `aprobado` y `no_aprobado` (En lote, Aprobado, Rechazado en conciliación)
- [x] 7.7 Tests de flujo: aceptar, sincronizar y ver Sincronizado; rechazar sin motivo; editar un rechazado que vuelve a Por conciliar; sin permiso no hay enlace ni ruta

## 8. Documentación

- [x] 8.1 `docs/architecture/api-contracts-designaciones.md`: endpoints nuevos y la edición ampliada
- [x] 8.2 `docs/architecture/data-model.md` y `docs/architecture/domains/designaciones.md`: estados, eventos, `materializado_en`, la escritura del rol Docente vía `IAdministracionIdentity` y la materialización en la sincronización
- [x] 8.3 `docs/product/designs/conciliacion-designaciones-design-spec.md` desde `_design-spec-template.md`, con el prototipo validado
- [x] 8.4 Centro de ayuda: artículo nuevo "Conciliar el lote" y actualizar `descargar-el-lote-de-finalizados`, `editar-un-pedido`, `seguir-el-estado-de-un-pedido` y el artículo de roles y permisos

## 9. Verificación

- [ ] 9.1 `dotnet test backend/ArsDocendi.slnx`
- [x] 9.2 `node --test scripts/tests/migrations.test.mjs`, `node scripts/migrations/validate.mjs` y `--validar-recursos` tras `dotnet build -c Release`
- [x] 9.3 `pnpm --filter frontend test:run`, `pnpm --filter frontend lint` y `pnpm --filter frontend build`
- [x] 9.4 `pnpm format:check`, `node scripts/docs/check-links.mjs` y `pnpm exec openspec validate --all`
