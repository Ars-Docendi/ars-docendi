## Purpose

Alta de usuarios del sistema desde la página de gestión de usuarios: la Secretaría/Administración carga una persona completa (nombre, apellido, documento, legajo, fecha de nacimiento, UPN, roles, y datos opcionales) mediante un modal con formulario en grilla de 2 columnas. El usuario queda activo al crearse.

## Requirements

### Requirement: Formulario de alta de usuario con datos completos de persona

La página SHALL proveer un botón "Nuevo usuario" que abre un modal con un formulario organizado en grilla de 2 columnas. El formulario SHALL solicitar:

- **Obligatorios**: Nombre, Apellido, Documento (DNI), Legajo, Fecha de nacimiento, UPN (email institucional), al menos un Rol.
- **Opcionales**: CUIL, Teléfono.

Los botones de acción ("Cancelar" / "Crear usuario") SHALL estar en extremos opuestos del footer (`justify-content: space-between`); el botón "Cancelar" SHALL tener fondo rojo.

#### Scenario: Apertura del modal

- **WHEN** el operador hace clic en "Nuevo usuario"
- **THEN** se abre un modal con el formulario vacío en grilla de 2 columnas

#### Scenario: Alta exitosa

- **WHEN** el operador completa todos los campos obligatorios, selecciona al menos un rol y confirma
- **THEN** el modal se cierra y el nuevo usuario aparece en la tabla con `is_active = true` y sus datos de persona

#### Scenario: UPN duplicada

- **WHEN** el operador ingresa una UPN que ya existe en el listado
- **THEN** el formulario muestra un error inline "Ya existe un usuario con esa UPN" y no permite confirmar

### Requirement: Validación de campos obligatorios

Nombre, Apellido, Documento, Legajo, Fecha de nacimiento, UPN y al menos un Rol son obligatorios. Al intentar confirmar con alguno vacío SHALL mostrarse error por campo.

#### Scenario: Intento de confirmar con campos vacíos

- **WHEN** el operador hace clic en confirmar con uno o más campos obligatorios vacíos o sin roles seleccionados
- **THEN** se muestran mensajes de error por campo y el usuario no se crea

#### Scenario: Cancelar alta

- **WHEN** el operador cierra el modal sin confirmar (botón Cancelar rojo o tecla Escape)
- **THEN** el modal se cierra y no se agrega ningún usuario

### Requirement: Layout de 2 columnas para los campos de persona

Los campos del formulario SHALL presentarse en grilla de 2 columnas para reducir el scroll vertical:

- Fila 1: Nombre | Apellido
- Fila 2: Documento | Legajo
- Fila 3: CUIL | Fecha de nacimiento
- Fila 4: UPN (ancho completo)
- Fila 5: Teléfono (ancho completo)
- Fila 6: Roles / checkboxes (ancho completo)

#### Scenario: Disposición de los campos en grilla

- **WHEN** el operador abre el modal de alta de usuario
- **THEN** los campos se presentan en una grilla de 2 columnas con el orden de filas especificado (Nombre|Apellido, Documento|Legajo, CUIL|Fecha de nacimiento, y UPN, Teléfono y Roles a ancho completo)
