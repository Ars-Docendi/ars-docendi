## MODIFIED Requirements

### Requirement: Asignaciones materia+cargo+horas con patrón de filas añadibles

El sistema SHALL mostrar el componente `AsignacionesSelector` donde cada fila tiene un campo de solo lectura de materia y carrera con el botón "Buscar"/"Cambiar" del buscador de materias, un `Select` de cargo, un `Select` de dedicación y un `Input` numérico de horas. El usuario MUST poder agregar más filas con "+ Agregar asignación". Se requiere al menos una fila completa (materia, carrera, cargo y dedicación elegidos, horas > 0).

#### Scenario: Fila inicial vacía

- **WHEN** el usuario abre el modal
- **THEN** hay una fila de asignación sin materia, con el campo "Ninguna materia seleccionada" y el botón "Buscar"

#### Scenario: Alta con asignación completa

- **WHEN** el usuario elige en el buscador la materia "03500" de una carrera, selecciona cargo "Jefe de Trabajos Prácticos", una dedicación y horas "8" en la primera fila y hace clic en "Crear docente"
- **THEN** el docente creado tiene esa asignación y la tabla la muestra como badge "03500 – JTP · 8h"

#### Scenario: Agregar segunda asignación

- **WHEN** el usuario hace clic en "+ Agregar asignación"
- **THEN** aparece una nueva fila sin materia y con cargo, dedicación y horas vacíos

#### Scenario: Fila incompleta bloquea el submit

- **WHEN** el usuario tiene una fila con materia y cargo seleccionados pero sin horas y hace clic en "Crear docente"
- **THEN** se muestra error "Completá o quitá las filas incompletas"

#### Scenario: Horas inválidas bloquean el submit

- **WHEN** el usuario ingresa 0 o un valor negativo en el campo de horas
- **THEN** se muestra error "Las horas deben ser mayor a 0"

#### Scenario: Sin asignaciones bloquea el submit

- **WHEN** el usuario no tiene ninguna asignación completa y hace clic en "Crear docente"
- **THEN** se muestra error "Agregá al menos una asignación"

#### Scenario: Materia ya elegida no se puede elegir en otras filas

- **WHEN** el usuario eligió la materia "03500" en una fila y abre el buscador desde otra fila
- **THEN** las opciones de "03500" aparecen deshabilitadas con "Ya asignada", en cualquier carrera
