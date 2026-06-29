## Why

Actualmente no existe una pantalla de gestión de docentes en el sistema: los datos de cada docente (cargo, materias asignadas, información personal) no pueden administrarse desde la interfaz web. Esta página centraliza la gestión para que Secretaría Académica y Administración puedan operar sin acceso directo a la base de datos, siguiendo el mismo patrón ya validado en la pantalla de administración de usuarios.

## What Changes

- Nueva feature `docentes` en `frontend/src/features/docentes/` con página de listado y acciones inline.
- Nuevo ícono `docentes` en `frontend/src/app/shell/icons.tsx`.
- Entrada "Docentes" agregada al sidebar de Secretaría y Administración en `frontend/src/app/shell/nav.ts`.
- Nueva ruta `/docentes` registrada en `frontend/src/app/router.tsx`.
- Store mock local (`mockStore.ts`) con modelo de docente completo: `nombre`, `apellido`, `documento`, `legajo`, `cuil`, `fecha_nacimiento`, `telefono`, `upn`, `rol: RolDocente`, `asignaciones: AsignacionMateria[]`, `is_active`. Helper `nombreCompleto(d)` exportado para formatear "Apellido, Nombre".
- `RolDocente` = `"Docente" | "Jefe de Cátedra"` — el rol de sistema que determina permisos en la plataforma (distinto del cargo académico por materia).
- `AsignacionMateria` = `{ materia: MateriaMock; cargo: CargoDocente; horas: number }` — cada docente puede tener cargo y carga horaria distintos en cada materia asignada.
- Catálogo de materias mock (`MATERIAS_CATALOGO`) con `codigo: string` (5 dígitos, ej: "03500") y `nombre: string`. Catálogo de cargos (`CARGOS_DOCENTES`) con 7 opciones institucionales y mapa de abreviaciones (`ABREV_CARGOS`).
- Tabla con columnas: Apellido y Nombre | Documento | Legajo | Rol | Asignaciones (badge por asignación: código + cargo abreviado + horas) | Estado | Acciones. Scroll horizontal automático.
- Barra de filtros en dos filas: fija (Apellido, Nombre, Documento) + opcionales añadibles (Código de materia, Materia, Cargo, Rol, Estado).
- Estado activo/inactivo renderizado con `StatusBadge` de ui-lib (`kind="aprobado"` para Activo, `kind="rechazado"` para Inactivo).
- Botones de acción en tabla `variant="ghost"`: "Editar" siempre; "Desactivar" o "Activar" según estado.
- Modal de alta con dos modos (nueva persona / persona del sistema), selector de rol (`RolDocente`) y componente `AsignacionesSelector` (filas añadibles: una por asignación materia+cargo+horas). Campos obligatorios: datos personales, rol, al menos una asignación completa (materia, cargo y horas).
- Confirmación antes de activar o desactivar para prevenir clicks no intencionales.
- Sin llamadas HTTP reales; todo es estado local mock (mismo patrón que admin-usuarios).

## Capabilities

### New Capabilities

- `listar-docentes`: Tabla con datos del docente (Apellido y Nombre, Documento, Legajo, **Rol**), asignaciones materia+cargo+horas (badge por asignación: código + cargo abreviado + horas) y estado (StatusBadge verde/rojo). Scroll horizontal. Barra de filtros con Apellido, Nombre y Documento fijos y Código de materia, Materia, Cargo, **Rol** y Estado opcionales.
- `crear-docente`: Formulario en modal. Dos modos: nueva persona o persona del sistema. Campos personales + **Rol** (Select: Docente / Jefe de Cátedra) + asignaciones via `AsignacionesSelector` (filas: una por materia+cargo+horas). Validación de campos obligatorios y UPN duplicada.
- `desactivar-docente`: Botón ghost en tabla → modal de confirmación → `is_active = false`.
- `activar-docente`: Botón ghost "Activar" visible en filas inactivas → modal de confirmación → `is_active = true`.
- `editar-docente`: Modal "Editar" con todos los campos pre-cargados (datos personales + rol + asignaciones). Misma validación que alta.

### Modified Capabilities

_(ninguna — es todo nuevo)_

## Impact

- `frontend/src/app/shell/icons.tsx` — se agrega `docentes` al objeto `navIcons`.
- `frontend/src/app/shell/nav.ts` — se agrega ítem a las secciones de Secretaría y Administración.
- `frontend/src/app/router.tsx` — se agrega `docentesRoutes`.
- No se toca ningún archivo de backend. Sin llamadas HTTP reales; todo es estado local mock.
- No hay cambios en el grafo de dependencias de módulos backend.
