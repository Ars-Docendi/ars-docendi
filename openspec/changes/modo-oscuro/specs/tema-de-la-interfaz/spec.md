## ADDED Requirements

### Requirement: Elección del tema

El menú de usuario SHALL ofrecer el tema Claro, Oscuro o Sistema, con la opción vigente marcada. Sistema SHALL ser el valor por defecto y SHALL seguir el modo del dispositivo, también cuando cambia con el sistema abierto.

#### Scenario: Elegir oscuro

- **WHEN** la persona elige Oscuro en el menú de usuario
- **THEN** toda la interfaz pasa a la paleta oscura sin recargar la página

#### Scenario: Seguir al dispositivo

- **WHEN** la preferencia es Sistema y el dispositivo pasa a modo oscuro
- **THEN** la interfaz pasa a la paleta oscura

### Requirement: Persistencia por navegador

La preferencia SHALL guardarse en el navegador y SHALL aplicarse antes del primer pintado, incluida la pantalla de ingreso. Sin almacenamiento disponible SHALL durar la sesión.

#### Scenario: Volver al sistema

- **WHEN** la persona eligió Oscuro y vuelve a abrir el sistema en el mismo navegador
- **THEN** la primera pantalla ya se muestra en oscuro, sin destello claro

### Requirement: Paleta oscura legible

En oscuro, las superficies SHALL ordenarse de más oscura a más clara como fondo, superficie y superficie elevada; ningún componente SHALL conservar un fondo blanco fijo, y los fondos de hover y selección SHALL seguir siendo oscuros bajo texto claro.

#### Scenario: Componente de la librería con fondo fijo

- **WHEN** se muestra un campo, un modal, una tabla o un botón secundario en oscuro
- **THEN** su fondo es la superficie elevada del tema, no blanco
