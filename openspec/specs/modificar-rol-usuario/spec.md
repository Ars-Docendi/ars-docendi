## Purpose

Edición completa de un usuario existente desde la tabla de gestión de usuarios. Reemplaza el flujo previo "Editar roles" (solo checkboxes de rol) por un modal "Editar usuario" que permite modificar todos los datos de la persona, incluyendo roles, con las mismas validaciones que el alta.

## Requirements

### Requirement: Edición persistente de identidad y roles

La edición completa del usuario MUST enviarse a la API y MUST guardar atómicamente los datos de persona, cuenta y la lista completa de membresías de rol con sus ámbitos. La interfaz MUST conservar IDs de rol, materia y carrera, no etiquetas visibles. Si cualquier parte es inválida o la versión es obsoleta, ninguna modificación MUST quedar aplicada.

#### Scenario: Edición completa exitosa

- **GIVEN** datos válidos, membresías con ámbitos compatibles y un operador autorizado
- **WHEN** el backend confirma la edición
- **THEN** una nueva consulta devuelve conjuntamente todos los valores actualizados y cada rol conserva su materia o carrera

#### Scenario: Usuario con roles por materia

- **GIVEN** un usuario tiene `docente` en una materia y `jefe_catedra` en otra
- **WHEN** el operador guarda datos personales sin cambiar membresías
- **THEN** la edición conserva exactamente ambas asignaciones

#### Scenario: Una asignación de rol es inválida

- **GIVEN** cambios de persona válidos y una membresía con ámbito inválido o repetida
- **WHEN** se intenta guardar el formulario
- **THEN** la API rechaza la operación completa y conserva tanto los datos de persona como las membresías anteriores

### Requirement: Edición completa del usuario desde la tabla

Cada fila SHALL tener un botón ghost "Editar" que abre un modal pre-poblado con todos los campos del usuario y sus membresías. Al confirmar, todos los datos del usuario en la tabla SHALL actualizarse, incluyendo nombre, apellido, documento, legajo, CUIL, fecha de nacimiento, teléfono, UPN y el resumen de roles.

#### Scenario: Apertura del modal de edición

- **WHEN** el operador hace clic en "Editar" en cualquier fila
- **THEN** se abre el modal "Editar usuario" con todos los campos y membresías actuales pre-cargados

#### Scenario: Edición exitosa

- **WHEN** el operador modifica uno o más campos y hace clic en "Guardar cambios"
- **THEN** el modal se cierra y la tabla refleja los valores actualizados sin repetir roles por ámbito

#### Scenario: Cancelar edición

- **WHEN** el operador hace clic en "Cancelar" (rojo) o presiona Escape
- **THEN** el modal se cierra sin aplicar ningún cambio

#### Scenario: Conflicto de concurrencia

- **WHEN** otro operador modifica el usuario antes de guardar
- **THEN** el modal permanece abierto y muestra que los datos deben actualizarse antes de reintentar

#### Scenario: UPN duplicada al editar

- **WHEN** el operador ingresa una UPN que ya pertenece a otro usuario (no al mismo)
- **THEN** se muestra "Ya existe otro usuario con esa UPN" y no se puede confirmar

#### Scenario: Intentar guardar con campos obligatorios vacíos

- **WHEN** el operador borra un campo obligatorio o elimina todas las membresías
- **THEN** se muestran mensajes de error por campo y los cambios no se aplican

### Requirement: Campos del formulario de edición

El modal SHALL mostrar los mismos campos que el alta en la misma grilla de 2 columnas y SHALL reemplazar los checkboxes globales de roles por una sección de membresías donde cada fila identifique rol y ámbito.

- Fila 1: Nombre | Apellido
- Fila 2: Documento | Legajo
- Fila 3: CUIL | Fecha de nacimiento
- Fila 4: UPN (ancho completo)
- Fila 5: Teléfono (ancho completo)
- Fila 6: Membresías de rol (ancho completo)

Obligatorios: Nombre, Apellido, Documento, Legajo, Fecha de nacimiento, UPN y al menos una membresía.
Opcionales: CUIL, Teléfono.

#### Scenario: Identificación del usuario en el modal

- **WHEN** el modal se abre
- **THEN** se muestra en el encabezado el nombre completo y UPN actual del usuario para confirmación visual

#### Scenario: Membresía por ámbito

- **WHEN** el operador agrega una membresía
- **THEN** puede seleccionar un rol activo y sólo los ámbitos compatibles con el rol

### Requirement: Sincronización de estado del formulario sin efecto

El modal SHALL re-inicializar sus campos internos cada vez que la prop `usuario` cambia, incluyendo todas sus membresías. Esta sincronización SHALL implementarse comparando la prop con el valor anterior durante el render (`if (usuario !== prevUsuario)`), no mediante `useEffect`.

#### Scenario: Reapertura del modal para otro usuario

- **WHEN** el modal se cierra y se vuelve a abrir para un usuario distinto
- **THEN** los campos y membresías internas se re-inicializan con los valores del nuevo usuario sin arrastrar el estado anterior

### Requirement: Botones del modal con separación y colores semánticos

El footer del modal SHALL ubicar "Cancelar" (rojo) a la izquierda y "Guardar cambios" (primary) a la derecha, con `justify-content: space-between`.

#### Scenario: Disposición de los botones

- **WHEN** el modal está abierto
- **THEN** el botón "Cancelar" (rojo) está a la izquierda y "Guardar cambios" (primary) está a la derecha

### Requirement: Fecha de nacimiento sin icono SVG adicional

El campo "Fecha de nacimiento" del modal "Editar usuario" SHALL usar el control nativo de fecha
del navegador y MUST NOT renderizar un SVG de calendario separado dentro del campo. El valor, la
edición y la validación del campo SHALL conservar el comportamiento existente.

#### Scenario: Apertura del campo de fecha

- **GIVEN** el operador abre "Editar usuario" para una cuenta con fecha de nacimiento
- **WHEN** el modal se muestra
- **THEN** el campo contiene la fecha actual y no existe un elemento SVG de calendario adicional
  asociado al campo

#### Scenario: Guardado de la fecha

- **GIVEN** el operador modifica una fecha válida
- **WHEN** confirma "Guardar cambios"
- **THEN** el formulario envía la fecha modificada con el mismo formato y junto con el resto de
  los datos del usuario
