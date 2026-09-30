## Why

El panel «Uso del asistente» sólo deja editar el cupo diario, y en una sola dirección: una vez que un usuario tiene un cupo propio no hay forma de devolverlo al del rol, y no existe ningún control de **acceso**. Quién puede usar el asistente lo decide únicamente el permiso `asistente.consultar` de identity, que el módulo no puede escribir (regla 4 de AGENTS.md) y que no distingue a un usuario puntual de su rol. El diseño aprobado del panel («Made with Claude Design», columna «Acceso») pide control granular por rol y por usuario.

## What Changes

- **Acceso por rol**: cada rol de sistema tiene un interruptor «Con acceso / Sin acceso», persistido en `asistente.presupuesto_rol.acceso_habilitado` (default `true`, así ningún despliegue cambia quién accede hoy).
- **Revocación por usuario**: a un usuario se le puede **quitar** el acceso, nunca darlo por encima de su rol. Se persiste en `asistente.acceso_usuario_revocado`; restablecer borra la revocación y el usuario vuelve a heredar del rol.
- **Restablecer el cupo propio**: `DELETE …/presupuestos/usuarios/{actorId}` cierra la vigencia del override y el usuario vuelve al cupo del rol.
- **El turno se bloquea de verdad**: un actor sin acceso efectivo recibe el motivo nuevo `sin_acceso` (mismo carril que cupo agotado / tope / mantenimiento), sin llamar al modelo.
- **Panel**: columna «Acceso» en «Por usuario» y «Por rol», con origen («del rol» / «propio»), ícono para restablecer, y el cupo atenuado y no editable cuando el usuario no tiene acceso.
- Cada cambio se audita en `asistente.auditoria_administracion` (`acceso.rol`, `acceso.usuario`, `presupuesto.usuario.restablecer`).

## Capabilities

### New Capabilities

- `asistente-acceso-al-asistente`: acceso heredado del rol con revocación por usuario, y su aplicación en el turno.

### Modified Capabilities

- `asistente-presupuesto-persistente`: el override de un usuario puede restablecerse al default del rol.

## Impact

- `database/asistente/006_asistente_administracion.sql` — columna y tabla nuevas, idempotentes.
- `backend/src/Modules.Asistente/` — regla de acceso efectivo, puerto `IAccesoAlAsistente`, endpoints de administración, motivo `sin_acceso`.
- `frontend/src/features/asistente/` — panel de uso y el indicador de cupo.
- `docs/architecture/api-contracts.md`, `docs/architecture/data-model.md`, design spec del asistente.
