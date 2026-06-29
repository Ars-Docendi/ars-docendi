## Why

Actualmente la gestión de roles del sistema y la asignación de permisos a esos roles no existe como funcionalidad accesible desde la interfaz web: cualquier cambio requiere acceso directo a la base de datos. Esta feature incorpora dos pantallas de administración que permiten a Secretaría y Administración definir roles y controlar qué permisos tiene cada uno, completando el módulo de identidad del sistema.

## What Changes

- Nueva feature `roles` en `frontend/src/features/roles/` con página de listado, búsqueda, creación (con herencia opcional) y edición de roles.
- Nueva feature `membresia-roles` en `frontend/src/features/membresia-roles/` con página de listado de roles y panel de asignación de permisos por rol.
- Nuevos íconos `roles` y `membresiaRoles` en `frontend/src/app/shell/icons.tsx`.
- Entradas "Roles" y "Membresía Roles" agregadas al grupo "Configuración" del sidebar de Secretaría y Administración en `frontend/src/app/shell/nav.ts`.
- Nuevas rutas `/roles` y `/membresia-roles` registradas en `frontend/src/app/router.tsx`, ambas protegidas con `RequireRole` para los mismos roles que `/usuarios`.
- Mock stores locales para roles y permisos (misma estrategia que `usuarios/mock/mockStore.ts`): datos en memoria, funciones puras de mutación, sin llamadas HTTP.

## Capabilities

### New Capabilities

- `listar-roles`: Tabla de roles con columnas Nombre y Descripción. Buscador en tiempo real que filtra por nombre y descripción. Botones de acción "Editar" por fila.
- `crear-rol`: Modal para crear un nuevo rol con campos Nombre y Descripción. Opción de "Usar un rol existente como base" (checkbox que habilita un selector de roles): el nuevo rol hereda los permisos del rol base.
- `editar-rol`: Modal para editar Nombre y Descripción de un rol existente.
- `listar-membresia-roles`: Panel izquierdo con lista de roles y buscador. Al hacer click en un rol se activa y muestra su panel de permisos.
- `gestionar-permisos-rol`: Panel derecho (visible al seleccionar un rol) que lista todos los permisos del sistema con un checkbox por permiso. El operador marca/desmarca para otorgar o revocar el permiso al rol. Botón "Guardar cambios" confirma la operación.

### Modified Capabilities

_(ninguna — es todo nuevo)_

## Impact

- `frontend/src/app/shell/icons.tsx` — se agregan `roles` y `membresiaRoles` al objeto `navIcons`.
- `frontend/src/app/shell/nav.ts` — se agregan los ítems "Roles" y "Membresía Roles" al grupo "Configuración" de Secretaría y Administración.
- `frontend/src/app/router.tsx` — se registran `rolesRoutes` y `membresiaRolesRoutes`.
- No se toca el backend. Sin llamadas HTTP reales; todo es estado local mock.
- El guard `RequireRole` ya existe (`shared/auth/RequireRole.tsx`); se reutiliza sin modificaciones.
