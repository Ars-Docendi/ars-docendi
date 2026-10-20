# Domain: almacenamiento de archivos

## Propósito

Storage controla metadata y ciclo de vida de los adjuntos privados. Los bytes se guardan en SeaweedFS mediante S3; PostgreSQL conserva propiedad, estado, metadata validada y clave interna. Los clientes sólo reciben `archivoId` y descargas autorizadas por backend.

## Bounded context y contratos

- Storage es dueño de `storage.archivos`, reservas de carga/revisión/eliminación y estado de disponibilidad.
- Los módulos consumidores no escriben metadata/objetos directamente. Portal y Designaciones implementan `IConsultasReferenciasArchivos` y participan en `IProteccionArchivos` con su conexión/transacción local.
- No hay FK entre schemas. Una composición incompleta o un lector ausente/fallido no equivale a “sin referencias”: el borrado cierra fail-safe.
- La exclusión se toma por archivo en orden estable. Asociación, desvinculación y consulta de referencias usan la misma transacción local; rollback revierte la metadata participante, sin MSDTC.

## Asociar, reemplazar y borrar

1. Sólo se asocia un archivo disponible y propiedad del actor/operación autorizada; los clientes no suministran bucket, clave ni URI.
2. Desvincular o reemplazar primero persiste la nueva asociación y retira la anterior dentro del guard; después evalúa retención. Si Portal o Designaciones mantienen otra referencia, los bytes se conservan. El DELETE directo de archivo referenciado devuelve `409 archivo-referenced`.
3. Para archivo sin referencias, Storage reserva primero el estado no disponible (`cuarentena`, fase `eliminando`) bajo exclusión. El I/O remoto ocurre fuera de la transacción DB. Si falla, queda no descargable y el error `409 archivo-delete-pending` permite reintentar; no se restaura a disponible. Sólo tras éxito remoto se marca eliminado.
4. El recolector vuelve a evaluar referencias y vencimiento bajo el mismo protocolo. Nunca elimina claves arbitrarias ni claves publicadas por intentos ajenos; fallos de lectores impiden el borrado.

Los clientes sólo reciben `archivoId` y descargas autorizadas por backend. Ver el [contrato API](../api-contracts.md) y [runbook operativo](../../operations/storage-runbook.md). La operación no promete una transacción distribuida entre PostgreSQL y SeaweedFS ni protección frente a un DBA/proveedor externo que omita el protocolo.

## Interfaces relevantes

- `ArsDocendi.Storage.Contracts.IConsultasReferenciasArchivos`: nombre canónico del consumidor y consulta por ID.
- `ArsDocendi.Storage.Contracts.IProteccionArchivos`: operación transaccional local con conjunto ordenado de IDs.
- Participantes registrados explícitamente por Portal y Designaciones; no se descubre consumidores por reflexión ni se interpreta la ausencia como cero.

## Verificación

Las regresiones con PostgreSQL y SeaweedFS descartables están en `backend/tests/ArsDocendi.IntegrationTests/Storage/` y `Portal/ReferenciasPortalTests.cs`. Incluyen intercalaciones de asociación/delete, rollback multicontexto real, archivo compartido Portal/pedido, fallo/recuperación de borrado remoto, revisión tardía, consulta de consumidor fallida y aislamiento de bytes ajenos. Matriz/resultado del change: [evidencia post-auditoría](../../evidence/portal-storage-tareas-post-auditoria.md).
