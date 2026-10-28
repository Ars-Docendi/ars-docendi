## ADDED Requirements

### Requirement: Buscador emergente de materias

Las pantallas de administración que asignan una materia SHALL ofrecer un campo de solo lectura con la selección actual y un botón "Buscar" (o "Cambiar" cuando ya hay selección) que abre el modal "Buscar materia". El modal MUST seguir el formato del buscador de Responsable de Tareas: fila de filtros con botón "Buscar", tabla de resultados con filas clickeables y botón "Cancelar". Hacer clic en el campo de solo lectura SHALL abrir el mismo modal.

#### Scenario: Abrir el buscador desde una fila

- **GIVEN** la pantalla de nueva persona en `/personas/nueva` con una asignación sin materia
- **WHEN** el usuario hace clic en "Buscar" de esa asignación
- **THEN** se abre "Buscar materia" con los filtros vacíos y todas las materias disponibles listadas

#### Scenario: Cancelar sin elegir

- **GIVEN** el buscador abierto desde una fila que ya tiene materia
- **WHEN** el usuario hace clic en "Cancelar" o cierra el modal
- **THEN** la fila conserva su materia anterior y los filtros se descartan

### Requirement: Filtros por materia y carrera

El buscador SHALL filtrar por código de materia, nombre de materia, código de carrera y nombre de carrera. Los filtros MUST combinarse (todos deben coincidir), MUST buscar por contenido sin distinguir mayúsculas ni tildes y SHALL aplicarse al hacer clic en "Buscar" o presionar Enter. El modal SHALL mostrar la cantidad de resultados y una acción "Limpiar filtros" cuando hay filtros cargados.

#### Scenario: Filtrar por código de carrera

- **GIVEN** el buscador abierto desde una asignación
- **WHEN** el usuario escribe "201" en código de carrera y hace clic en "Buscar"
- **THEN** sólo se listan materias dictadas en la carrera de código 201

#### Scenario: Nombre sin tildes

- **WHEN** el usuario escribe "informatica" en nombre de carrera y presiona Enter
- **THEN** se listan las materias de "Ingeniería en Informática"

#### Scenario: Sin resultados

- **WHEN** ningún par materia–carrera coincide con los filtros
- **THEN** la tabla muestra "No hay materias que coincidan con la búsqueda."

### Requirement: Resultados según el ámbito de la fila

Para una asignación o una membresía de Docente, cada resultado SHALL ser un par materia–carrera vigente del catálogo y elegirlo MUST completar la materia y la carrera de la fila. Para una membresía de Jefe de Cátedra, cada resultado SHALL ser una materia canónica, con las carreras en que se dicta, y elegirla MUST completar sólo la materia. Los filtros de carrera MUST seguir funcionando en ese modo, conservando las materias dictadas en alguna carrera coincidente.

#### Scenario: Elegir materia en una carrera para una asignación

- **GIVEN** el buscador abierto desde una asignación
- **WHEN** el usuario hace clic en la fila "01023 · ANALISIS MATEMATICO I · 207 · Ingeniería Civil"
- **THEN** el modal se cierra y la asignación muestra esa materia con la carrera Ingeniería Civil

#### Scenario: Jefe de Cátedra elige la materia canónica

- **GIVEN** una membresía con rol "Jefe de Cátedra"
- **WHEN** el usuario abre el buscador
- **THEN** cada materia aparece una sola vez con la lista de carreras en que se dicta
- **AND** al elegirla la membresía queda con la materia y sin carrera

#### Scenario: Cambiar el rol descarta el ámbito

- **GIVEN** una membresía "Docente" con materia y carrera elegidas
- **WHEN** el usuario cambia el rol a "Jefe de Cátedra"
- **THEN** la materia y la carrera de esa membresía se vacían

### Requirement: Opciones no elegibles visibles con su motivo

El buscador MUST mostrar deshabilitadas, con su motivo, las opciones que la pantalla no admite, en lugar de ocultarlas: una materia que ya tiene otra asignación del mismo docente ("Ya asignada", en cualquier carrera) y un ámbito que ya tiene otra membresía con el mismo rol ("Ya tiene esta membresía"). La selección actual de la fila SHALL verse resaltada. La validación definitiva de ámbito y unicidad MUST permanecer en el backend.

#### Scenario: Materia ya asignada

- **GIVEN** el docente tiene una asignación en "01023" para la carrera 201
- **WHEN** el usuario abre el buscador desde otra asignación
- **THEN** todas las filas de "01023" se ven deshabilitadas con "Ya asignada" y no se pueden elegir

#### Scenario: Selección actual resaltada

- **WHEN** el usuario abre el buscador desde una fila que ya tiene materia
- **THEN** esa opción aparece resaltada y se puede volver a elegir

### Requirement: Modales estirables

El modal "Buscar materia" SHALL abrirse con un tamaño fijo que muestre varias filas y SHALL poder redimensionarse desde la esquina inferior derecha dentro de un mínimo y del tamaño de la ventana. El encabezado y el pie MUST permanecer visibles y la tabla de resultados SHALL ocupar el alto disponible. La alta y edición de personas son pantallas completas y no usan modal.

#### Scenario: Agrandar el buscador

- **WHEN** el usuario arrastra la esquina de "Buscar materia" hacia abajo
- **THEN** la tabla de resultados muestra más filas sin desplazar los filtros ni el pie

### Requirement: Catálogo con código de carrera

El catálogo de personas (`GET /api/administracion/personas/catalogos`) SHALL incluir en cada par materia–carrera el id de la materia canónica, el código y el nombre de la materia, y el id, código y nombre de la carrera.

#### Scenario: Par materia–carrera del catálogo de personas

- **GIVEN** una materia dictada en una carrera activa
- **WHEN** un operador obtiene el catálogo de personas
- **THEN** el par informa el id de la materia (no el de la fila materia–plan) y el código de la carrera
