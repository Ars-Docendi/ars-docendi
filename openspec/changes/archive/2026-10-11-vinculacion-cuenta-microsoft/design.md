## Context

Motivación en `proposal.md`; comportamiento en los specs delta.

- `login-microsoft` dejó `identity.users.azure_oid`/`azure_tid` vacíos hasta el primer ingreso, que los completa, y actualiza `last_login_at` en cada ingreso aceptado. Un usuario ya vinculado sólo se reconoce por ese vínculo: otra cuenta con el mismo mail se rechaza.
- La administración de usuarios vive en `ArsDocendi.Shared/Identity/Administracion` (`ServicioUsuarios`, `RepositorioUsuarios`) y se expone en `UsuariosController`. Activar y desactivar ya usan `POST .../{id}/activar|desactivar` con la versión del usuario para concurrencia optimista.
- En `/usuarios` no hay pantalla de detalle: las acciones son botones ghost por fila, cada una con su modal de confirmación.

## Goals / Non-Goals

**Goals:**

- Que la administración vea quién ya ingresó y cuándo, sin exponer identificadores de Microsoft.
- Destrabar a una persona sin editar la base.

**Non-Goals:**

- Cerrar la sesión vigente de la persona al desvincular (ver Riesgos).
- Filtrar u ordenar por la columna nueva.
- Vincular manualmente una cuenta concreta: el vínculo siempre lo crea el ingreso de la persona.

## Decisions

### 1. Estado derivado, sin exponer el `oid`

El DTO de usuarios suma `cuentaMicrosoftVinculada` (`azure_oid IS NOT NULL`) y `ultimoIngresoEn` (`last_login_at`). El `oid` y el tenant no salen de la base: alcanzan para el diagnóstico y no agregan datos pseudónimos a la API. `ServicioAuditoria` ya oculta `azure_oid` y `azure_tid` en el detalle de auditoría.

### 2. Endpoint dedicado, como activar/desactivar

`POST /api/administracion/usuarios/{id}/desvincular-cuenta-microsoft` con `{ version }`, política `usuarios.administrar`. Devuelve el `UsuarioAdministracionDto` actualizado.

- Si la cuenta no está vinculada, responde un error de validación sin cambios.
- Una versión desactualizada responde `409`, igual que las demás mutaciones de usuarios.
- Vacía `azure_oid` y `azure_tid` juntos, porque el `CHECK` de la migración `013` exige ambos o ninguno. Conserva `last_login_at`: es historia, no parte del vínculo.
- La escritura usa la superficie de administración de identidad, así que el interceptor de auditoría registra al operador como actor.
- Alternativa descartada: incluirlo en `PUT /usuarios/{id}`. Mezclaría una acción puntual y confirmada con la edición de datos, y haría falta un campo nuevo en el formulario.

### 3. Frontend

- **Columna "Cuenta Microsoft":** muestra "Vinculada · último ingreso dd/mm/aaaa" o "Pendiente de primer ingreso", con los componentes existentes de `@ars-docendi/ui` (no se inventan componentes).
- **Acción "Desvincular cuenta":** ghost, sólo para filas vinculadas y si el operador tiene `usuarios.administrar`. Abre un modal de confirmación con el mismo patrón que activar/desactivar. Explica que la persona va a volver a vincularse en su próximo ingreso y que la acción no le quita el acceso actual.
- **Después de confirmar:** se invalida la consulta de usuarios. Un `409` mantiene el modal abierto y pide recargar.
- **Diseño:** sin Pencil, por decisión del equipo. Se reutilizan el botón ghost, el badge y el modal de confirmación de activar/desactivar, y se documenta en el design spec de Usuarios.

## Risks / Trade-offs

- [Desvincular no corta una sesión ya abierta] → La cookie identifica al usuario, no a la cuenta Microsoft. Para cortar el acceso de inmediato se desactiva al usuario; el modal lo aclara.
- [Un operador desvincula por error] → La persona se vuelve a vincular sola en su próximo ingreso y el cambio queda en auditoría.
- [Muchos usuarios pendientes al principio de prod] → Es esperable: la columna justamente muestra quién falta ingresar.

## Migration Plan

Sin migraciones. Se despliega backend y frontend juntos; el rollback es revertir ambos.
