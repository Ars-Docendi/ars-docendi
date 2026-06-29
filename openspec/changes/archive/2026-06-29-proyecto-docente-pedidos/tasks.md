## 1. Fase 0 — Harness de tests (habilitante)

- [x] 1.1 Agregar devDeps al `frontend/package.json`: `vitest`, `@testing-library/react`, `@testing-library/jest-dom`, `@testing-library/user-event`, `jsdom`, `@vitest/coverage-v8` (`pnpm --filter frontend add -D ...`)
- [x] 1.2 Configurar el harness en `frontend/vite.config.ts` (`test`: `environment: "jsdom"`, `globals: true`, `setupFiles`) + `src/test/setup.ts` que importe `@testing-library/jest-dom`, limpie `localStorage` y haga `cleanup()` entre tests (el reset del store singleton se engancha en Fase 1)
- [x] 1.3 Agregar scripts `"test": "vitest"` y `"test:run": "vitest run"` a `frontend/package.json`
- [x] 1.4 Escribir un test trivial verde (smoke del setup) y verificar `pnpm --filter frontend test:run` en verde
- [x] 1.5 Registrar en `docs/quality/tech-debt.md` que la deuda "runner frontend TBD" queda resuelta

## 2. Fase 1 — Modelo de dominio (`types.ts`)

- [x] 2.1 Extender `frontend/src/features/designaciones/types.ts` con `Rol` (alias de `Role`), `Novedad`, `Cargo`, `Dedicacion`, `EstadoPedido` (completo, incluido `devuelto`/`en_lote`/`rechazado`/`cancelado`), `Adjunto`, `EventoHistorial`, `PedidoDesignacion`, `DatosEditablesPedido`, `ActorContexto` (modelo del §6.4 del plan)
- [x] 2.2 Declarar el error de dominio `ErrorDominioPedido` para los rechazos de guard de la máquina de estados

## 3. Fase 1 — Máquina de estados pura (TDD ESTRICTO, red-green)

- [x] 3.1 RED: escribir los tests que fallan primero en `maquinaEstados.test.ts` para las transiciones del lado JC y sus guards: `enviaBorradorVaARevisionCoordinador`, `cancelarSoloEnBorrador`, `editarSoloBorradorODevueltoDelPropietario` [BR-008], `accionSobrePedidoTerminalDenegada`, `cadaTransicionRegistraHistorial`
- [x] 3.2 GREEN: implementar `maquinaEstados.ts` (`aplicarAccion(pedido, accion)` puro, sin React/Promise) hasta poner esos tests en verde, validando guards y devolviendo el pedido o lanzando `ErrorDominioPedido`
- [x] 3.3 Diseñar la firma/estructura de `aplicarAccion` para que SCRUM-8 agregue `aceptar`/`rechazar`/`devolver`/`reenviar`/`priorizar` sin reescribir lo existente (union `AccionPedido` + switch exhaustivo con `never`)
- [x] 3.4 Verificar `pnpm --filter frontend test:run` en verde para la suite de `maquinaEstados` (12 tests)

## 4. Fase 1 — Seam de datos mock (store + api + seed)

- [x] 4.1 Crear `api/pedidosStore.ts`: singleton en memoria hidratado desde `localStorage` (clave `adoc.mock.pedidos`), persistido en cada escritura; lectura/escritura síncrona con `structuredClone` (no lo consumen los componentes); `reiniciarStorePedidos()` para tests
- [x] 4.2 Crear `api/pedidosSeed.ts`: datos iniciales (docentes precargados del período anterior como "Sin novedad" + ejemplos en borrador/Alta/enviado/devuelto)
- [x] 4.3 Crear `api/pedidosApi.ts`: funciones async (`Promise` + `await demora(250)`) sobre el store, delegando transiciones a `maquinaEstados.ts`: `listarMisPedidos`, `obtenerPedido`, `crearPedido`, `editarPedido`, `enviarPedido`, `cancelarPedido` (+ `api/contextoActor.ts` seam del ámbito)
- [x] 4.4 Anotar cada función de `pedidosApi.ts` con su `// TODO(backend): <endpoint real> — SCRUM-7. Mock actual: ...` (formato fijo del §7 del plan); verificado: no hay `// TODO(backend)` fuera de `api/`

## 5. Fase 1 — Hooks React Query + badge de estado

- [x] 5.1 Crear `hooks/usePedidos.ts`: `useMisPedidos(actor)` y `usePedido(id)` con `useQuery` y queryKeys namespaced (`["pedidos", ...]`); + `hooks/useActorContexto.ts`
- [x] 5.2 Crear `hooks/useAccionesPedido.ts`: mutations `useCrearPedido`, `useEditarPedido`, `useEnviarPedido`, `useCancelarPedido` que invalidan `["pedidos"]` en `onSuccess`
- [x] 5.3 Crear `components/EstadoPedidoBadge.tsx`: wrapper sobre `StatusBadge` que mapea `EstadoPedido` → `StatusKind` según §6.6 (+ badge extra `prioritario`)

## 6. Fase 2 — Validaciones de form (TDD ESTRICTO, red-green)

- [x] 6.1 RED: escribir los tests que fallan primero del validador puro `pedidoValidacion.ts`: `unPedidoPorDocentePorPeriodo` [BR-001], `altaExigeCvYDniFrenteYDorso` [BR-002], `bajaExigeJustificativo` [BR-003], `cambioExigeJustificacion` [BR-004]
- [x] 6.2 GREEN: implementar `validarPedido` puro (sin React) hasta poner esos tests en verde
- [x] 6.3 Verificar `pnpm --filter frontend test:run` en verde para la suite de validaciones (9 tests)

## 7. Fase 2 — UI: "Mis pedidos"

- [x] 7.1 Crear `components/TablaMisPedidos.tsx`: lista presentacional (docente, materia, novedad, `EstadoPedidoBadge`, flag prioritario) + acciones por estado (Editar, Enviar, Cancelar)
- [x] 7.2 Crear `pages/MisPedidosPage.tsx`: contenedor que consume `useMisPedidos` y renderiza explícitamente Loading / Empty / Error / Success
- [x] 7.3 Cablear las acciones a las mutations (Enviar → `useEnviarPedido`, Cancelar → `useCancelarPedido` con modal de confirmación) reflejando el cambio en la UI (sin fake UI, invariante #7)

## 8. Fase 2 — UI: Form de pedido

- [x] 8.1 Crear `components/PedidoForm.tsx`: campos comunes con `Field`+`Input`/`Select`/`Radio` y secciones condicionales por novedad (Alta/Cambio → cargo/dedicación solicitados; adjuntos con `FileUpload`; justificación con `Textarea`); cargo/dedicación actual read-only
- [x] 8.2 Conectar el validador (tarea 6) al form: error inline con `InlineAlert`/`Field error`, submit inválido bloqueado
- [x] 8.3 Crear `pages/PedidoFormPage.tsx`: modo alta (`/pedidos/nuevo`) y edición (`/pedidos/:id/editar`), consumiendo `useCrearPedido`/`useEditarPedido` y `usePedido`; guard `puedeEditarPedido` para navegación directa a un pedido no editable
- [x] 8.4 Tests de UI (RTL/user-event) de `PedidoForm`: muestra/oculta secciones según novedad; bloquea submit inválido (6 tests)

## 9. Fase 2 — Routing, nav, gating y personas mock

- [x] 9.1 Extender `routes.tsx`: `mis-pedidos` → `MisPedidosPage`, `pedidos/nuevo` y `pedidos/:id/editar` → `PedidoFormPage`, envueltas en `RequireRole` para el Jefe de Cátedra
- [x] 9.2 Extender `app/shell/nav.ts` (`NAV_BY_ROLE`): ítem "Mis pedidos" solo para el JC (sin links muertos, invariante #7)
- [x] 9.3 `mockUsers.ts`: las 6 personas single-rol realistas YA existen (con `carrera_id` para el Coordinador). El usuario "Demo (todos los roles)" + role-switching se DIFIERE a SCRUM-8: en SCRUM-7 cambiar a un rol revisor no tiene superficie construida (el tablero/detalle son SCRUM-8) ⇒ sería UI a medias (roza invariante #7). Se construye junto al circuito de revisión.
- [x] 9.4 Derivar `ActorContexto` desde `useCurrentUser()` + el mock de ámbito (`api/contextoActor.ts`) vía `hooks/useActorContexto.ts`; consumido por las páginas para alimentar la capa `api/`

## 10. Fase cierre — Documentación (invariantes #11 y #12)

- [x] 10.1 Crear `docs/business-rules/designaciones.md` y registrar BR-designaciones-001..004 + 008 (los de SCRUM-7) con statement, fuente y mapping a test; BR de revisión (005/009/011/013/014/015/017) listadas como pendientes de SCRUM-8 (no se registran sin test); índice regenerado (`pnpm generate-indexes`)
- [x] 10.2 Crear `docs/product/designs/proyecto-docente-design-spec.md` desde `_design-spec-template.md` (Mis pedidos + Form de pedido)

## 11. Cierre — QA y criterios de aceptación

- [x] 11.1 `pnpm --filter frontend test:run` verde (34 tests: BR-001..004 + 008, máquina de estados, validador, PedidoForm UI, seam api, MisPedidosPage integración)
- [x] 11.2 `pnpm --filter frontend lint` y `pnpm --filter frontend build` verdes; `openspec validate --strict` verde
- [x] 11.3 Criterios de aceptación SCRUM-7 (§10): persistencia entre recarga (test seam), Loading/Empty/Error/Success en Mis pedidos y form, sin `// TODO(backend)` fuera de `api/` (verificado), identificadores/comentarios en español
- [x] 11.4 Correr `/evaluate` y registrar la composite score en `docs/quality/scorecard.md` (composite 4.25 — pass: ningún criterio < 3, Func ≥ 4)
