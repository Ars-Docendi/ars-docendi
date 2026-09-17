## Context

El formulario actual recibe `catedra` desde `PedidoFormPage` y muestra un selector de materia antes de la novedad. `PedidoForm` conserva la materia como contexto fijo y `SeccionDocentePedido` usa un catálogo de personas sin designaciones para Alta. El catálogo de Designaciones ya entrega las materias visibles del actor y las designaciones vigentes de cada persona, pero el frontend descarta el `materiaId` de esas designaciones.

En backend, `ServicioPedidos` valida que la materia pertenezca al ámbito del actor, pero no valida que la materia seleccionada para Baja/Cambio sea una designación vigente del docente. El DTO de guardado exige `PersonaId`, aunque identity ya define personas sin cuenta para el caso de Alta y el vinculador de primer login crea la cuenta Azure AD después.

## Goals / Non-Goals

**Goals:**

- Hacer que la novedad y el docente determinen el conjunto válido de materias.
- Mantener `materiaId` como autoridad canónica desde el formulario hasta el backend.
- Permitir un Alta con datos de persona nueva sin UPN, cuenta Azure AD ni legajo.
- Crear o resolver la persona canónica sin duplicar documentos y sin provisionar usuarios externos.
- Revalidar en backend el ámbito del Jefe y la relación entre docente y materia.
- Preservar la carrera derivada de la materia y el snapshot vigente de Baja/Cambio.

**Non-Goals:**

- No crear cuentas, contraseñas ni invitaciones en Azure AD.
- No cambiar la unicidad de un pedido por persona y período.
- No permitir que el Alta elija materias fuera del ámbito del Jefe.
- No agregar una entidad `es_docente` ni duplicar roles en personas.
- No modificar el circuito de aprobación ni la materialización de designaciones.

## Decisions

### 1. Una sola fuente de materia en el formulario

Se eliminará el selector superior de `PedidoFormPage`. El estado del formulario tendrá un único `materiaId`.

La sección de datos del pedido resolverá sus opciones según el estado:

```text
Tipo de novedad
|
+-- Alta
|   +-- datos nuevos de persona
|   +-- materias del actor con rol Jefe de Cátedra
|
+-- Baja / Cambio
    +-- docente existente
    +-- designaciones del docente ∩ materias del actor
```

El catálogo de asignaciones conservará `materiaId`, nombre y datos vigentes. Si el conjunto tiene una opción se seleccionará automáticamente; si tiene varias se exigirá elección; si está vacío se mostrará un error accionable y se bloqueará el guardado.

El cambio de materia en Baja/Cambio actualizará cargo, dedicación y horas vigentes a partir de esa asignación, sin usar la primera designación del docente como sustituto. La materia elegida seguirá enviándose como UUID canónico.

### 2. La API valida el contexto completo

El servicio de pedidos recibirá la materia elegida y construirá el conjunto permitido a partir del actor autenticado y, para Baja/Cambio, de las designaciones vigentes de la persona. La API rechazará cualquier `materiaId` fuera de ese conjunto, incluso si el cliente manipuló el payload.

Para Baja/Cambio se consultará la designación vigente de `(personaId, materiaId)` antes de validar horas, snapshot y novedad. Esa misma fila será la fuente de los datos actuales. Para Alta sólo se validará que la materia esté dentro de `MateriasACargo` del actor.

### 3. Alta con datos de persona, no con selector de personas

El DTO de guardado tendrá dos formas mutuamente excluyentes:

- `personaId` para Baja/Cambio y otros pedidos sobre una persona existente.
- `persona` con DNI, nombre y apellido para Alta.

El backend rechazará una combinación incorrecta. El flujo de Alta solicitará la creación o resolución de la persona canónica mediante una operación pública de la superficie de administración de identity, sin acceso de Designaciones al `IdentityDbContext`. El módulo recibirá el `personaId` resuelto y persistirá el pedido; no se duplicarán datos personales en Designaciones.

La operación de identity será idempotente por documento dentro del comando y devolverá conflicto ante un documento ya registrado, salvo que el contrato futuro defina explícitamente una referencia canónica permitida. No creará UPN, `identity.users`, contraseña ni invitación Azure AD.

### 4. La cuenta se resuelve en el primer login

Se reutilizará el `IVinculadorPrimerLogin` existente: cuando la persona tenga una cuenta Azure AD y se autentique, el sistema creará o actualizará `identity.users` y fijará `persona_id`. La aprobación del pedido no provisionará la cuenta ni reemplazará otros roles de la persona.

### 5. Fronteras y transacción

La escritura de `identity.personas` quedará encapsulada en el servicio público de administración de identity. Designaciones sólo consumirá la referencia canónica y continuará leyendo identity mediante `IConsultasIdentity` para autorización.

La creación de persona y pedido deberá ejecutarse con una estrategia transaccional o idempotente que impida un pedido apuntando a una persona inexistente. Si el proveedor actual no permite compartir transacción entre ambos contextos, el comando de persona será idempotente y el pedido se creará sólo después de recibir una referencia confirmada; un fallo posterior no deberá crear una segunda persona en el reintento.

### 6. Compatibilidad durante el despliegue

Frontend y backend se desplegarán coordinadamente. Durante una transición, el backend podrá aceptar `personaId` sólo para pedidos existentes o Baja/Cambio, pero no deberá volver a exponer personas como opción de Alta. La eliminación del selector superior se hará junto con el nuevo estado de materia para evitar enviar el UUID de la primera materia como valor implícito.

## Risks / Trade-offs

- **[Persona creada y pedido fallido]** -> Usar una operación idempotente por documento y una transacción compartida cuando esté disponible; si no, conservar la persona sin cuenta como entidad canónica auditada y permitir reintento sin duplicación.
- **[El catálogo de asignaciones pierde el ID de materia]** -> Mantener `materiaId` en el modelo frontend y agregar tests que verifiquen que se envía el UUID elegido, no el nombre.
- **[Cliente manipula una materia compatible de otra cátedra]** -> Validar nuevamente actor, materia y persona en backend; el filtrado visual nunca será autoridad.
- **[Persona con múltiples roles]** -> El Alta no crea ni revoca roles; el primer login sólo vincula la cuenta. Las membresías existentes se conservan.
- **[Cambio de contrato]** -> Documentar el payload nuevo y desplegarlo junto con el frontend; mantener compatibilidad acotada sólo para ediciones y pedidos históricos.

## Migration Plan

1. Agregar tests de contrato y dominio que fallen para el Alta con datos y para la materia contextual.
2. Incorporar el comando público de identity y el payload de Alta sin retirar todavía la lectura necesaria para pedidos existentes.
3. Implementar la validación backend de intersección actor-docente-materia.
4. Refactorizar el frontend para eliminar el selector superior, conservar IDs canónicos y mostrar la materia contextual.
5. Actualizar documentación de API, arquitectura, modelo de datos y design spec.
6. Ejecutar la suite proporcional y validar OpenSpec estricto.

Rollback: volver a mostrar el formulario anterior sólo si el backend mantiene temporalmente su contrato anterior; no reintroducir el catálogo indiscriminado de personas como solución permanente.
