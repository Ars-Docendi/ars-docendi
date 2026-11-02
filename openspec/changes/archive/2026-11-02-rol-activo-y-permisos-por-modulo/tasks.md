# Tasks

## 1. Fase 1 — Grilla de roles y permisos por módulo

- [x] 1.1 Backend: `POST /api/administracion/roles` recibe los permisos iniciales en lugar de `rolBaseId` (`ModelosRoles`, `ServicioRoles`); test de integración del alta con permisos y sin permisos.
- [x] 1.2 Crear `features/roles/modulosPermisos.ts`: prefijo → etiqueta y orden, y agrupación de `PermisoRol[]` por módulo (prefijos sin mapa al final).
- [x] 1.3 Componente de permisos por módulo: `<details>` por módulo con `Checkbox` de la guía (`indeterminate`), nombre, contador y chevron; marcado por módulo sin plegar el bloque; deshabilitado sin `roles.gestionar_membresia`.
- [x] 1.4 Grilla `/roles` con `Table`, `FiltroEncabezado`, orden, fila clickeable y `MenuAcciones`; columnas Nombre, Descripción, Ámbito, Tipo, Permisos y Acciones.
- [x] 1.5 Pantalla `EditorRol` en `/roles/nuevo` y `/roles/:rolId`: datos arriba, módulos debajo, "Cancelar"/"Guardar"; alta con todo destildado; edición con metadatos de sistema bloqueados y manejo de conflicto.
- [x] 1.6 Retirar `ListaRoles`, `PanelPermisos`, `ModalNuevoRol`, `ModalEditarRol` y el selector de rol base; estilos en `roles.css` sólo con tokens.
- [x] 1.7 Tests en `roles.test.tsx`: alta con permisos destildados, marcado por módulo con estado intermedio y contador, y edición que guarda datos y permisos.
- [x] 1.9 Activar y desactivar roles personalizados: `POST /roles/{id}/activar|desactivar` (reemplazan `DELETE`), listado con inactivos, columna Estado y acciones Editar/Desactivar/Activar en la grilla.
- [x] 1.10 Buscador de permisos en la sección de permisos del editor.
- [x] 1.8 Actualizar `administracion-roles-permisos-design-spec.md`, `api-contracts-administracion.md` y el artículo de ayuda `administrar-roles-y-permisos` (texto y capturas).

## 2. Fase 2 — Rol activo

- [x] 2.1 Backend: `ServicioSesion` resuelve por asignación (vigente del propio usuario) en lugar de primer rol por nombre; listar asignaciones vigentes con rol y ámbito.
- [x] 2.2 `EventosSesion`: claim `ars:asignacion`; elección automática con una sola asignación; regla de asignación que deja de estar vigente; sin asignación, principal sin permisos.
- [x] 2.3 `EndpointsAutenticacion`: `POST /api/auth/asignacion` (con anti-falsificación, reemite la cookie conservando `ars:inicio`) y campos nuevos en `GET /api/auth/sesion`.
- [x] 2.4 `ICurrentUser.AsignacionActivaId` y `ConsultasIdentity` limitado a la asignación activa cuando consulta por el actor.
- [x] 2.5 Ingreso de desarrollo por asignación: catálogo con una opción por asignación, header `X-Dev-Asignacion-Id`, modal y "Cambiar rol" sobre la sesión de desarrollo.
- [x] 2.6 Tests de integración: una asignación entra directo; varias sin elegir operan con la de por defecto; asignación ajena → `403`; Jefe de Cátedra en dos materias ve sólo el ámbito de la elegida; asignación eliminada vuelve a la de por defecto.
- [x] 2.7 Frontend: modelo de sesión con `asignacion`, `cantidadAsignaciones` y `requiereElegirRol`; modal de elegir rol sobre la aplicación al ingresar con varias asignaciones.
- [x] 2.8 `MenuUsuario`: rol y ámbito activos debajo del nombre; "Cambiar rol" (sólo con más de una asignación) que abre `RoleMenu` en `Modal`; al elegir, invalida la sesión y navega al inicio.
- [x] 2.9 Test de frontend del flujo de selección y cambio de rol.
- [x] 2.10 Docs: `api-contracts.md` (endpoints de auth), artículos de ayuda `ingresar-al-sistema` y `recorrer-la-pantalla`.

## 3. Fase 3 — Depuración de permisos y seguimiento de pedidos

- [x] 3.1 Relevar cada permiso contra sus usos en políticas, guardas y menú, y acordar con producto: retirar `portal.editar`, `reportes.ver` y `sistema.parametrizar`; conservar `aulas.*`, `portal.ver` y `designaciones.aprobar_*`.
- [x] 3.2 Delta de spec: catálogo sin permisos sin uso (`gestionar-permisos-rol`) y seguimiento según permiso (`pedidos-designacion`).
- [x] 3.3 Migración `PermisosSeguimientoPedidosIdentity` (scaffold): crea y asigna `designaciones.ver_seguimiento` (todos los roles con `designaciones.ver` salvo Jefe de Cátedra) y retira los tres permisos, con `Down` que los restaura; constante en `Permisos.cs`.
- [x] 3.4 Backend: `VistaPedido` simplifica estado, historial y etapa sin seguimiento; `PedidoDto.motivoDevolucion`.
- [x] 3.5 Frontend: estados `en_proceso` y `aprobado`, motivo de devolución en detalle y formulario, cadena e historial sólo con seguimiento.
- [x] 3.6 Tests: recorrido HTTP del Jefe (en proceso, devuelto con motivo, aprobado) y detalle con y sin seguimiento; contrato `pedido-detalle.json` regenerado desde Secretaría.
- [x] 3.7 Docs y ayuda: `api-contracts-designaciones.md`, `seguir-el-estado-de-un-pedido`, `editar-un-pedido`.
