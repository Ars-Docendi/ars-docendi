## 1. Fase 1 — Máquina de estados extendida (TDD ESTRICTO, red-green)

- [x] 1.1 Extender la union `AccionPedido` en `frontend/src/features/designaciones/api/maquinaEstados.ts` con `{ tipo: "aceptar"; comentario? }`, `{ tipo: "rechazar"; comentario }`, `{ tipo: "devolver"; comentario }`, `{ tipo: "reenviar" }`, `{ tipo: "priorizar"; comentario }` (el `switch` con `never` deja de compilar: esa es la señal a completar)
- [x] 1.2 RED: escribir en `maquinaEstados.test.ts` los tests que fallan primero, uno por fila/guard de la tabla §6.5: `aceptaCoordinadorAvanzaASecretaria`, `aceptaSecretariaAvanzaADecanato`, `aceptaDecanatoVaAEnLote`, `administracionNoPuedeAceptar` [BR-015], `rechazoSinJustificativoFalla` / `rechazoEsTerminal` [BR-005, BR-011], `devolucionSinComentarioFalla` / `devolucionRetrocedeUnNivel` / `reenvioRetomaEtapaDelRevisor` [BR-005, BR-014], `rolEtapaIncorrectaDenegado` [BR-013], `coordinadorFueraDeCarreraDenegado` [BR-009], `prioritarioExigeJustificativo` / `prioridadNoCambiaEstado` [BR-017]
- [x] 1.3 GREEN: implementar las funciones `aceptar`/`rechazar`/`devolver`/`reenviar`/`priorizar` y los mapas `SIGUIENTE_ETAPA` / `PROPIETARIO_DEVOLUCION` / `ROL_DE_ETAPA`, más los guards transversales de etapa [BR-013] y ámbito (`actorAlcanzaAmbito`) [BR-009], hasta poner los tests en verde; completar el `switch` (sin reescribir las ramas de SCRUM-7)
- [x] 1.4 Exponer los predicados que la UI necesita derivados del dominio (sin duplicar lógica): `puedeRevisar(pedido, actor)` / `puedeAceptar(pedido, actor)` (revisor de la etapa en su ámbito) y `actorAlcanzaAmbito(pedido, actor)`
- [x] 1.5 Verificar `pnpm --filter frontend test:run` en verde para toda la suite de `maquinaEstados` (12 de SCRUM-7 + 15 nuevos), sin regresiones del lado JC (49 tests verdes en total)

## 2. Fase 2 — Seam `api/` + hooks + contexto + seed

- [x] 2.1 Extender `api/pedidosApi.ts` con `aceptarPedido` / `rechazarPedido` / `devolverPedido` / `reenviarPedido` / `priorizarPedido` (async, delegan a `aplicarAccion`) y `listarPedidosPorAmbito(actor)` (filtra el store con `actorAlcanzaAmbito`); cada función con su `// TODO(backend): <endpoint real> — SCRUM-8. Mock actual: ...` (formato fijo §7)
- [x] 2.2 Extender `api/contextoActor.ts`: agregar al mapa de ámbito Secretaría / Decanato / Administración (depto-wide, sin `carrera`), manteniendo la firma de `construirActorContexto`
- [x] 2.3 Extender `api/pedidosSeed.ts` con pedidos en `en_revision_secretaria`, `en_revision_decanato`, `rechazado`, `en_lote` y de otra carrera (ejercita el ámbito del Coordinador [BR-009]), con historial de fechas fijas
- [x] 2.4 Extender `hooks/useAccionesPedido.ts` con las mutations `useAceptarPedido` / `useRechazarPedido` / `useDevolverPedido` / `useReenviarPedido` / `usePriorizarPedido` (invalidan `["pedidos"]` en `onSuccess`)
- [x] 2.5 Extender `hooks/usePedidos.ts` con `usePedidosPorAmbito(actor)` (queryKey namespaced `["pedidos", "ambito", ...]`)
- [x] 2.6 Tests del seam en `pedidosApi.test.ts`: `listarPedidosPorAmbito` acota por carrera para el Coordinador y es depto-wide para Secretaría/Decanato/Administración; una aceptación persiste el avance de etapa entre recargas (reset + re-hidratación). Verificado: no hay `// TODO(backend)` fuera de `api/`

## 3. Fase 3 — UI del revisor (Kanban + detalle + modal)

- [x] 3.1 Crear `components/PedidoCard.tsx` (presentacional: docente + cátedra + novedad + `EstadoPedidoBadge` + flag prioritario; click → detalle) y `components/ColumnaKanban.tsx` (título + cuenta + lista de cards; estado vacío)
- [x] 3.2 Crear `components/TableroRevision.tsx` (orquesta las 4 columnas Pendiente-mi-etapa / Aprobado / Rechazado / Devuelto a partir de la lista por ámbito) y `pages/TableroRevisionPage.tsx` (consume `usePedidosPorAmbito`, renderiza Loading / Empty / Error / Success)
- [x] 3.3 Crear los adapters de presentación puros: `accionAAuditVerb(accion)` (español → `AuditVerb`) y `derivarTimeline(pedido)` (estado + historial → `TimelineStep[]`), con `switch` exhaustivo (`never`); ubicados junto a los componentes de detalle (NO en `maquinaEstados.ts`)
- [x] 3.4 Crear `components/ModalAccionRevision.tsx` (Modal + Textarea + Button por variante primary/destructive/warning/ghost) que aplica la regla de comentario [BR-005] (obligatorio en rechazar/devolver/priorizar; opcional en aceptar) y dispara la mutation correspondiente
- [x] 3.5 Crear `pages/DetallePedidoPage.tsx` (`/designaciones/pedidos/:id`): `DataList` + `ApprovalTimeline` + `AuditLog` (+ `Tabs` Solicitud/Historial/Documentos); role-aware: botonera de revisión solo si `puedeRevisar(pedido, actor)`, resto solo lectura; Loading/Empty/Error/Success
- [x] 3.6 Tests de UI (RTL/user-event): `ModalAccionRevision` exige comentario en rechazo/devolución y dispara la mutation; `TableroRevision`/`PedidoCard` muestran estado y prioridad correctos y solo las columnas/acciones del ámbito y la etapa

## 4. Fase 3 — Routing, nav, gating y role-switching

- [x] 4.1 Extender `features/designaciones/routes.tsx`: `revision` → `TableroRevisionPage` envuelta en `RequireRole` (Coordinador/Secretaría/Decanato/Administración); `pedidos/:id` → `DetallePedidoPage` (cualquier rol, visibilidad por ámbito, acciones gated por etapa)
- [x] 4.2 Extender `app/shell/nav.ts` (`NAV_BY_ROLE`): ítem "Revisión" → `/designaciones/revision` solo para los revisores (sin links muertos, invariante #7)
- [x] 4.3 Extender `shared/auth/dev/mockSession.ts`: `setRolActivo(rol)` / `getRolActivo()` persistidos en `localStorage` (clave `adoc.dev.mockRol`) + `suscribirMockSession(cb)`; `getMockUser()` aplica el rol activo como override acotado a `currentUser.roles`
- [x] 4.4 Volver reactivo `shared/auth/useCurrentUser.ts` con `useSyncExternalStore` suscrito a `suscribirMockSession` (misma firma `(): CurrentUser`); `AppLayout` deja de cablear un `role` local y `onSwitchRole` pasa a `setRolActivo`
- [x] 4.5 Agregar a `shared/auth/dev/mockUsers.ts` el usuario "Demo (todos los roles)" con `roles: [todos]` (el `RoleMenu` existente ya renderiza con `roles.length > 1`); verificar que SCRUM-7 (`MisPedidosPage.test.tsx`) sigue verde tras el cambio de `useCurrentUser`

## 5. Fase 3 — Tests de integración (RTL sobre el store mock)

- [x] 5.1 `happy-path`: JC envía → Coordinador acepta → Secretaría acepta → Decanato acepta → `en_lote`, cambiando el `ActorContexto` (rol activo) entre pasos; verifica que el pedido viaja entre vistas y conserva historial
- [x] 5.2 `devolución`: Coordinador devuelve con comentario → JC corrige/reenvía → vuelve a `en_revision_coordinador`; verifica además que confirmar sin comentario por la UI no muta el store [BR-005]

## 6. Fase 4 — Cierre: documentación y QA (invariantes #11 y #12)

- [x] 6.1 Registrar en `docs/business-rules/designaciones.md` las BR-005/009/011/013/014/015/017 (hoy en "Pendientes (SCRUM-8)") con statement, fuente (decisión de proceso) y mapping a test; regenerar el índice (`pnpm generate-indexes`)
- [x] 6.2 Extender `docs/product/designs/proyecto-docente-design-spec.md` con tablero de revisión + detalle role-aware + cadena de aprobación/timeline; tildar los roles revisores en "Roles que ven esta surface" (invariante #12)
- [x] 6.3 QA: `pnpm --filter frontend test:run` verde (suite completa SCRUM-7 + SCRUM-8), `pnpm --filter frontend lint` y `pnpm --filter frontend build` verdes, `pnpm exec openspec validate flujo-aprobacion-designaciones --strict` verde
- [x] 6.4 Verificar criterios de aceptación SCRUM-8 (§10 del plan): avance/rechazo/devolución/reenvío/prioridad correctos, gating por etapa+ámbito, Administración no aprueba, detalle con timeline+historial, sin `// TODO(backend)` fuera de `api/`, identificadores/comentarios en español
- [x] 6.5 Correr `/evaluate` y registrar la composite score en `docs/quality/scorecard.md`
