## 1. Catálogo y autorización de pantallas

- [x] 1.1 Agregar `docentes.ver` y completar `designaciones.revisar` en `Permisos.Todos`, el catálogo SQL y los defaults institucionales; verificar mediante la migración y una prueba de integración que ambos permisos aparecen en `GET /api/administracion/permisos` y quedan asignados a los roles institucionales correspondientes.
- [x] 1.2 Cambiar las políticas de Revisión y Docentes para exigir sus permisos persistidos, conservando los filtros de ámbito y las reglas de etapa del dominio; verificar que un rol personalizado con permiso puede consultar y que un usuario sin permiso recibe `403`.

## 2. Roles y permisos en backend

- [x] 2.1 Agregar primero pruebas de regresión para renombrar un rol personalizado, conservar su código, modificar permisos de un rol de sistema, rechazar cualquier mutación de identidad de sistema, eliminar un rol personalizado y rechazar versión obsoleta; verificar que las pruebas fallan antes de la implementación.
- [x] 2.2 Extender los modelos administrativos, repositorio, `ServicioRoles` y `RolesController` con `DELETE /api/administracion/roles/{id}` y versión esperada; implementar baja lógica con `is_active = false`, conservación de relaciones y rechazo de roles de sistema; verificar con `AdministracionRolesTests` la respuesta y el estado persistido.
- [x] 2.3 Hacer que listar, crear, editar, usar como rol base y asignar membresías operen sólo sobre roles activos; permitir permisos en cualquier rol activo y bloquear metadatos/ciclo de vida de roles de sistema; verificar escenarios de rol inactivo, nombre duplicado y permisos de sistema.
- [x] 2.4 Mantener la concurrencia de edición, baja y membresía con `Version`, transacción y el manejador de errores existente; verificar `409` sin cambios parciales cuando otra sesión modificó el rol.

## 3. Sesión de desarrollo dinámica

- [x] 3.1 Ampliar `RolDesarrolloDto` y su mapeo para devolver código, nombre, ámbitos y permisos efectivos de cada rol activo, incluidos los personalizados; verificar el JSON de `/api/desarrollo/identidades` y que no aparecen roles inactivos.
- [x] 3.2 Verificar que el handler de desarrollo emite las claims de permisos recalculadas para el código seleccionado y rechaza roles inexistentes, inactivos o no asignados; agregar una prueba de autenticación para el caso que antes terminaba en “No se pudo validar la sesión”.

## 4. Sesión, navegación y guards frontend

- [x] 4.1 Reemplazar la unión cerrada de nombres en `CurrentUser` por nombre y `roleCode` dinámicos más `permissions`, usando el DTO de desarrollo; verificar con una prueba de rol personalizado que `AppLayout` no muestra error de sesión.
- [x] 4.2 Convertir `nav.ts` y `Sidebar` en un registro estático de rutas filtrable por permiso, quitar `/membresia-roles` y omitir grupos vacíos; cubrir `portal.ver`, `aulas.ver`, `tareas.ver`, `usuarios.ver`, `docentes.ver`, `roles.ver`, `designaciones.gestionar`, `designaciones.revisar` y `periodos.administrar` con `Sidebar.test.tsx`.
- [x] 4.3 Reemplazar `RequireRole` por el guard mínimo de permiso y aplicarlo a Roles, Usuarios, Docentes, Períodos, Mis pedidos, alta/edición de pedidos, Revisión y detalle según sus APIs; verificar que el acceso directo sin permiso redirige sin renderizar contenido.
- [x] 4.4 Hacer que la renovación de la sesión y la invalidación de React Query reflejen altas y bajas de permisos; verificar que agregar o quitar un permiso cambia sidebar y guards tras validar nuevamente la sesión, sin depender del nombre del rol.

## 5. Pantalla unificada de Roles

- [x] 5.1 Consolidar la página, componentes y modelos de `features/membresia-roles` dentro de `features/roles`, manteniendo el layout de lista lateral y panel derecho; verificar que `/roles` carga lista, selección, búsqueda por nombre/descripción y permisos.
- [x] 5.2 Mantener `/membresia-roles` como redirect a `/roles`, retirar su route lazy y la entrada duplicada de navegación; verificar una navegación legacy y que sólo existe el enlace “Roles”.
- [x] 5.3 Integrar “Nuevo rol” en la pantalla unificada con ámbito, rol base y permisos heredados, habilitado por `roles.administrar`; verificar creación, validaciones, copia puntual de permisos y selección del nuevo rol.
- [x] 5.4 Ajustar edición para que sólo roles personalizados permitan renombrar/describir, conservando código y versión; representar metadatos de roles de sistema como sólo lectura y ocultar cualquier acción de renombrado efectiva; verificar ambos caminos con pruebas de componentes.
- [x] 5.5 Agregar confirmación y mutación de baja lógica sólo para roles personalizados, ocultar roles inactivos y refrescar la selección/lista después de eliminar; verificar que el rol desaparece, no se puede usar como base y el sistema no ofrece restauración ficticia.
- [x] 5.6 Usar el panel de permisos para roles institucionales y personalizados, con carga/error/guardado accesibles, control por `roles.gestionar_membresia` y renovación del `version` seleccionado después de invalidar consultas; verificar guardado consecutivo y conflicto de concurrencia.

## 6. Documentación y contrato

- [x] 6.1 Actualizar `docs/architecture/api-contracts-administracion.md` con el DELETE, cuerpos/versiones, protecciones de sistema, baja lógica, permisos de pantalla y DTO de desarrollo; verificar que rutas, autorizaciones y respuestas coinciden con los controllers.
- [x] 6.2 Actualizar `docs/architecture/data-model.md` para documentar `is_active`, conservación de relaciones y código estable de roles personalizados, y agregar `docs/product/designs/administracion-roles-permisos-design-spec.md` con el layout unificado, estados y reglas de edición; verificar que los documentos no describen `/membresia-roles` como pantalla independiente.
- [x] 6.3 Revisar las especificaciones vigentes relacionadas y el mapeo de pruebas sin sincronizar ni archivar este cambio todavía; verificar `pnpm exec openspec validate --all --strict`.

## 7. Verificación integral

- [x] 7.1 Ejecutar `dotnet test backend/ArsDocendi.slnx` y `pnpm --filter frontend test:run`; verificar sesión personalizada, navegación por permisos, unificación de rutas, roles de sistema y baja lógica.
- [x] 7.2 Ejecutar `pnpm --filter frontend lint`, `pnpm --filter frontend build` y `pnpm format:check`; corregir sólo errores introducidos por este cambio y confirmar que el worktree conserva los cambios preexistentes no relacionados.
