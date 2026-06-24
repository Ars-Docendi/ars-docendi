## ADDED Requirements

### Requirement: Dos modos de alta en el modal

El modal de alta SHALL ofrecer dos modos seleccionables: "Nueva persona" y "Persona del sistema". En ambos modos el Rol y las Asignaciones (materia+cargo) son obligatorios.

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

El sistema SHALL ofrecer un `Select` obligatorio para elegir el rol de sistema del docente: "Docente" o "Jefe de Cátedra". En el alta se selecciona un único rol (en primera instancia es improbable que el docente tenga ambos simultáneamente). La asignación de múltiples roles se hace desde la edición. El rol es independiente del cargo académico por materia y determina los permisos en la plataforma.

#### Scenario: Rol no seleccionado en alta

- **WHEN** el usuario hace clic en "Crear docente" sin seleccionar un Rol
- **THEN** el campo Rol muestra "Campo obligatorio"

#### Scenario: Selección de Rol "Jefe de Cátedra" en alta

- **WHEN** el usuario selecciona "Jefe de Cátedra" y completa el resto del formulario
- **THEN** el docente creado tiene `roles = ["Jefe de Cátedra"]` y aparece con ese badge en la tabla

### Requirement: Asignaciones materia+cargo+horas con patrón de filas añadibles

El sistema SHALL mostrar el componente `AsignacionesSelector` donde cada fila tiene un `Select` de materia, un `Select` de cargo y un `Input` numérico de horas. El usuario MUST poder agregar más filas con "+ Agregar asignación". Se requiere al menos una fila completa (los tres campos rellenos, horas > 0).

#### Scenario: Fila inicial vacía

- **WHEN** el usuario abre el modal
- **THEN** hay una fila de asignación con ambos selectores vacíos

#### Scenario: Alta con asignación completa

- **WHEN** el usuario selecciona materia "03500", cargo "Jefe de Trabajos Prácticos" y horas "8" en la primera fila y hace clic en "Crear docente"
- **THEN** el docente creado tiene esa asignación y la tabla la muestra como badge "03500 – JTP · 8h"

#### Scenario: Agregar segunda asignación

- **WHEN** el usuario hace clic en "+ Agregar asignación"
- **THEN** aparece una nueva fila con materia, cargo y horas vacíos

#### Scenario: Fila incompleta bloquea el submit

- **WHEN** el usuario tiene una fila con materia y cargo seleccionados pero sin horas y hace clic en "Crear docente"
- **THEN** se muestra error "Completá o quitá las filas incompletas"

#### Scenario: Horas inválidas bloquean el submit

- **WHEN** el usuario ingresa 0 o un valor negativo en el campo de horas
- **THEN** se muestra error "Las horas deben ser mayor a 0"

#### Scenario: Sin asignaciones bloquea el submit

- **WHEN** el usuario no tiene ninguna asignación completa y hace clic en "Crear docente"
- **THEN** se muestra error "Agregá al menos una asignación"

#### Scenario: Materia ya elegida no aparece en otras filas

- **WHEN** el usuario seleccionó materia "03500" en una fila
- **THEN** esa materia no aparece disponible en los selectores de las demás filas
