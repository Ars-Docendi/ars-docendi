## ADDED Requirements

### Requirement: Filtros y ordenamiento por encabezado en períodos

La tabla de períodos de designación SHALL ofrecer un control de filtro accesible en los encabezados Nombre, Carga desde, Carga hasta, Impacto desde, Impacto hasta y Activo. La columna Acciones MUST NOT ofrecer filtro ni ordenamiento. Los filtros textuales SHALL buscar coincidencias parciales sin distinguir mayúsculas ni tildes; el filtro Activo SHALL permitir seleccionar Activo, Inactivo o ambos.

La tabla SHALL permitir ordenar Nombre, Carga desde, Carga hasta, Impacto desde, Impacto hasta y Activo desde sus encabezados. Las fechas SHALL ordenarse cronológicamente usando el valor almacenado, no el texto formateado; cada orden SHALL alternar entre ascendente, descendente y sin orden manual.

#### Scenario: Filtrar por nombre desde el encabezado

- **GIVEN** existen períodos con nombres diferentes
- **WHEN** el operador escribe una parte del nombre en el filtro de Nombre
- **THEN** la tabla muestra sólo los períodos coincidentes sin distinguir mayúsculas ni tildes

#### Scenario: Filtrar por estado activo

- **GIVEN** la tabla contiene períodos activos e inactivos
- **WHEN** el operador selecciona "Activo" en el filtro del encabezado Activo
- **THEN** sólo se muestran los períodos activos

#### Scenario: Ordenar fechas cronológicamente

- **GIVEN** los períodos tienen fechas de carga o impacto en años y meses diferentes
- **WHEN** el operador ordena ascendentemente la columna "Impacto desde"
- **THEN** los períodos se presentan por fecha cronológica y no por el texto "Mes Año"

#### Scenario: Limpiar un filtro sin afectar otros

- **GIVEN** hay filtros activos en Nombre y Activo
- **WHEN** el operador activa "Limpiar filtro" en Nombre
- **THEN** se elimina sólo el criterio de Nombre y Activo continúa aplicado

#### Scenario: Acciones sin filtro ni orden

- **WHEN** el operador observa el encabezado Acciones
- **THEN** no se muestra control de filtro ni indicador de ordenamiento en esa columna

#### Scenario: Operación accesible del encabezado

- **WHEN** el operador enfoca el control de filtro y presiona Enter o Espacio
- **THEN** se abre el menú asociado sin ejecutar una acción de fila y el orden activo se comunica al lector de pantalla
