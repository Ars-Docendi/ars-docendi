## MODIFIED Requirements

### Requirement: Dos modos de alta en el modal

El modal de alta SHALL ofrecer dos modos seleccionables: "Nueva persona" y "Persona del sistema". En ambos modos las membresías de rol por ámbito y las asignaciones académicas materia+cargo son obligatorias.

#### Scenario: Modo "Nueva persona" por defecto

- **WHEN** el usuario abre el modal "Nuevo docente"
- **THEN** el modo "Nueva persona" está activo con todos los campos personales vacíos

#### Scenario: Selección de persona existente en modo "Persona del sistema"

- **WHEN** el usuario selecciona una persona del catálogo en modo "Persona del sistema"
- **THEN** se muestra una tarjeta con Apellido y Nombre, DNI y UPN de la persona

#### Scenario: Persona ya registrada como docente

- **WHEN** el usuario selecciona una persona cuya UPN ya existe como docente y hace clic en "Crear docente"
- **THEN** se muestra el error de UPN duplicada y no se crea el registro

### Requirement: Selección de Rol en alta (Docente / Jefe de Cátedra)

El sistema SHALL permitir seleccionar una o más membresías de rol de sistema (`Docente` o `Jefe de Cátedra`), cada una vinculada a una materia compatible. El alta MUST permitir que roles distintos se asignen a materias distintas y no SHALL asumir un único rol global.

#### Scenario: Rol no seleccionado en alta

- **WHEN** el usuario hace clic en "Crear docente" sin seleccionar una membresía
- **THEN** la sección de membresías muestra "Seleccioná al menos una membresía"

#### Scenario: Selección de Rol "Jefe de Cátedra" en alta

- **WHEN** el usuario selecciona "Jefe de Cátedra" y completa el resto del formulario
- **THEN** el docente creado conserva una membresía "Jefe de Cátedra" en la materia elegida y aparece con ese badge en la tabla

#### Scenario: Roles distintos por materia

- **WHEN** el usuario selecciona `Docente` para Materia A y `Jefe de Cátedra` para Materia B y completa las asignaciones
- **THEN** el docente creado conserva exactamente esas dos membresías por materia

#### Scenario: Ámbito incompatible

- **WHEN** el usuario intenta seleccionar una materia incompatible con el rol
- **THEN** la interfaz no ofrece esa combinación o muestra el error de ámbito sin crear el docente
