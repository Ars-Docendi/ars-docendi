# Proposal

## Why

El change archivado `2026-10-11-vinculacion-cuenta-microsoft` quedó con sus tareas de implementación sin marcar. Su requisito de desvincular la cuenta Microsoft sigue vigente en `openspec/specs/vinculacion-cuenta-microsoft/spec.md`, pero el endpoint, los campos del DTO y la acción de `/usuarios` no existen en `backend/src` ni en `frontend/src`. Sin desvincular, una persona vinculada a una cuenta equivocada no tiene salida desde la app.

## What Changes

- Implementar `POST /api/administracion/usuarios/{id}/desvincular-cuenta-microsoft` con control de versión y auditoría: vacía `AzureOid` y `AzureTid` y conserva roles, datos personales, estado y último ingreso.
- Sumar `cuentaMicrosoftVinculada` y `ultimoIngresoEn` al DTO de usuarios, sin exponer `oid` ni tenant.
- Agregar la acción "Desvincular cuenta" en `/usuarios`, con confirmación, visible solo con vínculo y permiso `usuarios.administrar`.
- Documentar el endpoint y los campos en `docs/architecture/api-contracts-administracion.md`.

No cambia ningún requisito vigente: este change solo implementa lo ya especificado.

## Estado

Pendiente, para otra PR. El change archivado queda como historia; este change lleva el trabajo que faltó.

## Impact

- Backend: `ServicioUsuarios`, `RepositorioUsuarios`, `UsuariosController`, DTO de usuarios.
- Frontend: `features/usuarios` (tabla y modal de confirmación).
- Documentación: contrato de API de administración y design spec de usuarios si cambia la UX.
