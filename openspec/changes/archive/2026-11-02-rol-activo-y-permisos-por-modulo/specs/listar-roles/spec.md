## MODIFIED Requirements

### Requirement: Visualización de roles en tabla

La pantalla `/roles` SHALL mostrar los roles activos e inactivos en la grilla administrativa de la guía de diseño, con las columnas Nombre, Descripción, Ámbito, Tipo (Sistema o Personalizado), Permisos (`asignados/total`), Estado y Acciones. Las acciones SHALL ser "Editar" y, en roles personalizados y con `roles.administrar`, "Desactivar" o "Activar" según su estado. Cada fila SHALL ser clickeable y abrir la pantalla de edición del rol. El acceso SHALL requerir `roles.ver`, sin depender del nombre del rol.

#### Scenario: Acceso autorizado carga la pantalla unificada

- **WHEN** un usuario con `roles.ver` navega a `/roles`
- **THEN** la pantalla carga la grilla de roles con su estado

#### Scenario: Acceso autorizado carga la tabla

- **WHEN** un usuario con `roles.ver` navega a `/roles`
- **THEN** cada fila muestra Nombre, Descripción, Ámbito, Tipo, la cantidad de permisos asignados sobre el total y su Estado

#### Scenario: Acceso no autorizado redirige

- **WHEN** un usuario sin `roles.ver` intenta acceder a `/roles`
- **THEN** el sistema redirige a `/`

#### Scenario: La lista permite seleccionar y administrar

- **WHEN** el operador hace clic en una fila o elige "Editar" en sus acciones
- **THEN** se abre la pantalla de edición de ese rol, y las acciones de la fila ofrecen "Desactivar" o "Activar" sólo para roles personalizados

#### Scenario: La tabla muestra nombre y descripción

- **WHEN** la grilla de roles es visible
- **THEN** cada fila muestra el Nombre y la Descripción del rol, y las acciones permitidas

### Requirement: Buscador de roles

La grilla de `/roles` SHALL permitir filtrar por encabezado de columna y ordenar por columna, como las demás grillas administrativas. El filtro de Nombre y Descripción SHALL ignorar mayúsculas y tildes.

#### Scenario: Búsqueda filtra por nombre

- **WHEN** el operador escribe texto en el filtro de la columna Nombre
- **THEN** la grilla muestra únicamente los roles cuyo Nombre contiene el texto ingresado

#### Scenario: Búsqueda vacía muestra todos

- **WHEN** no hay filtros aplicados
- **THEN** la grilla muestra todos los roles, activos e inactivos

#### Scenario: Sin resultados muestra estado vacío

- **WHEN** los filtros no coinciden con ningún rol
- **THEN** la grilla muestra un estado vacío informativo
