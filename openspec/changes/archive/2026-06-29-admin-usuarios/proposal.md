## Why

Actualmente no existe forma de que Secretaría Académica ni Administrativos gestionen los usuarios del sistema desde la interfaz web: altas, bajas y reasignación de roles se harían fuera del sistema. Esta página centraliza esa gestión para que ambos perfiles puedan hacerlo sin depender de acceso directo a la base de datos.

## What Changes

- Nueva feature `usuarios` en `frontend/src/features/usuarios/` con página de listado y acciones inline.
- Nuevo ícono `usuarios` en `frontend/src/app/shell/icons.tsx`.
- Entrada "Usuarios" agregada al sidebar de Secretaría y Administración en `frontend/src/app/shell/nav.ts`.
- Nueva ruta `/usuarios` registrada en `frontend/src/app/router.tsx`.
- Store mock local (`mockStore.ts`) con modelo de persona completo: `nombre`, `apellido`, `documento`, `legajo`, `cuil`, `fecha_nacimiento`, `telefono`, `upn`, `is_active`, `roles: RolSistema[]`. Helper `nombreCompleto(u)` exportado para formatear "Apellido, Nombre".
- Tabla con columnas: Apellido y Nombre | Documento | Legajo | UPN/Email | Roles | Estado | Acciones. Scroll horizontal automático cuando el viewport lo requiere.
- Barra de filtros en dos filas: fija (nombre/apellido, email) + opcionales añadibles (documento, legajo, rol, estado). Los selectores de filtro se dimensionan según su opción más larga (`width: auto`).
- Estado activo/inactivo renderizado con `StatusBadge` de ui-lib (`kind="aprobado"` para Activo, `kind="rechazado"` para Inactivo).
- Botones de acción en tabla `variant="ghost"`: "Editar" siempre; "Desactivar" o "Activar" según estado.
- Modal de alta con grilla de 2 columnas para los campos de persona. Campos obligatorios: nombre, apellido, documento, legajo, fecha de nacimiento, UPN y al menos un rol. CUIL y teléfono opcionales.
- Modales con separación visual entre campos y botones de acción (`justify-content: space-between`).
- Confirmación antes de activar o desactivar un usuario para prevenir clicks no intencionales.

## Capabilities

### New Capabilities

- `listar-usuarios`: Tabla con datos de persona (Apellido y Nombre, Documento, Legajo), UPN, roles (múltiples) y estado (StatusBadge verde/rojo). Scroll horizontal. Barra de filtros con nombre/email fijos y documento/legajo/rol/estado opcionales.
- `crear-usuario`: Formulario en modal con grilla 2 columnas. Campos de persona: nombre, apellido, DNI, legajo, CUIL, fecha de nacimiento, teléfono, UPN, roles (checkboxes). Validación de campos obligatorios y UPN duplicada.
- `desactivar-usuario`: Botón ghost en tabla → modal de confirmación → `is_active = false`.
- `activar-usuario`: Botón ghost "Activar" visible en filas inactivas → modal de confirmación → `is_active = true`.
- `modificar-rol-usuario`: Modal "Editar usuario" (botón "Editar") con checkboxes de roles y todos los campos de persona pre-cargados.

### Modified Capabilities

_(ninguna — es todo nuevo)_

## Impact

- `frontend/src/app/shell/icons.tsx` — se agrega `usuarios` al objeto `navIcons`.
- `frontend/src/app/shell/nav.ts` — se agrega ítem a las secciones de Secretaría y Administración.
- `frontend/src/app/router.tsx` — se agrega `usuariosRoutes`.
- No se toca ningún archivo de backend. Sin llamadas HTTP reales; todo es estado local mock.
