## Purpose

Hace visible para la administración qué cuentas están vinculadas a un perfil docente y permite recorrer la relación sin duplicar personas, cuentas ni datos personales.

## ADDED Requirements

### Requirement: Identificación de perfil docente en Usuarios

La tabla de Usuarios SHALL mostrar un indicador de perfil docente cuando la cuenta tenga una membresía `docente` o `jefe_catedra` activa o una designación docente vigente. El indicador SHALL incluir, cuando corresponda, el número de materias alcanzadas.

#### Scenario: Usuario docente

- **GIVEN** una cuenta tiene una membresía docente o una designación vigente
- **WHEN** el administrador consulta `/usuarios`
- **THEN** la fila muestra "Docente" y no presenta la cuenta como un usuario administrativo genérico

#### Scenario: Usuario no docente

- **GIVEN** una cuenta no tiene membresías docentes ni designaciones vigentes
- **WHEN** el administrador consulta `/usuarios`
- **THEN** la fila muestra que no tiene perfil docente

#### Scenario: Filtro de docentes

- **WHEN** el administrador activa el filtro de perfil docente
- **THEN** la tabla muestra sólo las cuentas con perfil docente

### Requirement: Navegación entre Usuario y Docente

La administración SHALL ofrecer una acción real para abrir la ficha docente desde una cuenta con perfil docente y SHALL permitir volver desde la ficha docente a la cuenta vinculada cuando exista.

#### Scenario: Cuenta con ficha docente

- **GIVEN** una cuenta tiene `personaId` y perfil docente
- **WHEN** el administrador selecciona "Ver docente"
- **THEN** se abre la información docente de la misma persona sin crear un registro nuevo

#### Scenario: Docente con cuenta vinculada

- **GIVEN** una ficha docente tiene una cuenta asociada
- **WHEN** el administrador consulta la ficha
- **THEN** se muestra el estado de la cuenta y una acción para abrir el usuario relacionado

### Requirement: Distinción de docentes sin cuenta

La tabla de Docentes SHALL distinguir una persona docente con cuenta de una persona docente sin cuenta, sin impedir la consulta de sus designaciones.

#### Scenario: Docente sin cuenta

- **GIVEN** existe una persona con designación vigente y sin usuario vinculado
- **WHEN** el administrador consulta `/docentes`
- **THEN** la tabla muestra "Sin cuenta" y conserva sus datos docentes
