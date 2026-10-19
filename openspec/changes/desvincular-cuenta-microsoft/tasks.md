# Tasks

Pendiente de implementación en otra PR. Cada tarea se verifica con sus pruebas antes de marcarse; no marcar por intención.

## 1. Backend

- [ ] 1.1 Escribir primero las pruebas: desvincular vacía el vínculo y conserva roles, estado y último ingreso; sin vínculo responde validación sin cambios; una versión vieja responde 409; sin `usuarios.administrar` responde 403; tras desvincular, un ingreso con otra cuenta y el mismo mail verificado vuelve a vincularla; la operación queda en `audit.change_log` con el operador como actor. Verificar que fallan antes de implementar.
- [ ] 1.2 Extender `UsuarioAdministracionDto` con `cuentaMicrosoftVinculada` y `ultimoIngresoEn` (la columna es `last_login_at`), sin exponer `oid` ni tenant.
- [ ] 1.3 Implementar `POST /api/administracion/usuarios/{id}/desvincular-cuenta-microsoft` en `UsuariosController` y `ServicioUsuarios`/`RepositorioUsuarios`. Verificar que las pruebas de 1.1 pasan.
- [ ] 1.4 Documentar el endpoint y los campos nuevos en `docs/architecture/api-contracts-administracion.md` (regla 6 de AGENTS.md).

## 2. Frontend y UX

- [ ] 2.1 Actualizar `docs/product/designs/administracion-usuarios-docentes-design-spec.md` con la columna, la acción y el modal (regla 9 de AGENTS.md).
- [ ] 2.2 Agregar la columna vinculada/pendiente y la acción "Desvincular cuenta" en `features/usuarios`, con confirmación, invalidación del listado y manejo de 409. Verificar con Vitest.
- [ ] 2.3 Probar en local: entrar con Microsoft, desvincular como `sys_admin` y volver a entrar para comprobar que se vincula de nuevo.

## 3. Cierre

- [ ] 3.1 Correr `dotnet test backend/ArsDocendi.slnx`, `pnpm --filter frontend test:run`, `pnpm --filter frontend lint`, `pnpm --filter frontend build`, `pnpm format:check` y `pnpm exec openspec validate --all --strict`.
