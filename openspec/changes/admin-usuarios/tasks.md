## 1. Shell — ícono y sidebar

- [x] 1.1 Agregar `usuarios` al objeto `navIcons` en `frontend/src/app/shell/icons.tsx`
- [x] 1.2 Agregar ítem `Usuarios` al grupo "Configuración" de Secretaría y Administración en `nav.ts`

## 2. Guard de rol

- [x] 2.1 Crear `frontend/src/shared/auth/RequireRole.tsx`

## 3. Mock store de usuarios

- [x] 3.1 Crear `frontend/src/features/usuarios/mock/mockStore.ts` con modelo de persona completo: `nombre`, `apellido`, `documento`, `legajo`, `cuil`, `fecha_nacimiento`, `telefono`, `upn`, `is_active`, `roles: RolSistema[]`
- [x] 3.2 Exportar helper `nombreCompleto(u)` → `"Apellido, Nombre"`
- [x] 3.3 Exportar funciones puras: `agregarUsuario`, `desactivarUsuario`, `activarUsuario`, `editarUsuario`
- [x] 3.4 Exportar `normalizarTexto(s)` — búsqueda insensible a tildes vía NFD + strip combining marks
- [x] 3.5 Datos mock con 8 usuarios, legajos de 4 dígitos numéricos (ej: `"0421"`)

## 4. Feature routing

- [x] 4.1 Crear `frontend/src/features/usuarios/routes.tsx` envuelta en `RequireRole`
- [x] 4.2 Registrar `usuariosRoutes` en `frontend/src/app/router.tsx`

## 5. Página principal — listado

- [x] 5.1 Crear `frontend/src/features/usuarios/pages/IndexPage.tsx`
- [x] 5.2 Crear `frontend/src/features/usuarios/components/TablaUsuarios.tsx` — columnas: Apellido y Nombre | Documento | Legajo | UPN/Email | Roles | Estado | Acciones. Botones ghost. Scroll horizontal automático.

## 6. Modal — crear usuario

- [x] 6.1 Crear `frontend/src/features/usuarios/components/ModalNuevoUsuario.tsx` — grilla 2 columnas para campos de persona. Campos obligatorios: nombre, apellido, documento, legajo, fecha de nacimiento, UPN, al menos un rol. Opcionales: CUIL, teléfono. Usa `DatePicker` para fecha. Botones con `space-between`; Cancelar con fondo rojo.

## 7. Modales — cambio de estado

- [x] 7.1 Crear `frontend/src/features/usuarios/components/ModalConfirmarDesactivacion.tsx`
- [x] 7.2 Crear `frontend/src/features/usuarios/components/ModalConfirmarActivacion.tsx`

## 8. Modal — editar usuario

- [x] 8.1 Reescribir `frontend/src/features/usuarios/components/ModalEditarRol.tsx` como `ModalEditarUsuario` — grilla 2 columnas, todos los campos de persona pre-poblados, misma validación que alta. UPN duplicada excluye la propia UPN del usuario editado. Botón en tabla: `"Editar"` (ghost).
- [x] 8.2 Sincronización de estado al cambiar de usuario: reemplazar el `useEffect` que llamaba `setCampos` / `setEnviado` por el patrón de comparación durante el render (`if (usuario !== prevUsuario)`). Evita renders en cascada y satisface la regla ESLint `react-hooks/set-state-in-effect`.

## 9. Filtros

- [x] 9.A Crear `frontend/src/features/usuarios/components/FiltrosUsuarios.tsx` — dos filas: fija (Apellido + Nombre + Documento + selector) y condicional (opcionales: Legajo, Mail/UPN, Rol, Estado). Selectores con `width: auto`.
- [x] 9.B Filtros de Apellido y Nombre son campos separados, búsqueda insensible a tildes via `normalizarTexto`
- [x] 9.C Integrar en `IndexPage.tsx` con `useMemo`

## 10. Componentes de ui-lib utilizados

- `Table`, `Table.Root`, `Table.Head`, `Table.Body`, `Table.Row`, `Table.HeaderCell`, `Table.Cell`
- `StatusBadge` (kind aprobado/rechazado para estado)
- `Button` (variant ghost, primary, secondary)
- `Modal`, `Field`, `Input`, `Select`, `Checkbox`, `DatePicker`, `InlineAlert`

## 11. Verificación

- [x] 11.1 Secretaría ve "Usuarios" en sidebar y la página carga
- [x] 11.2 Otro rol es redirigido a `/`
- [x] 11.3 Alta de usuario: todos los campos de persona correctos → aparece en tabla
- [x] 11.4 UPN duplicada: error inline, no se crea
- [x] 11.5 Desactivar: modal confirma → badge rojo "Inactivo", botón cambia a "Activar"
- [x] 11.5b Activar: modal confirma → badge verde "Activo", botón cambia a "Desactivar"
- [x] 11.6 Editar usuario: modal pre-poblado, campos editables → tabla actualizada
- [x] 11.7 Filtros fijos (apellido, nombre, documento): buscan correctamente e ignoran tildes
- [x] 11.8 Filtros opcionales (legajo, mail, rol, estado): se añaden/quitan con selector y ×
- [x] 11.9 Selectores muestran su ancho según la opción más larga
