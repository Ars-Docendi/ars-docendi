## Why

Las pantallas de Roles y Membresía de Roles están separadas y la navegación y
la sesión frontend dependen de una lista fija de roles institucionales. Como
consecuencia, un rol personalizado puede existir en `identity`, tener permisos
válidos y ser aceptado por el backend, pero el frontend lo considera una sesión
inválida y no puede construir su sidenav.

La administración necesita una única superficie para definir roles y permisos,
y los permisos efectivos deben ser la fuente de verdad de las pantallas y
acciones disponibles, tanto para roles personalizados como institucionales.

## What Changes

- Unificar `/roles` y `/membresia-roles` en una única pantalla con el layout de membresía: lista de roles a la izquierda y datos, permisos y acciones del rol seleccionado a la derecha.
- Mantener `/roles` como ruta canónica y redirigir `/membresia-roles` para conservar enlaces existentes.
- Permitir crear un rol desde la pantalla unificada, heredar opcionalmente permisos de un rol base y asignar o modificar sus permisos sin cambiar de pantalla.
- Permitir renombrar roles personalizados conservando su código estable.
- Permitir eliminar roles personalizados mediante baja lógica, preservando asignaciones históricas y auditoría; los roles dados de baja no serán seleccionables, efectivos en sesión ni visibles en la navegación operativa.
- Impedir en backend que los roles de sistema sean renombrados, eliminados, cambiados de código, ámbito o marca `es_sistema`.
- Mantener modificables los permisos de roles de sistema y personalizados, con actualización de la sesión frontend cuando corresponda.
- Reemplazar la validación frontend basada en nombres de rol por una sesión que conserve el código, nombre y permisos efectivos del rol recibido desde backend.
- Construir la sidenav y los guards frontend a partir de permisos, sin asumir que el rol pertenece a un conjunto cerrado.
- Completar los permisos explícitos de acceso a pantallas que hoy dependen de roles (`designaciones.revisar` y `docentes.ver`), manteniendo las reglas de etapa y ámbito separadas.
- Mantener las restricciones de dominio del circuito de Designaciones: los roles personalizados no se convierten por tener permisos en roles institucionales de la máquina de estados.
- Actualizar los contratos, las especificaciones, el diseño administrativo y las pruebas de backend/frontend afectados.

## Capabilities

### New Capabilities

- `eliminar-rol`: baja lógica de roles personalizados, protección de roles de sistema y comportamiento de roles dados de baja.
- `navegacion-por-permisos`: sesión frontend, sidenav y guards basados en permisos efectivos para todos los roles.

### Modified Capabilities

- `administracion-identidad-api`: agregar la operación de baja de roles personalizados y explicitar qué propiedades de los roles de sistema son inmutables y cuáles permiten cambios de permisos.
- `listar-roles`: reemplazar la pantalla tabular independiente por la superficie unificada y autorizar la consulta mediante `roles.ver`.
- `crear-rol`: crear roles desde la pantalla unificada y continuar la configuración de permisos en el mismo flujo.
- `editar-rol`: permitir renombrar roles personalizados y rechazar la mutación de identidad de roles de sistema.
- `gestionar-permisos-rol`: gestionar permisos desde la pantalla unificada para cualquier rol visible y refrescar la autorización frontend.
- `listar-membresia-roles`: retirar la pantalla independiente y conservar la ruta anterior como redirección a `/roles`.
- `sesion-desarrollo-sembrada`: aceptar roles personalizados activos y transportar sus nombres, códigos, ámbitos y permisos efectivos al selector/sesión de desarrollo.
- `listar-usuarios`: mostrar la pantalla y su entrada de navegación según `usuarios.ver`, no según nombres de rol.
- `listar-docentes`: reemplazar el gating frontend por permisos, conservando el alcance de ámbito autoritativo del backend.
- `gestion-periodos`: reemplazar el acceso frontend fijo de Secretaría por el permiso correspondiente.
- `navegacion-designaciones`: decidir la presencia de cada entrada por permisos, manteniendo las reglas de flujo institucional en backend.
- `aprobacion-pedidos-designacion`: cambiar el gating de la superficie de revisión a permisos sin alterar las reglas de etapa y rol del dominio.

## Impact

- **Frontend:** `features/roles`, la eliminación/consolidación de `features/membresia-roles`, `shared/auth`, `app/shell/nav.ts`, `Sidebar`, guards de rutas y pruebas de navegación. `CurrentUser` deberá exponer permisos y admitir nombres de rol dinámicos.
- **Backend:** `ArsDocendi.Shared.Identity.Administracion` y `ArsDocendi.Host.Api/RolesController.cs` para la baja lógica y las protecciones de roles de sistema; `Identity.Desarrollo` y su handler para publicar y resolver permisos efectivos.
- **API:** se agrega la operación administrativa de baja de roles personalizados, se amplía el DTO no productivo de identidades de desarrollo con los permisos necesarios para la sesión frontend y se agregan al catálogo los permisos explícitos de revisión de Designaciones y consulta de Docentes. Se actualizarán `docs/architecture/api-contracts-administracion.md` y los artefactos SQL versionados del catálogo de permisos.
- **Persistencia:** no se agrega una tabla ni un esquema nuevo; se reutiliza `roles.is_active`, el `version` de concurrencia y las restricciones existentes de `user_roles`. No se eliminarán físicamente roles con historial.
- **Dependencias:** no cambia el grafo de módulos. Identity continúa en `ArsDocendi.Shared`; los módulos de negocio siguen leyendo identidad mediante `IConsultasIdentity`. Las reglas específicas de Designaciones permanecen en ese módulo.
- **UX/documentación:** se agregará o actualizará el design spec de Roles y permisos para documentar el layout unificado, permisos de lectura/escritura, estados de baja, sesión dinámica y redirección de la ruta anterior.
- **Rollback:** desplegar frontend y backend compatibles. Si la sesión dinámica o la baja lógica requiere reversión, volver a la pareja anterior sin borrar filas de `roles`, `user_roles`, `rol_permisos` ni auditoría; una baja lógica puede revertirse operativamente sólo mediante una futura acción explícita de reactivación, que no forma parte de este cambio.
