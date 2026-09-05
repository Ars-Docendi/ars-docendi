## 1. Shell — íconos y sidebar

- [x] 1.1 Agregar `roles` y `membresiaRoles` al objeto `navIcons` en `frontend/src/app/shell/icons.tsx`
- [x] 1.2 Agregar ítems "Roles" y "Membresía Roles" al grupo "Configuración" de Secretaría y Administración en `frontend/src/app/shell/nav.ts`

## 2. Mock store de roles

- [x] 2.1 Crear `frontend/src/features/roles/mock/mockStore.ts` con tipo `RolMock` (`id`, `nombre`, `descripcion`) y array inicial con 5-6 roles de ejemplo
- [x] 2.2 Exportar funciones puras: `agregarRol`, `editarRol`
- [x] 2.3 Exportar `normalizarTexto` (reutilizar lógica de usuarios — o importar si se mueve a shared)

## 3. Mock store de membresía roles

- [x] 3.1 Crear `frontend/src/features/membresia-roles/mock/mockStore.ts` con tipo `PermisoMock` (`id`, `nombre`) y array inicial con permisos de ejemplo del sistema
- [x] 3.2 Exportar mapa `membresiasIniciales: Record<string, string[]>` (rolId → permisoId[]) con datos de ejemplo
- [x] 3.3 Exportar función pura `actualizarMembresia(mapa, rolId, permisosIds[])` que devuelve el mapa actualizado

## 4. Feature routing — roles

- [x] 4.1 Crear `frontend/src/features/roles/routes.tsx` envuelta en `RequireRole` con `allowedRoles={["Secretaría", "Administración"]}`
- [x] 4.2 Registrar `rolesRoutes` en `frontend/src/app/router.tsx`

## 5. Feature routing — membresía roles

- [x] 5.1 Crear `frontend/src/features/membresia-roles/routes.tsx` envuelta en `RequireRole` con `allowedRoles={["Secretaría", "Administración"]}`
- [x] 5.2 Registrar `membresiaRolesRoutes` en `frontend/src/app/router.tsx`

## 6. Pantalla Roles — listado y búsqueda

- [x] 6.1 Crear `frontend/src/features/roles/pages/IndexPage.tsx` con estado local de roles, búsqueda y control de modales
- [x] 6.2 Crear `frontend/src/features/roles/components/TablaRoles.tsx` — columnas: Nombre | Descripción | Acciones. Botón "Editar" ghost por fila. Muestra estado vacío cuando no hay resultados de búsqueda.
- [x] 6.3 Crear `frontend/src/features/roles/components/BuscadorRoles.tsx` — campo `Input` que filtra en tiempo real, insensible a tildes

## 7. Pantalla Roles — crear rol

- [x] 7.1 Crear `frontend/src/features/roles/components/ModalNuevoRol.tsx` — campos Nombre (obligatorio, único) y Descripción (obligatoria). Checkbox "Usar un rol existente como base" que habilita selector de roles. Botones Cancelar / Guardar.
- [x] 7.2 Implementar validación de nombre vacío y nombre duplicado con error inline via `InlineAlert` de ui-lib
- [x] 7.3 Implementar lógica de herencia: al confirmar con rol base seleccionado, copiar los permisos del rol base al nuevo rol en el store de membresías

## 8. Pantalla Roles — editar rol

- [x] 8.1 Crear `frontend/src/features/roles/components/ModalEditarRol.tsx` — mismos campos que alta, pre-poblados. Validación de nombre duplicado excluye el propio rol. Botones Cancelar / Guardar.

## 9. Pantalla Membresía Roles — layout split

- [x] 9.1 Crear `frontend/src/features/membresia-roles/pages/IndexPage.tsx` con layout de dos paneles (izquierdo: lista de roles; derecho: permisos del rol seleccionado o placeholder)
- [x] 9.2 Crear `frontend/src/features/membresia-roles/components/ListaRoles.tsx` — lista de roles con buscador, ítem clickeable que resalta el seleccionado

## 10. Pantalla Membresía Roles — gestión de permisos

- [x] 10.1 Crear `frontend/src/features/membresia-roles/components/PanelPermisos.tsx` — lista todos los permisos con `Checkbox` de ui-lib. Estado local de checkboxes inicializado desde el store. Botón "Guardar cambios" que llama `actualizarMembresia`.
- [x] 10.2 Implementar descarte de cambios al cambiar de rol sin guardar: al seleccionar un rol diferente, el estado local de checkboxes se reinicia desde el store sin preguntar al usuario

## 11. Verificación

- [x] 11.1 Secretaría y Administración ven "Roles" y "Membresía Roles" en el sidebar
- [x] 11.2 Otro rol es redirigido a `/` al intentar acceder a `/roles` o `/membresia-roles`
- [x] 11.3 Tabla de roles muestra todos los roles; buscador filtra en tiempo real
- [x] 11.4 Crear rol: modal abre, campos obligatorios validados, rol aparece en tabla
- [x] 11.5 Crear rol con base: nuevo rol hereda los permisos del rol seleccionado
- [x] 11.6 Editar rol: modal pre-poblado, cambios reflejados en tabla; nombre duplicado bloqueado
- [x] 11.7 Membresía Roles: lista de roles se carga, buscador filtra, clic en rol muestra sus permisos
- [x] 11.8 Guardar permisos: cambios se persisten en el store; cambiar de rol sin guardar descarta cambios
