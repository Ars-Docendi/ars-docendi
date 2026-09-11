## ADDED Requirements

### Requirement: La tabla de Usuarios no muestra ámbitos

La tabla visible de `/usuarios` MUST mostrar los datos administrativos definidos para la cuenta
sin renderizar una columna `Ámbitos`. Las membresías y sus ámbitos MUST conservarse disponibles
para el formulario de edición y no deben eliminarse del contrato ni del estado de la pantalla.

#### Scenario: Usuario con membresías

- **GIVEN** una cuenta con una o más membresías de rol en distintos ámbitos
- **WHEN** un operador autorizado consulta `/usuarios`
- **THEN** la tabla muestra roles, estado, perfil docente y acciones sin incluir el encabezado ni
  la celda `Ámbitos`

#### Scenario: Edición conserva las membresías

- **GIVEN** un usuario con membresías cargadas en la consulta
- **WHEN** el operador abre "Editar usuario"
- **THEN** el formulario conserva esas membresías para editarlas aunque no exista una columna
  `Ámbitos` en la tabla
