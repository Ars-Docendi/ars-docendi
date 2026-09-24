## MODIFIED Requirements

### Requirement: Lista Mis pedidos

`/designaciones/mis-pedidos` SHALL listar los pedidos visibles del período, con número, persona, legajo, materia, novedad, estado y prioridad. La fila SHALL abrir el detalle (con click, o con Enter o Espacio cuando está enfocada); la tabla MUST NOT mostrar un botón "Ver". Editar y Eliminar SHALL mostrarse sólo cuando la API incluya esas acciones.

#### Scenario: Filtros

- **WHEN** se filtra por docente, número, legajo, tipo o estado
- **THEN** la tabla conserva sólo las coincidencias sin distinguir mayúsculas ni acentos

#### Scenario: Estados remotos

- **WHEN** la consulta carga, falla, queda vacía o responde con datos
- **THEN** la pantalla representa explícitamente Loading, Error, Empty o Success

### Requirement: Filtros y ordenamiento por encabezado en Mis pedidos

La tabla de `/designaciones/mis-pedidos` SHALL ofrecer un control de filtro accesible en cada encabezado de datos: N°, Docente, Legajo, Cátedra, Tipo, Enviado y Estado. La columna Acciones MUST NOT ofrecer filtro ni ordenamiento. Los criterios de texto SHALL buscar coincidencias parciales sin distinguir mayúsculas ni tildes; los criterios categóricos SHALL permitir seleccionar uno o más valores. Los filtros de columnas distintas SHALL combinarse con lógica AND y las opciones múltiples de una misma columna SHALL combinarse con lógica OR.

La tabla SHALL permitir ordenar N°, Docente, Legajo, Cátedra, Tipo, Enviado y Estado desde sus encabezados. Cada orden SHALL alternar entre ascendente, descendente y sin orden manual, y el orden SHALL aplicarse después de filtrar.

#### Scenario: Filtrar por docente desde el encabezado

- **GIVEN** la tabla contiene pedidos de varios docentes
- **WHEN** el operador abre el filtro del encabezado "Docente" y escribe una parte del nombre sin tilde
- **THEN** la tabla muestra sólo los pedidos cuyo docente coincide sin distinguir mayúsculas ni tildes

#### Scenario: Filtrar por tipo y estado

- **GIVEN** la tabla contiene pedidos de distintos tipos y estados
- **WHEN** el operador selecciona "Alta" en Tipo y "En revisión" en Estado
- **THEN** la tabla muestra únicamente pedidos que cumplen ambos criterios

#### Scenario: Limpiar un filtro sin perder los demás

- **GIVEN** existen filtros activos en Docente y Estado
- **WHEN** el operador activa "Limpiar filtro" en Docente
- **THEN** se elimina sólo el criterio de Docente y el filtro de Estado continúa aplicado

#### Scenario: Ordenar después de filtrar

- **GIVEN** un filtro activo dejó visibles una parte de los pedidos
- **WHEN** el operador ordena el encabezado "Enviado" en forma ascendente
- **THEN** sólo las filas visibles se ordenan cronológicamente y no reaparecen pedidos excluidos

#### Scenario: Filtrar sobre la lista completa

- **GIVEN** la lista tiene más pedidos de los que entran en pantalla
- **WHEN** el operador aplica un filtro desde un encabezado
- **THEN** la tabla muestra todos los pedidos que cumplen el filtro, sin paginar, y conserva la navegación a detalle y las acciones permitidas

#### Scenario: Operación accesible del encabezado

- **WHEN** el operador enfoca el control de filtro de un encabezado y presiona Enter o Espacio
- **THEN** se abre el menú sin activar la navegación de la fila, y el encabezado comunica el orden mediante su estado accesible cuando corresponde
