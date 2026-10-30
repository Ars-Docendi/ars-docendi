## MODIFIED Requirements

### Requirement: Visualización de permisos por rol

Las pantallas de alta y edición de rol SHALL mostrar, debajo de los datos del rol y sin encabezado propio, todos los permisos del sistema agrupados por módulo. El módulo SHALL deducirse del prefijo del código del permiso. Cada módulo SHALL ser un bloque desplegable, cerrado por defecto, con checkbox de módulo, nombre y contador `asignados/total`; al desplegarlo SHALL listar sus permisos con un checkbox cada uno.

#### Scenario: Panel derecho muestra todos los permisos al seleccionar un rol

- **WHEN** el operador abre la edición de un rol
- **THEN** ve un bloque por módulo con su nombre y el contador `asignados/total`

#### Scenario: Desplegar un módulo muestra sus permisos

- **WHEN** el operador despliega el bloque de un módulo
- **THEN** ve los permisos de ese módulo con su nombre, su descripción y su estado actual

#### Scenario: Checkboxes reflejan el estado actual de membresía

- **WHEN** se muestran los permisos de un rol
- **THEN** los permisos asignados aparecen marcados y los no asignados desmarcados, sin inferir el estado a partir del nombre del rol

#### Scenario: Checkbox de módulo refleja sus permisos

- **WHEN** el rol tiene todos, ninguno o una parte de los permisos de un módulo
- **THEN** el checkbox del módulo aparece marcado, desmarcado o en estado intermedio, respectivamente

### Requirement: Modificación de permisos de un rol

El operador con `roles.gestionar_membresia` SHALL poder marcar y desmarcar permisos, de a uno o por módulo, incluidos los de roles de sistema. Marcar un módulo desmarcado o intermedio SHALL marcar todos sus permisos; desmarcarlo SHALL desmarcarlos todos. Los cambios SHALL aplicarse sólo al guardar y SHALL conservar el control de concurrencia del rol.

#### Scenario: Marcar un permiso lo agrega visualmente

- **WHEN** el operador marca el checkbox de un permiso no asignado
- **THEN** el checkbox queda marcado y el contador del módulo se actualiza, sin persistirse todavía

#### Scenario: Desmarcar un permiso lo quita visualmente

- **WHEN** el operador desmarca el checkbox de un permiso asignado
- **THEN** el checkbox queda desmarcado y el contador del módulo se actualiza, sin persistirse todavía

#### Scenario: Marcar un módulo intermedio

- **WHEN** el operador marca el checkbox de un módulo en estado intermedio
- **THEN** todos los permisos del módulo quedan marcados, sin persistirse todavía

#### Scenario: Desmarcar un módulo completo

- **WHEN** el operador desmarca el checkbox de un módulo con todos sus permisos asignados
- **THEN** todos los permisos del módulo quedan desmarcados, sin persistirse todavía

#### Scenario: Guardar cambios persiste la membresía

- **WHEN** el operador guarda
- **THEN** la API reemplaza el conjunto de permisos del rol, la grilla muestra el nuevo contador y una nueva solicitud de sesión obtiene esos permisos

#### Scenario: Cambiar de rol sin guardar descarta los cambios pendientes

- **WHEN** el operador sale de la pantalla con "Cancelar" o volviendo a la grilla sin guardar
- **THEN** los cambios pendientes se descartan y el rol conserva su estado guardado

#### Scenario: Conflicto de concurrencia conserva los cambios pendientes

- **WHEN** otro operador modifica el rol antes de guardar
- **THEN** la pantalla muestra el conflicto, no confirma la membresía y permite recargar el estado antes de reintentar

#### Scenario: Operador sin permiso de gestión

- **GIVEN** un operador sin `roles.gestionar_membresia`
- **WHEN** despliega un módulo
- **THEN** ve los checkboxes de módulo y de permisos deshabilitados

## ADDED Requirements

### Requirement: Buscador de permisos

La sección de permisos de las pantallas de alta y edición de rol SHALL ofrecer un buscador que filtre por nombre o descripción del permiso, o por nombre del módulo, sin distinguir mayúsculas ni tildes. Con texto, SHALL mostrar sólo los módulos con coincidencias, desplegados y con los permisos que coinciden; el checkbox y el contador del módulo SHALL seguir refiriéndose al módulo completo. Al vaciarlo, los módulos SHALL volver a mostrarse completos y cerrados.

#### Scenario: Buscar un permiso interno de un módulo

- **WHEN** el operador escribe parte del nombre de un permiso
- **THEN** ve desplegado sólo el módulo que lo contiene, con ese permiso a la vista

#### Scenario: Búsqueda sin coincidencias

- **WHEN** el texto no coincide con ningún permiso ni módulo
- **THEN** la sección informa que no hay permisos que coincidan

### Requirement: Catálogo sin permisos sin uso

El catálogo de permisos MUST NOT ofrecer permisos que no habilitan ninguna pantalla ni operación. `portal.editar`, `reportes.ver` y `sistema.parametrizar` SHALL retirarse junto con sus asignaciones; editar el propio Mi Portal no requiere permiso y corregir el de otra persona requiere `portal.administrar`.

#### Scenario: Permisos retirados no aparecen

- **WHEN** el operador abre la edición de cualquier rol
- **THEN** no ve `portal.editar`, `reportes.ver` ni `sistema.parametrizar` entre los permisos
