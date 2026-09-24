## 1. ui-lib v1.0.3 (repo externo `Ars-Docendi/ui-lib`)

- [x] 1.1 Crear la rama `fix/datepicker-monthyearpicker` desde `main` actualizado
- [x] 1.2 Fix `DatePicker` en `src/styles/components.css`: input a `width: 100%` dentro de `.adoc-date` e indicador nativo (`::-webkit-calendar-picker-indicator`) transparente sobre `.cal-ico` (design D2)
- [ ] 1.3 Verificar el `DatePicker` en Chrome, Firefox y Safari: un único ícono, dentro del campo, y el click sobre el ícono abre el calendario
- [x] 1.4 Red: test que falla eligiendo el mes antes que el año (el mes debe conservarse y emitir `"YYYY-MM"` cuando llega el año). La ui-lib no tiene test runner, así que el test vive en el frontend (`CampoPeriodo.test.tsx`) y ejercita `MonthYearPicker` de punta a punta
- [x] 1.5 Green: crear `src/components/MonthYearPicker/` (componente + `index.ts` + `.stories.tsx`) con el diseño de `SelectorFecha` del Portal y el mes pendiente en estado interno (design D3). Exportarlo en `src/index.ts`
- [x] 1.6 Alinear `version` del `package.json` a `1.0.3`
- [x] 1.7 Abrir PR a `main`, mergear, crear el tag `v1.0.3` y confirmar que el workflow publicó `release/v1.0.3`

## 2. Barra superior

- [x] 2.1 Quitar de `app/shell/TopBar.tsx` el buscador global y los botones de notificaciones y ayuda
- [x] 2.2 Eliminar los íconos que quedan sin uso en `app/shell/icons.tsx` y el CSS de `.adoc-topbar-search` y de los botones que ya no se usan
- [x] 2.3 Test: la barra superior no contiene búsqueda, "Notificaciones" ni "Ayuda", y el menú de usuario sigue ofreciendo "Cerrar sesión"

## 3. Encabezados de página

- [x] 3.1 Quitar la prop `pretitle` de `shared/ui/PageHeader.tsx` y sus usos (Aulas, Tareas, Mis pedidos, Revisión, Detalle, Períodos)
- [x] 3.2 Cambiar el grupo `"DESIGNACIONES"` a `"Designaciones"` en `app/shell/nav.ts` y actualizar `Sidebar.test.tsx`
- [x] 3.3 Ajustar breadcrumbs (sin nivel de grupo) y títulos según la tabla de la spec `encabezado-paginas`: Portal, Aulas, Mis pedidos, Revisión, Períodos, Usuarios, Docentes y Roles
- [x] 3.4 Revisión: quitar el rol del meta
- [x] 3.5 Detalle del pedido: título = tipo de novedad, meta = período, sin número ni cátedra en el encabezado
- [x] 3.6 Nuevo/Editar pedido: reemplazar el `h1` propio de `PedidoForm` por `PageHeader` ("Nuevo pedido" / "Editar pedido", con el subtítulo actual como meta) y cambiar el breadcrumb de edición a "Editar pedido"
- [x] 3.7 Red: test del detalle abierto desde Mis pedidos que espera "Mis pedidos" en el breadcrumb (hoy falla con "Revisión")
- [x] 3.8 Green: Mis pedidos y Revisión pasan `state.origen` al navegar al detalle, y el detalle resuelve el breadcrumb y el link de error por origen, con respaldo por permiso (design D7). Tests de los 4 escenarios de la spec
- [x] 3.9 Actualizar los tests existentes que dependan de los títulos o breadcrumbs anteriores y agregar tests de los escenarios de la spec (título de Revisión, breadcrumb de Períodos, "Mis docentes" para Jefe de Cátedra, encabezado del detalle)

## 4. Períodos de designación

- [x] 4.1 Reemplazar `MenuAccionesPeriodo` en `TablaPeriodos` por un botón ghost "Editar" + botón de eliminar con ícono y `aria-label` (patrón de `TablaMisPedidos`)
- [x] 4.2 Eliminar `MenuAccionesPeriodo.tsx` y los estilos o íconos que queden sin uso
- [x] 4.3 Actualizar `TablaPeriodos.test.tsx`: las acciones son botones visibles y no hay menú kebab

## 5. Dependencia ui-lib y período del Portal (requiere el grupo 1)

- [x] 5.1 Bumpear `@ars-docendi/ui` a `github:Ars-Docendi/ui-lib#release/v1.0.3` en `frontend/package.json` y correr `pnpm install`
- [x] 5.2 Red: test en el Portal que elige el mes antes que el año en Experiencia y espera `"2014-03"` (hoy falla)
- [x] 5.3 Green: reemplazar `SelectorFecha` de `CampoPeriodo.tsx` por `MonthYearPicker` y quitar el CSS `.portal-fecha` que pasa a la librería
- [x] 5.4 Validación "Completá el año" cuando hay mes sin año, con test
- [x] 5.5 Verificar en el navegador el modal de Períodos: un solo ícono de calendario por campo

## 7. Confirmación de borrado unificada

- [x] 7.1 Red: test de `shared/ui/ModalConfirmarEliminar` (texto con el objeto, "Esta acción no se puede deshacer.", Cancelar deshabilitado y Eliminar en carga mientras `eliminando`, error "No se pudo eliminar")
- [x] 7.2 Green: crear `shared/ui/ModalConfirmarEliminar.tsx` (design D8)
- [x] 7.3 Reemplazar `ModalEliminarPeriodo` (Períodos, pasando `eliminando` y el error de la mutación), `ModalEliminarPedido` (Mis pedidos y Detalle) y el `ModalConfirmarEliminar` del Portal (4 secciones). Eliminar los tres componentes viejos
- [x] 7.4 Suite completa, lint y build en verde

## 6. Docs y cierre

- [x] 6.1 Actualizar los design-specs afectados en `docs/product/designs/` (portal, designaciones/proyecto docente, administración de usuarios/docentes y roles) con la convención de encabezados y los cambios de cada pantalla
- [x] 6.2 Correr lint, typecheck y tests del frontend en verde
- [x] 6.3 `openspec validate ajustes-ui-varios --strict`
