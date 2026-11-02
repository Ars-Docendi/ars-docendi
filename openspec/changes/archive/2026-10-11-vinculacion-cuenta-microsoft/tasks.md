## 1. Diseño

- [ ] 1.1 Actualizar `docs/product/designs/administracion-usuarios-docentes-design-spec.md` con la columna, la acción y el modal, usando los componentes existentes de `@ars-docendi/ui` (sin diseño en Pencil). Verificar con `pnpm format:check`.

## 2. Backend

- [ ] 2.1 Escribir primero las pruebas: el listado y el detalle informan `cuentaMicrosoftVinculada` y `ultimoIngresoEn` sin exponer `oid` ni tenant; desvincular vacía el vínculo y conserva roles, estado y último ingreso, con el operador como actor en `audit.change_log`; un usuario sin vínculo da error de validación sin cambios; una versión vieja da `409`; sin `usuarios.administrar` da `403`; tras desvincular, un ingreso con otra cuenta y el mismo mail verificado se acepta y queda vinculado. Verificar que fallan antes de implementar.
- [ ] 2.2 Extender `UsuarioAdministracionDto` y su mapeo, y agregar la desvinculación en `ServicioUsuarios`/`RepositorioUsuarios` y `POST /api/administracion/usuarios/{id}/desvincular-cuenta-microsoft` en `UsuariosController`. Verificar que las pruebas de 2.1 pasan.
- [ ] 2.3 Documentar el endpoint y los campos nuevos en `docs/architecture/api-contracts-administracion.md`. Verificar con `pnpm format:check`.

## 3. Frontend

- [ ] 3.1 Agregar los campos al modelo y la API de `features/usuarios`, la mutación de desvincular con invalidación del listado y el manejo de `409`. Verificar con Vitest.
- [ ] 3.2 Implementar la columna, la acción condicionada a vínculo y permiso, y el modal según el design spec actualizado. Verificar con Vitest en `TablaUsuarios`: columna vinculada/pendiente, acción visible sólo en filas vinculadas con permiso, confirmación que llama a la API y conflicto que mantiene el modal abierto.

## 4. Verificación

- [ ] 4.1 Correr `dotnet test backend/ArsDocendi.slnx`, `pnpm --filter frontend test:run`, `pnpm --filter frontend lint`, `pnpm --filter frontend build`, `pnpm format:check` y `pnpm exec openspec validate --all --strict`.
- [ ] 4.2 Probar en local: entrar con Microsoft, ver la cuenta vinculada en `/usuarios`, desvincularla como `sys_admin` y volver a entrar para comprobar que se vincula de nuevo.
