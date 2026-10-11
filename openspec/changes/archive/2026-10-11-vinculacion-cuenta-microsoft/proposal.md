## Why

Desde el change `login-microsoft`, cada usuario queda vinculado a su cuenta Microsoft en el primer ingreso y desde entonces se lo reconoce por ese vínculo. Si a una persona le recrean la cuenta Microsoft, o si su primer ingreso se hizo con una cuenta que no correspondía, queda bloqueada ("otra cuenta con el mismo mail") y la única salida hoy es editar la base a mano. Además, la administración no tiene forma de saber si un usuario ya ingresó, que es lo primero que se necesita para diagnosticar un "no puedo entrar".

## What Changes

- La tabla de `/usuarios` muestra, para cada usuario, si su cuenta Microsoft está **vinculada** (con la fecha del último ingreso) o **pendiente de primer ingreso**.
- Nueva acción **"Desvincular cuenta"** en la fila de un usuario vinculado, con modal de confirmación, para quien tenga `usuarios.administrar`. Vacía el vínculo; el próximo ingreso de la persona lo vuelve a crear con su mail verificado. No cambia roles, datos personales ni el estado activo/inactivo.
- Nuevo endpoint `POST /api/administracion/usuarios/{id}/desvincular-cuenta-microsoft` con control de concurrencia por versión, auditado con el operador como actor.
- El DTO de usuarios suma `cuentaMicrosoftVinculada` y `ultimoIngresoEn`. Nunca expone el `oid` ni el tenant.

## Capabilities

### New Capabilities

- `vinculacion-cuenta-microsoft`: consulta del estado de vinculación y último ingreso de un usuario, y desvinculación administrativa de su cuenta Microsoft.

### Modified Capabilities

- `listar-usuarios`: la tabla suma la columna "Cuenta Microsoft" y la acción "Desvincular cuenta" para usuarios vinculados.

## Impact

- **Backend:** `ServicioUsuarios`/`RepositorioUsuarios` (Shared, superficie de administración de identidad) y `UsuariosController` (Host). Sin migraciones: usa `azure_oid`, `azure_tid` y `last_login_at` existentes.
- **Frontend:** `features/usuarios` (tabla, modal de confirmación, hooks y API).
- **API y docs:** `api-contracts-administracion.md` y design spec de Usuarios (sin diseño en Pencil).
- **Dependencias:** depende de `login-microsoft` (migración `013` y vínculo por `oid`). Sin edges nuevos en el grafo.
- **Normativa:** no aplica; sin BR nuevas.
- **Rollback:** revertir el frontend y el endpoint; los vínculos ya borrados se recrean solos en el próximo ingreso de cada persona.
