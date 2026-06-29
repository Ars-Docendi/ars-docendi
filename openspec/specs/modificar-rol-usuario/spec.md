## Purpose

Edición completa de un usuario existente desde la tabla de gestión de usuarios. Reemplaza el flujo previo "Editar roles" (solo checkboxes de rol) por un modal "Editar usuario" que permite modificar todos los datos de la persona, incluyendo roles, con las mismas validaciones que el alta.

## Requirements

### Requirement: Edición completa del usuario desde la tabla

Cada fila SHALL tener un botón ghost "Editar" que abre un modal pre-poblado con todos los campos del usuario. Al confirmar, todos los datos del usuario en la tabla SHALL actualizarse (incluyendo nombre, apellido, documento, legajo, CUIL, fecha de nacimiento, teléfono, UPN y roles).

#### Scenario: Apertura del modal de edición

- **WHEN** el operador hace clic en "Editar" en cualquier fila
- **THEN** se abre el modal "Editar usuario" con todos los campos pre-cargados con los valores actuales del usuario

#### Scenario: Edición exitosa

- **WHEN** el operador modifica uno o más campos y hace clic en "Guardar cambios"
- **THEN** el modal se cierra y la tabla refleja los valores actualizados del usuario

#### Scenario: Cancelar edición

- **WHEN** el operador hace clic en "Cancelar" (rojo) o presiona Escape
- **THEN** el modal se cierra sin aplicar ningún cambio

#### Scenario: UPN duplicada al editar

- **WHEN** el operador ingresa una UPN que ya pertenece a otro usuario (no al mismo)
- **THEN** se muestra "Ya existe otro usuario con esa UPN" y no se puede confirmar

#### Scenario: Intentar guardar con campos obligatorios vacíos

- **WHEN** el operador borra un campo obligatorio (nombre, apellido, documento, legajo, fecha de nacimiento, UPN o todos los roles) y hace clic en "Guardar cambios"
- **THEN** se muestran mensajes de error por campo y los cambios no se aplican

### Requirement: Campos del formulario de edición

El modal SHALL mostrar los mismos campos que el alta de usuario en la misma grilla de 2 columnas:

- Fila 1: Nombre | Apellido
- Fila 2: Documento | Legajo
- Fila 3: CUIL | Fecha de nacimiento
- Fila 4: UPN (ancho completo)
- Fila 5: Teléfono (ancho completo)
- Fila 6: Roles / checkboxes (ancho completo)

Obligatorios: Nombre, Apellido, Documento, Legajo, Fecha de nacimiento, UPN, al menos un Rol.
Opcionales: CUIL, Teléfono.

#### Scenario: Identificación del usuario en el modal

- **WHEN** el modal se abre
- **THEN** se muestra en el encabezado el nombre completo y UPN actual del usuario para confirmación visual

### Requirement: Sincronización de estado del formulario sin efecto

El modal SHALL re-inicializar sus campos internos cada vez que la prop `usuario` cambia (p.ej. al abrir el modal para un usuario diferente o al cerrarlo). Esta sincronización SHALL implementarse comparando la prop con el valor anterior durante el render (`if (usuario !== prevUsuario)`), **no** mediante `useEffect`. El uso de `useEffect` para llamar a `setState` directamente en su cuerpo está prohibido en esta base de código (`react-hooks/set-state-in-effect`).

#### Scenario: Reapertura del modal para otro usuario

- **WHEN** el modal se cierra y se vuelve a abrir para un usuario distinto
- **THEN** los campos internos se re-inicializan con los valores del nuevo usuario sin arrastrar el estado anterior

### Requirement: Botones del modal con separación y colores semánticos

El footer del modal SHALL ubicar "Cancelar" (rojo) a la izquierda y "Guardar cambios" (primary) a la derecha, con `justify-content: space-between`.

#### Scenario: Disposición de los botones

- **WHEN** el modal está abierto
- **THEN** el botón "Cancelar" (rojo) está a la izquierda y "Guardar cambios" (primary) está a la derecha (`justify-content: space-between`)
