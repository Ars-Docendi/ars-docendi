## Why

El formulario actual obliga a elegir una persona ya registrada para un pedido de Alta y permite seleccionar personas que no son docentes, como Administración o Decanato. Además, la materia se elige antes de conocer la novedad y el docente, lo que obliga a un Jefe de Cátedra con varias materias a iniciar el trámite desde un contexto incorrecto y permite inconsistencias en Baja y Cambio.

La documentación vigente define que un Alta incorpora los datos de una persona nueva y que cada pedido corresponde a una única materia autorizada. El flujo debe reflejar esas reglas y dejar la materia contextualizada por la novedad y el docente seleccionado.

## What Changes

- Reemplazar en Alta el selector de personas por datos nuevos de DNI y apellido/nombre.
- Crear o recuperar la persona canónica al registrar el Alta, sin crear automáticamente una cuenta `identity.users`.
- Mantener el vínculo de la cuenta de Azure AD para el primer login posterior, sin duplicar la persona ni eliminar otros roles que pudiera tener.
- Remover el selector superior de materia de `Nuevo pedido`.
- Mostrar la materia después de seleccionar la novedad y el docente:
  - en Alta, cualquier materia donde el actor tenga una membresía vigente de Jefe de Cátedra;
  - en Baja/Cambio, sólo las materias con designación vigente del docente seleccionado que además estén dentro del ámbito del actor.
- Autoseleccionar la materia cuando exista una sola opción y mostrar un selector cuando existan varias.
- Bloquear el guardado si no existe una materia compatible.
- Validar en backend la materia, el ámbito del actor y la relación docente-materia; no confiar en el filtrado del frontend.
- Actualizar los contratos de creación/edición, la documentación de API, el modelo de datos documentado y los tests correspondientes.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `pedidos-designacion`: cambia la captura de la persona en Alta, la selección contextual de materia y las validaciones por novedad.
- `persistencia-designaciones`: refuerza que Baja/Cambio sólo puedan referir una designación vigente del docente en la materia elegida.
- `persistencia-identity`: formaliza la creación de una persona sin cuenta durante un Alta y el vínculo posterior de la cuenta Azure AD en el primer login.

## Impact

- **Frontend:** `features/designaciones`, especialmente `PedidoFormPage`, `PedidoForm`, `SeccionDocentePedido`, modelos, catálogos y tests.
- **Backend:** API y servicio de pedidos de Designaciones; validación de la materia según actor, persona y novedad; operación de identidad para crear o recuperar la persona canónica.
- **Contratos:** el payload de Alta deberá aceptar datos de persona o una referencia canónica resuelta por el backend; los UUID de materia seguirán siendo canónicos.
- **Identidad:** una persona podrá persistir con DNI y nombre, sin legajo, UPN ni usuario Azure AD. El primer login seguirá siendo el mecanismo de creación/vínculo de `identity.users`.
- **Documentación:** actualizar `docs/product/designs/proyecto-docente-design-spec.md`, `docs/architecture/api-contracts.md`, `docs/architecture/data-model.md` y `docs/architecture/domains/` según el contrato final.
- **Dependencias:** se conserva el DAG actual; Designaciones seguirá leyendo identidad y la creación de personas deberá atravesar una frontera pública de administración, sin escritura directa desde el módulo.
- **Rollback:** si el nuevo payload aún no puede desplegarse junto con el frontend, mantener temporalmente compatibilidad de lectura con `personaId`; desactivar el nuevo formulario antes de eliminar el contrato anterior.
